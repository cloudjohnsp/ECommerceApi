# ECommerce API

Backend de e-commerce organizado com Clean Architecture.

O cadastro público cria exclusivamente usuários com a role `Customer`. A
alteração de roles exige `Administrator`; operações de perfil e desativação são
limitadas ao próprio usuário ou a administradores, e a troca de senha é restrita
ao próprio usuário.

Clientes consultam, cancelam e pagam somente os próprios pedidos. O identificador
do cliente é obtido do JWT, portanto um `CustomerId` enviado por um cliente não
pode ser usado para operar em nome de outra conta. Administradores mantêm visão
global dos pedidos.

A borda HTTP aplica CORS somente às origens configuradas em
`ApiProtection:AllowedOrigins` e rate limiting por cliente. Há um limite global
e outro mais restritivo para `/api/auth/login`, `/api/auth/register`,
`/api/auth/refresh`, `/api/auth/logout`, confirmação de e-mail e recuperação de
senha.

Novos cadastros precisam confirmar o e-mail antes de autenticar. O registro e
as solicitações de recuperação gravam o token com hash e a entrega de e-mail na
mesma transação via Outbox; o worker SMTP remove o token em texto claro do
payload depois do envio. Os endpoints públicos são `confirm-email`,
`forgot-password` e `reset-password` sob `/api/v1/auth` (com rotas legadas em
`/api/auth`). Em desenvolvimento, o Docker Compose expõe a caixa do Mailpit em
`http://localhost:8025`.

O catálogo é paginado e aceita busca textual, faixa de preço e ordenação:

```http
GET /api/products?search=mouse&categoryId=00000000-0000-0000-0000-000000000000&minPrice=50&maxPrice=500&sortBy=price&descending=true&page=1&pageSize=20
```

`sortBy` aceita `name`, `price` ou `createdAt`, e `pageSize` é limitado a 100.
Leituras individuais de produtos usam cache-aside no Redis por cinco minutos;
consultas individuais e a listagem de categorias, por dez minutos. Criação,
alteração, desativação e mudanças de estoque atualizam ou invalidam as entradas
somente depois do commit. Se o Redis estiver temporariamente indisponível, a
aplicação continua atendendo pelo PostgreSQL; o readiness check sinaliza a
degradação.

Todos os controllers públicos pertencem à versão `1.0`. As rotas explícitas
usam o prefixo `/api/v1` (por exemplo, `GET /api/v1/products`). As rotas
anteriores sob `/api` continuam disponíveis e assumem v1 por compatibilidade;
respostas informam as versões suportadas nos headers de API versioning.

Categorias possuem CRUD próprio em `/api/v1/categories` (ou na rota legada
`/api/categories`). Leituras são públicas; criação, alteração e desativação
lógica exigem a role `Administrator`. O slug é gerado pelo domínio a partir do
nome e possui índice único no PostgreSQL.

Produtos podem opcionalmente receber `categoryId` na criação ou atualização, e
o catálogo pode ser filtrado por esse identificador. A Application só aceita
categorias ativas e existentes; a relação é protegida por chave estrangeira e,
se uma categoria for removida fisicamente em manutenção, o PostgreSQL define o
vínculo do produto como nulo.

Imagens de produto são enviadas por administradores em
`POST /api/v1/products/{productId}/images` como `multipart/form-data`. A API
aceita JPEG, PNG e WebP de até 5 MB, confere a assinatura do arquivo e persiste
somente os metadados no PostgreSQL; o binário fica no Azure Blob Storage. O
Docker Compose fornece Azurite para desenvolvimento local, e
`GET /api/v1/products/{productId}/images` expõe a lista pública.

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

Por padrão, a execução local não altera o schema automaticamente. Gere e
aplique migrations de forma explícita durante o desenvolvimento. No Compose,
`DatabaseInitialization:ApplyMigrationsOnStartup` é habilitado: a API aguarda os
health checks de PostgreSQL, RabbitMQ e Redis e aplica migrations pendentes com
retry limitado antes de começar a atender requisições.

## Integração contínua

O workflow `.github/workflows/ci.yml` executa restore, verificação de formato,
build e os testes da solução em cada pull request e push para `main`. Depois que
essas verificações passam, ele também constrói a imagem do `Dockerfile` sem
publicá-la; publicação e deploy permanecem separados porque exigem a escolha do
registry e do ambiente de destino.

Os testes de persistência incluem cenários de integração com PostgreSQL real via
Testcontainers. Eles aplicam todas as migrations e verificam constraints,
transações e consultas específicas do provedor. É necessário que o Docker esteja
disponível e executar os testes com `RUN_POSTGRES_INTEGRATION_TESTS=true`; sem
essa opção, somente esses cenários são reportados como ignorados. O workflow de
CI habilita a opção e executa os testes contra um container efêmero.

Health checks disponíveis:

- `/api/health/live`: confirma que o processo está ativo, sem consultar dependências;
- `/api/health/ready`: verifica PostgreSQL, Redis, RabbitMQ, gateway de pagamento,
  Azure Blob Storage e SMTP;
- `/api/health`: executa todas as verificações registradas.

## Observabilidade

A API produz logs estruturados em JSON com Serilog, incluindo `CorrelationId`,
`TraceId` e `SpanId`, e adiciona `X-Correlation-ID` a toda resposta. Um
identificador válido recebido nesse header é preservado; caso ele não seja
enviado, a API gera um novo identificador.

OpenTelemetry coleta métricas de ASP.NET Core, `HttpClient` e runtime, além de
traces de requisições e chamadas HTTP de saída. O Prometheus pode coletar as
métricas em `GET /metrics`. Para enviar traces e métricas a um collector OTLP,
configure `Observability:OtlpEndpoint` (por exemplo,
`http://localhost:4317`). O endpoint Prometheus pode ser desativado com
`Observability:EnablePrometheus=false`.

O `docker-compose.yml` também inicia Prometheus em
`http://localhost:9090` e Grafana em `http://localhost:3001`. O datasource e o
dashboard **ECommerce API** são provisionados automaticamente. As credenciais
iniciais do Grafana são `admin`/`admin` e podem ser substituídas pelas variáveis
`GRAFANA_ADMIN_USER` e `GRAFANA_ADMIN_PASSWORD`.

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
