# ECommerce API

Backend de e-commerce organizado com Clean Architecture.

## Projetos

- `ECommerce.Api`: endpoints HTTP e composição da aplicação.
- `ECommerce.Application`: casos de uso e contratos de saída.
- `ECommerce.Domain`: entidades e regras de negócio puras.
- `ECommerce.Persistence`: implementações de acesso a dados.
- `ECommerce.Infrastructure`: integrações externas.
- `ECommerce.Shared`: tipos reutilizáveis.

## Executar

```powershell
dotnet run --project src/ECommerce.Api
```

O health check fica disponível em `/api/health`.

## Gateway de pagamento

Execute o projeto `ECommercePayment` em `http://localhost:5002`. Depois de criar
um pedido, inicie o pagamento com:

```http
POST /api/payments
Content-Type: application/json

{
  "orderId": "00000000-0000-0000-0000-000000000000",
  "currency": "BRL"
}
```

O gateway retorna um identificador `pay_...`. A aprovação ou recusa feita no
simulador envia um webhook assinado para `POST /api/webhooks/payments`. O segredo
`PaymentGateway:WebhookSecret` deve ser igual ao `WEBHOOK_SECRET` configurado no
gateway.

Ao iniciar um pagamento, a API salva o pagamento e uma intenção
`PaymentCreationRequested` na outbox dentro da mesma transação. A tentativa é
executada imediatamente e um serviço em segundo plano reprocessa intenções que
continuarem pendentes, usando o `Payment.Id` como chave de idempotência no
gateway. O intervalo e o tamanho do lote são configurados em
`OutboxProcessor` no `appsettings.json`.

Eventos de pedido armazenados na outbox são publicados no exchange durável
`ecommerce.events` do RabbitMQ com uma routing key como `order.created`. A
publicação usa confirmação do broker e entrega persistente. Como o processamento
é *at-least-once*, consumidores devem deduplicar pelo `MessageId`, que corresponde
ao identificador da mensagem na outbox.
