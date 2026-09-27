# ADR 0004: Expiração de pedidos e reservas

- Status: implementado
- Data: 2026-09-27

## Contexto

Pedidos aguardando pagamento não podem reter estoque indefinidamente. Com
reservas persistidas, é necessário encerrar pedidos vencidos e devolver a
quantidade reservada mesmo após reinicializações ou indisponibilidade temporária.

A expiração é uma regra de negócio durável, não um timer mantido em memória.

## Decisão

Pedidos em `PendingPayment` e reservas `Active` terão prazo persistido. Um job
periódico buscará candidatos vencidos em batches e processará cada agregado em
transações curtas.

O processamento bloqueará pedido, reservas e inventários em ordem determinística,
revalidará o estado após o lock, marcará as reservas como `Expired`, reduzirá
`ReservedStock` e cancelará o pedido pelo motivo de expiração.

Eventos `order.cancelled` e `stock.updated` serão gravados no Outbox na mesma
transação. Reexecuções sobre itens já concluídos não produzirão novos efeitos.

## Consequências

- Estoque abandonado volta automaticamente à disponibilidade.
- O prazo deve ser configurável e persistido para não mudar retroativamente.
- Pagamento, cancelamento e expiração disputarão os mesmos locks e serão idempotentes.
- Índices para estados ativos e `ExpiresAt` serão necessários.
- Métricas deverão expor atrasos e falhas do processo de expiração.

## Implementação

- `Order.ExpiresAt` persiste o prazo calculado a partir da configuração no
  momento da criação; reservas do pedido reutilizam o mesmo prazo.
- `OrderExpirationJob`, agendado pelo Hangfire, processa candidatos em batches
  por meio de `IOrderExpirationProcessor`.
- Cada candidato é revalidado dentro de uma transação que bloqueia pedido,
  reservas e inventários em ordem determinística.
- A conclusão marca reservas como `Expired`, devolve o estoque reservado,
  cancela o pedido com o motivo `Expired` e grava `order.cancelled` e
  `stock.updated` no Outbox na mesma transação.
- O índice parcial `ix_orders_pending_expires_at` dá suporte à busca de pedidos
  vencidos, e métricas registram pedidos expirados, atraso e falhas do job.
- Testes unitários e testes de concorrência com PostgreSQL real cobrem
  idempotência e a disputa entre expiração e aprovação de pagamento.
