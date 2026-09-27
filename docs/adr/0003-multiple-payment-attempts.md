# ADR 0003: Múltiplas tentativas de pagamento por pedido

- Status: aceito; implementado
- Data: 2026-09-27

## Contexto

O modelo atual associa um único `Payment` a cada `Order` e possui unicidade por
`OrderId`. Uma recusa encerra o fluxo, embora um cliente deva poder iniciar outra
tentativa enquanto o pedido e sua reserva ainda forem válidos.

Uma tentativa de pagamento representa uma interação financeira independente e
não deve ser confundida com o estado definitivo do pedido.

## Decisão

A relação será alterada de `Order 1:1 Payment` para `Order 1:N Payments`. Cada
tentativa terá identificador, chave de idempotência, valor, moeda, estado e
identificador externo próprios.

Somente uma tentativa não terminal poderá existir por pedido. Uma tentativa
recusada será marcada como falha, mas o pedido permanecerá `PendingPayment` e a
reserva ativa até aprovação, cancelamento ou expiração.

Uma aprovação válida concluirá o pedido e consumirá suas reservas de forma
atômica. Webhooks e comandos repetidos continuarão idempotentes por tentativa.

## Consequências

- O índice único atual de `payments.order_id` será removido por migration.
- Queries, DTOs, handlers e endpoints deixarão de presumir pagamento singular.
- Regras impedirão tentativas concorrentes incompatíveis e aprovação duplicada.
- Histórico de recusas será preservado para auditoria e suporte.
- Testes deverão cobrir retry, concorrência e webhooks fora de ordem.

## Implementação

- `Payment` passou a persistir `IdempotencyKey`; a migration
  `SupportMultiplePaymentAttempts` preserva os registros existentes com chaves
  `legacy:<PaymentId>` e remove a unicidade global de `order_id`.
- O banco aplica unicidade por pedido/chave e índices parciais que permitem no
  máximo uma tentativa `Pending` e uma tentativa `Paid` por pedido.
- A criação bloqueia o pedido, devolve a mesma tentativa para a mesma chave e só
  aceita uma chave nova quando não há outra tentativa pendente.
- Uma recusa marca somente a tentativa como `Failed`; pedido e reservas
  permanecem ativos. A aprovação consome as reservas e conclui o pedido na mesma
  transação já protegida pelos locks existentes.
- A consulta por pedido retorna o histórico completo de tentativas, enquanto o
  reembolso seleciona especificamente a tentativa paga ou já reembolsada.
- Webhooks repetidos ou atrasados para tentativas terminais são idempotentes e
  não revertem o estado do pedido.
- Testes de domínio, aplicação e PostgreSQL cobrem idempotência, retry,
  concorrência, histórico e eventos fora de ordem.
