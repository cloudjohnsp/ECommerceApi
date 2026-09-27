# ADR 0002: Reservas de estoque persistidas

- Status: aceito; implementação pendente
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
