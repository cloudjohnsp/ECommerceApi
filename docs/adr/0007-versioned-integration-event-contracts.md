# ADR 0007: Contratos versionados de eventos de integração

- Status: aceito; migração pendente
- Data: 2026-09-27

## Contexto

API, Worker e consumers evoluem e são implantados separadamente. Nomes e payloads
implícitos, tipos compartilhados ou serialização de entidades internas tornam uma
alteração local capaz de quebrar outros processos sem aviso.

Os eventos atuais precisam ser alinhados entre os três repositórios.

## Decisão

Todo evento usará um envelope documentado contendo ao menos `MessageId`,
`EventType`, `Version`, `OccurredAt`, `CorrelationId` e `Payload`. O payload será
um contrato de transporte próprio, sem entidades de domínio ou comandos internos.

Versões existentes serão imutáveis. Mudança incompatível criará nova versão e
terá período explícito de convivência. Campos novos compatíveis serão opcionais e
consumers ignorarão campos desconhecidos.

Serão definidos contratos para o ciclo de pedido, pagamento, estoque e entrega de
e-mail, incluindo os equivalentes de `PaymentCompleted` e `OrderCancelled`.
Testes de contrato compararão exemplos serializados entre produtores e consumers.

## Consequências

- Releases independentes ganham uma fronteira verificável.
- Haverá duplicação deliberada de pequenos DTOs entre repositórios.
- Alterações incompatíveis exigirão estratégia de migração e retirada de versão.
- Routing keys e schemas deverão ser documentados junto aos contratos.
- Logs e traces poderão correlacionar HTTP, Outbox e consumo pelo mesmo envelope.
