# ADR 0006: Publicação do Outbox sob responsabilidade do Worker

- Status: implementado
- Data: 2026-09-27

## Contexto

A API persiste dados de negócio e mensagens de Outbox atomicamente, mas a
publicação ainda é agendada dentro do processo da API. Isso acopla processamento
assíncrono ao ciclo de vida HTTP e diverge da separação definida para o Worker.

O Worker independente deve continuar o processamento quando a API estiver ociosa
ou for reiniciada.

## Decisão

A API será responsável somente por persistir `Business Data + OutboxMessage` na
mesma transação. O Worker independente fará polling em batches, claim concorrente,
publicação com publisher confirms e confirmação do processamento.

O claim usará PostgreSQL com `FOR UPDATE SKIP LOCKED` ou lease equivalente, sem
manter locks durante a chamada de rede. A mensagem armazenará `ProcessedAt`,
tentativas, `NextAttemptAt`, lock/lease e último erro. O processamento continuará
*at least once*.

O job da API será desativado somente depois de o Worker assumir a função em cada
ambiente e os testes de integração comprovarem a retomada após falhas.

## Consequências

- A disponibilidade do endpoint HTTP deixa de controlar a drenagem do Outbox.
- Múltiplas instâncias do Worker poderão cooperar sem publicar o mesmo claim.
- Falha após publish e antes de `ProcessedAt` ainda pode causar redelivery.
- Consumers deverão permanecer idempotentes.
- A migração exige coordenação para nunca manter dois publishers ativos.

## Implementação

- A migration `AddOutboxDispatchLease` acrescenta `ProcessedAt`, tentativas,
  `NextAttemptAt`, `LockId`, `LockedUntil` e `LastError` à Outbox da API.
- A API mantém somente a gravação transacional dos eventos; seu job legado de
  integração é removido do agendamento quando
  `OutboxProcessor:PublishIntegrationEvents=false`, valor padrão.
- O `ApiOutboxPublisher` do repositório `ECommerceWorker` faz claim em batches
  com `FOR UPDATE SKIP LOCKED`, libera a transação antes da chamada de rede e
  conclui ou reagenda apenas a mensagem que ainda possui seu lease.
- O publisher RabbitMQ do Worker usa mensagens persistentes e publisher
  confirms. Falhas usam backoff exponencial e leases vencidos são retomados.
- O Compose principal habilita `ApiOutboxPublisher__Enabled=true` somente no
  Worker, mantendo um único publisher ativo.
- Testes com PostgreSQL real comprovam exclusão entre instâncias concorrentes e
  retomada após abandono de lease.
