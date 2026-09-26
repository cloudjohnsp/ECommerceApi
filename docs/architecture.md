# Arquitetura e decisões

## Dependências

```text
ECommerce.Api
    ├── ECommerce.Application
    ├── ECommerce.Infrastructure
    └── ECommerce.Persistence

ECommerce.Application
    ├── ECommerce.Domain
    └── ECommerce.Shared

ECommerce.Infrastructure ──► contratos da Application
ECommerce.Persistence    ──► contratos da Application
ECommerce.Worker         ──► ECommerce.Shared
```

O domínio não referencia ASP.NET Core, EF Core, RabbitMQ, Redis ou serviços
externos. Application descreve casos de uso e portas; Persistence e
Infrastructure implementam essas portas. O Worker possui armazenamento próprio
no schema PostgreSQL `worker` e compartilha somente o contrato serializado dos
eventos.

## Validação

A validação não é repetida igualmente em todas as camadas:

1. Controllers validam apenas aspectos HTTP, identidade e autorização.
2. FluentValidation rejeita formatos e campos obrigatórios antes do handler.
   Quando o contrato retorna `Result` ou `Result<T>`, cada mensagem distinta é
   devolvida como erro do próprio resultado, sem usar exceção como fluxo esperado.
3. Entidades protegem invariantes e transições de estado com `Result`.
4. Configurações Fluent API definem tipos, relações, índices e constraints de
   persistência; elas não substituem regras de negócio.
5. PostgreSQL é a última barreira para unicidade, integridade referencial e
   concorrência.

Falhas esperadas retornam `Result`; exceções ficam reservadas para condições
inesperadas e são convertidas em `ProblemDetails` sanitizado pelo middleware da
API. Respostas de erro incluem `traceId` e `correlationId` para localizar os logs
correspondentes sem revelar detalhes internos da exceção.

## Pedidos, estoque e pagamentos

Criar um pedido ou adicionar um item bloqueia os produtos envolvidos, reserva
estoque e persiste pedido e evento de outbox na mesma transação. Cancelar um
pedido pendente libera a reserva.

Cada criação de inventário e transição relevante de reserva, consumo, liberação,
restauração ou ajuste também grava `stock.updated` na mesma transação. O contrato
público contém somente `ProductId`, `AvailableStock`, o `OrderId` opcional, o
motivo e o instante da alteração. `Stock` e `ReservedStock` continuam detalhes
privados do agregado de inventário.

O estoque não é decrementado de forma assíncrona pelo Worker. Essa decisão evita
venda acima da disponibilidade: aprovação do webhook bloqueia pagamento, pedido
e produtos e, atomicamente, confirma o pagamento, marca o pedido como pago,
consome as reservas e grava `order.paid`. Recusa libera as reservas; reembolso
restaura estoque físico.

O endpoint administrativo de atualização não aprova pagamentos. Ele somente
reconcilia um pedido pendente quando já existe `PaymentStatus.Paid`, aplicando as
mesmas alterações de estoque e outbox em uma transação.

## Outbox do gateway

A criação local de `Payment` e a intenção `PaymentCreationRequested` usam o
mesmo identificador e a mesma transação. A chamada HTTP ao gateway ocorre apenas
depois do commit. Se ela falhar, a intenção permanece pendente para o Hangfire.
Se o gateway responder e o processo cair antes do segundo commit, a mesma chave
de idempotência é reutilizada no retry.

Depois da chamada HTTP, o processador recarrega e bloqueia primeiro o pagamento
e depois a intenção antes de registrar o identificador externo. Ele nunca salva
a instância lida antes da chamada externa; assim, um webhook ou retry concorrente
que já tenha avançado o pagamento não pode ser sobrescrito por um estado
`Pending` obsoleto.

O gateway é a autoridade de `ExternalPaymentId`; o e-commerce mantém seu próprio
`Payment.Id` e nunca mantém uma transação PostgreSQL aberta durante uma chamada
externa.
Webhooks são autenticados por HMAC e o par `event`/`data.status` é validado antes
de abrir a transação, evitando que um payload contraditório aplique uma transição
financeira diferente da informada pelo gateway.

Operações que precisam alterar pagamento, pedido e estoque adquirem os bloqueios
sempre na ordem `Payment → Order → Product`, com produtos ordenados pelo
identificador. A reconciliação administrativa, o webhook e o reembolso seguem a
mesma ordem para impedir ciclos de espera e deadlocks entre esses fluxos.

