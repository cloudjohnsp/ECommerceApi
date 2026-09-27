# ADR 0003: Múltiplas tentativas de pagamento por pedido

- Status: aceito; implementação pendente
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
