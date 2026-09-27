# ADR 0002: Reservas de estoque persistidas

- Status: aceito; implementado
- Data: 2026-09-27

## Contexto

O agregado `Inventory` mantém estoque físico e reservado, mas não existe uma
entidade persistida que identifique cada reserva. Isso impede representar prazo,
estado e histórico por pedido e produto e dificulta expiração e reconciliação.

Somente os contadores agregados não são suficientes para comprovar qual pedido
detém cada unidade reservada.

## Decisão

Será criada a entidade `InventoryReservation`, vinculada a `Order`, `Product` e
`Inventory`, com quantidade, instante de criação, expiração e estado explícito:
`Active`, `Consumed`, `Released` ou `Expired`.

Criação de pedido e inclusão de item bloquearão os inventários em ordem estável,
validarão `AvailableStock`, criarão as reservas e atualizarão `ReservedStock` na
mesma transação. Consumo, liberação e expiração serão transições idempotentes.

O banco terá constraints para quantidade positiva, transições válidas e
unicidade adequada por pedido e produto, além de índices para reservas ativas e
vencidas. `PhysicalStock` e `ReservedStock` continuarão privados no contrato HTTP.

## Consequências

- Cada unidade reservada passa a ter origem, estado e prazo auditáveis.
- Cancelamento, pagamento e expiração devem alterar reserva e inventário juntos.
- Consultas e migrations existentes de pedidos e estoque precisarão ser ajustadas.
- Bloqueios pessimistas e constraints continuam necessários para evitar overselling.
- A mudança exige testes de concorrência com PostgreSQL real.

## Implementação

- `InventoryReservation` persiste pedido, produto, inventário, quantidade,
  criação, expiração, conclusão e o estado da reserva.
- As transições `Active` para `Consumed`, `Released` ou `Expired` são protegidas
  pelo domínio e são idempotentes quando o estado final já foi alcançado.
- Criação de pedido e inclusão de item bloqueiam produto e inventário em ordem de
  `ProductId` e gravam pedido, reserva, contadores e Outbox na mesma transação.
- Aprovação, recusa e cancelamento bloqueiam as reservas ativas e alteram reserva
  e inventário na mesma transação.
- A migration `AddPersistedInventoryReservations` cria constraints, unicidade por
  pedido/produto, índice parcial por expiração ativa e realiza backfill dos
  pedidos pendentes existentes.
- Testes de domínio, aplicação e PostgreSQL cobrem as transições, a atomicidade e
  duas reservas concorrentes disputando o mesmo inventário sem overselling.
