# Contratos de eventos de integração v1

A API persiste somente DTOs de transporte na Outbox. O Worker envolve o payload
em JSON camelCase com `messageId`, `eventType`, `version`, `occurredAt`,
`correlationId` e `payload`, publica no exchange tópico durável
`ecommerce.events` e repete os cinco primeiros campos nas propriedades AMQP.

| Routing key | Payload v1 |
| --- | --- |
| `order.created`, `order.updated`, `order.deleted`, `order.paid`, `payment.failed`, `order.refunded`, `order.cancelled` | pedido, cliente, status, total, vencimento, cancelamento, ocorrência e itens |
| `stock.updated` | produto, estoque disponível, pedido opcional, motivo e ocorrência |
| `email.sent` | identificador da entrega, categoria e ocorrência |

`order.paid` representa `PaymentCompleted` dentro do e-commerce e nasce de
`payment.approved`; `order.cancelled` representa cancelamento explícito ou por
expiração. O exemplo canônico compartilhado com o consumer está em
[`contracts/v1/order-paid.example.json`](contracts/v1/order-paid.example.json).

O `correlationId` recebido no HTTP é persistido junto da Outbox, enviado ao
gateway, devolvido no webhook e preservado até o consumo. Mensagens criadas sem
requisição HTTP usam o próprio `MessageId` como correlação.

O schema v1 é imutável. Campos novos compatíveis são opcionais e devem ser
ignorados por consumers antigos. Mudanças incompatíveis exigem nova versão,
publicação/consumo simultâneos e retirada documentada. O Worker aceita
temporariamente mensagens legadas sem envelope como v1 para drenar a fila.
