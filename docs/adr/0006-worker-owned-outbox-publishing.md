# ADR 0006: Publicação do Outbox sob responsabilidade do Worker

- Status: aceito; migração pendente
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