## Eventos e Worker

Eventos de integração são publicados cronologicamente a partir da outbox. O lote
para no primeiro erro para preservar causalidade. A publicação é *at least once*:
uma queda depois do publish e antes de marcar a outbox pode gerar redelivery.
As routing keys incluem os eventos de ciclo de vida do pedido, `stock.updated` e
`email.sent`.

O Worker usa:

- fila quorum durável e ACK manual;
- atraso antes de retry e limite de entregas;
- dead-letter exchange e fila para mensagens inválidas ou esgotadas;
- inbox com `MessageId` único;
- projeção local do pedido;
- projeção local de `AvailableStock` alimentada por `stock.updated`;
- projeção sanitizada das entregas alimentada por `email.sent`;
- nota fiscal simulada única por pedido pago;
- outbox de notificações, com lease e backoff exponencial;
- outbox de integração para publicar a confirmação do SMTP sem dual-write.

Inbox, projeções, nota e intenção de notificação são confirmadas antes do ACK. Ao
enviar uma notificação, o Worker marca a entrega e cria `email.sent` na mesma
transação local; outra etapa publica o evento com confirmação, lease e retry. O
SMTP continua sendo *at least once*: uma falha depois da aceitação pelo servidor
e antes de `SentAt` pode repetir a mensagem, limitação documentada do protocolo.

## Segurança

- JWT é validado na borda e refresh tokens são armazenados com hash e rotacionados.
- A rotação abre uma transação e bloqueia o token por hash com `FOR UPDATE`, de
  modo que duas requisições concorrentes não possam criar duas cadeias válidas.
- Reutilizar um refresh token revogado invalida a família ativa do usuário,
  inclusive um token emitido por uma rotação concorrente que terminou primeiro.
- Logout bloqueia o token apresentado e revoga todos os refresh tokens ativos do
  usuário; assim, logout concorrente com rotação não deixa uma sessão residual.
- Troca ou redefinição de senha, alteração de role e desativação também revogam
  todos os refresh tokens na mesma unidade de trabalho da mutação e auditoria.
- A troca autenticada exige a senha atual; a recuperação sem essa credencial
  exige o token de uso único enviado por e-mail. Ambos usam a mesma política de
  complexidade para a nova senha.
- Confirmação de e-mail e redefinição de senha bloqueiam o token de uso único com
  `FOR UPDATE`; somente uma requisição concorrente pode consumi-lo.
- A solicitação de redefinição bloqueia o usuário por e-mail antes de invalidar o
  token anterior e gravar token + e-mail na outbox, deixando apenas um token
  utilizável mesmo quando duas solicitações chegam juntas.
- Quando um e-mail de conta é aceito pelo SMTP, a intenção original é concluída e
  um `email.sent` sanitizado é gravado atomicamente para publicação posterior.
  Destinatário, nome, conteúdo e token não fazem parte do evento de integração.
- Clientes recebem escopo pelo claim de identidade; identificadores enviados no
  corpo não permitem operar em nome de outro usuário.
- CORS aceita somente origens configuradas e rate limiting protege globalmente e
  com limite mais restritivo os endpoints de autenticação.
- Em implantação atrás de um ingress isolado, o processamento opt-in de um único
  salto de `X-Forwarded-For` e `X-Forwarded-Proto` preserva o cliente usado pelo
  rate limiting e o esquema HTTPS. A opção permanece desabilitada por padrão
  para impedir spoofing quando a API recebe tráfego direto.
- Todas as respostas, inclusive falhas, desabilitam MIME sniffing e framing e
  restringem o envio de referrer, câmera, geolocalização e microfone por headers.
- A API usa bearer tokens em headers, não cookies de autenticação; portanto CSRF
  não é aplicável ao modelo atual.
- EF Core parametriza consultas. SQL explícito usa interpolação parametrizada.
- Segredos não são versionados e são validados na inicialização.

## Operação

A API e o Worker possuem logs JSON, OpenTelemetry, métricas Prometheus e health
checks. O Compose sobe PostgreSQL, Redis, RabbitMQ, Azurite, Mailpit, API, Worker,
Prometheus e Grafana. O release publica imagens imutáveis: API no Azure App
Service e Worker no Azure Container Apps.

O CI executa restore, formatação, build, testes, integrações PostgreSQL com
Testcontainers, gate de cobertura superior a 80% para Domain/Application e build
das duas imagens.
