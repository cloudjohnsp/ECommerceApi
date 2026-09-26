# ECommerce API

Backend de e-commerce organizado com Clean Architecture.

As fronteiras, garantias transacionais e decisões que diferem do esboço inicial
estão registradas em [`docs/architecture.md`](docs/architecture.md).

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

Refresh tokens são persistidos somente como hash e rotacionados sob transação
com bloqueio pessimista no PostgreSQL. Se um token já revogado for reutilizado,
todos os tokens ainda ativos do usuário são revogados para interromper a cadeia
potencialmente comprometida.
O logout usa o mesmo bloqueio transacional e encerra todas as sessões do usuário,
inclusive quando concorre com uma solicitação de rotação.
Tokens de confirmação e redefinição também são consumidos sob bloqueio pessimista;
solicitações concorrentes de recuperação são serializadas pelo usuário e deixam
somente o token mais recente utilizável.

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

Administradores consultam `GET /api/v1/admin/dashboard` para obter totais de
usuários e produtos ativos, pedidos e pagamentos por estado e receita de
pagamentos aprovados. As agregações são executadas diretamente no PostgreSQL e
o endpoint não fica disponível para usuários da role `Customer`. O recurso pode
ser liberado ou retirado sem novo deploy por meio de
`FeatureFlags:AdminDashboard`; quando desabilitado, a API responde `404` sem
consultar o banco.

O relatório de vendas por período fica em
`GET /api/v1/admin/reports/sales?fromUtc=2026-01-01T00:00:00Z&toUtc=2026-02-01T00:00:00Z&topProducts=10`.
Ele retorna receita bruta, reembolsos, receita líquida, pedidos e pagamentos por
estado, série diária e os produtos mais vendidos. O intervalo usa limite inicial
inclusivo e final exclusivo, aceita no máximo 366 dias e permite de 1 a 50
produtos no ranking. As agregações e a limitação do ranking são executadas no
PostgreSQL; somente a combinação das séries diárias ocorre na aplicação.

Cada cadastro, alteração de perfil, troca de senha ou role, confirmação de
e-mail e desativação gera uma entrada de auditoria na mesma transação da
mutação. O histórico paginado fica disponível ao próprio usuário e a
administradores em `GET /api/v1/user/{userId}/history`. As diferenças de perfil
e role são armazenadas como `jsonb`; senhas, hashes e tokens nunca são incluídos
na trilha.

Enquanto o pedido está pendente, o cliente pode acrescentar um produto por
`POST /api/v1/orders/{orderId}/items`. A operação bloqueia pedido e produto,
reserva o estoque e grava `order.updated` e `stock.updated` na outbox na mesma
transação. Pedidos pagos, cancelados ou reembolsados não aceitam novos itens.

A consulta paginada fica em `GET /api/v1/orders/search` e aceita `status`,
`createdFromUtc`, `createdToUtc`, `sortBy`, `descending`, `page` e `pageSize`.
`sortBy` aceita `createdAt`, `status` ou `total`, e `pageSize` é limitado a 100.
Clientes recebem somente os próprios pedidos, independentemente dos parâmetros
enviados; administradores podem consultar o conjunto global. O `GET /orders`
original permanece disponível para compatibilidade.

O `PUT /api/v1/orders/{orderId}` é uma operação administrativa de reconciliação,
não um atalho para aprovar pagamentos. A transição para `Paid` só ocorre quando
já existe um pagamento aprovado; nesse caso, a reserva é consumida e o evento
`order.paid` é persistido atomicamente. O fluxo normal continua sendo dirigido
pelo webhook assinado do gateway.

## Projetos

- `ECommerce.Api`: endpoints HTTP e composição da aplicação.
- `ECommerce.Application`: casos de uso e contratos de saída.
- `ECommerce.Domain`: entidades e regras de negócio puras.
- `ECommerce.Persistence`: implementações de acesso a dados.
- `ECommerce.Infrastructure`: integrações externas.
- `ECommerce.Shared`: tipos reutilizáveis.
- `ECommerce.Worker`: consumidor RabbitMQ, inbox idempotente, notificações e
  geração simulada de notas fiscais.

## Executar

Nenhum segredo de execução é versionado em `appsettings.json`. Para executar a
API diretamente, inicialize o armazenamento local de segredos e informe ao
menos a conexão do PostgreSQL, a chave JWT, as credenciais do RabbitMQ e o
segredo compartilhado com o gateway:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=ecommerce;Username=postgres;Password=<password>" --project src/ECommerce.Api
dotnet user-secrets set "Jwt:SecretKey" "<at-least-32-random-characters>" --project src/ECommerce.Api
dotnet user-secrets set "RabbitMq:UserName" "ecommerce" --project src/ECommerce.Api
dotnet user-secrets set "RabbitMq:Password" "<password>" --project src/ECommerce.Api
dotnet user-secrets set "PaymentGateway:WebhookSecret" "<shared-secret>" --project src/ECommerce.Api
dotnet run --project src/ECommerce.Api
```

O Worker usa seu próprio schema `worker` no PostgreSQL e aplica as migrations
dele ao iniciar. Para executá-lo fora do Compose, configure os segredos e inicie
o processo separadamente:

```powershell
dotnet user-secrets set "ConnectionStrings:WorkerDatabase" "Host=localhost;Port=5432;Database=ecommerce;Username=postgres;Password=<password>" --project src/ECommerce.Worker
dotnet user-secrets set "RabbitMq:UserName" "ecommerce" --project src/ECommerce.Worker
dotnet user-secrets set "RabbitMq:Password" "<password>" --project src/ECommerce.Worker
dotnet run --project src/ECommerce.Worker
```

O consumidor recebe `order.created`, `order.updated`, `order.paid`,
`payment.failed`, `order.refunded`, `order.cancelled` e `stock.updated`. A tabela
de inbox impede efeitos duplicados; projeções de pedido e disponibilidade, nota
fiscal e notificação são persistidas atomicamente antes do ACK. Eventos de
estoque atrasados são registrados na inbox sem regredir a projeção mais recente.
Falhas transitórias são reenfileiradas e, após o limite da fila quorum, seguem
para `ecommerce.worker.orders.dead`. E-mails usam uma segunda outbox com lease e
backoff exponencial, visível no Mailpit durante o desenvolvimento.
O processo também expõe `/health/live`, `/health/ready` e `/metrics` na porta
`8082` do host quando executado pelo Compose. O Prometheus coleta métricas de
eventos consumidos, duplicados, falhos e notificações enviadas ou reprocessadas.

`UserSecretsId` é usado somente no ambiente `Development`. Em ambientes
publicados, forneça os mesmos valores por variáveis de ambiente ou pelo cofre de
segredos da plataforma; a validação de opções interrompe a inicialização quando
uma configuração obrigatória estiver ausente.

Para executar todo o ambiente Docker, inicialize o arquivo local `.env` e então
suba os serviços:

```powershell
.\scripts\initialize-dev-env.ps1
docker compose up -d --build
```

O script preenche somente os segredos obrigatórios que estiverem ausentes ou em
branco, preserva os valores já configurados e não imprime os segredos. O arquivo
`.env` é ignorado pelo Git. O valor de
`PAYMENT_GATEWAY_WEBHOOK_SECRET` deve ser o mesmo configurado no projeto
`ECommercePayment`.

O Compose publica somente HTTP em `http://localhost:8080` e desabilita o
middleware de redirecionamento HTTPS, evitando redirecionamentos para uma porta
inexistente. TLS deve ser terminado pelo ingress ou reverse proxy nos ambientes
publicados. O perfil local `https` do `launchSettings.json` habilita o
redirecionamento porque também inicia `https://localhost:5001`.

Se a política de execução do PowerShell bloquear scripts locais, execute apenas
para este processo:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\initialize-dev-env.ps1
```

No Compose, o seed idempotente cria três categorias e três produtos de exemplo.
Para também criar um administrador confirmado, preencha `SEED_ADMIN_EMAIL` e
`SEED_ADMIN_PASSWORD` no `.env`; a senha deve ter ao menos 12 caracteres e nunca
é gravada no repositório. O seed usa um advisory lock do PostgreSQL para evitar
duplicação quando mais de uma instância inicia ao mesmo tempo. Em produção,
`DatabaseSeed:Enabled` permanece desabilitado por padrão.

Por padrão, a execução local não altera o schema automaticamente. Gere e
aplique migrations de forma explícita durante o desenvolvimento. No Compose,
`DatabaseInitialization:ApplyMigrationsOnStartup` é habilitado: a API aguarda os
health checks de PostgreSQL, RabbitMQ e Redis e aplica migrations pendentes com
retry limitado antes de começar a atender requisições.

Comandos do EF que apenas trabalham com o modelo usam uma factory de design-time
e não precisam iniciar a API. Para comandos que conectam ao banco, como
`database update`, forneça a conexão somente no processo atual:

```powershell
$env:ECOMMERCE_DESIGN_TIME_CONNECTION_STRING = "Host=localhost;Port=5432;Database=ecommerce;Username=postgres;Password=<password>"
dotnet ef database update --project src/ECommerce.Persistence --startup-project src/ECommerce.Api
Remove-Item Env:ECOMMERCE_DESIGN_TIME_CONNECTION_STRING
```

## Integração contínua

O workflow `.github/workflows/ci.yml` executa restore, verificação de formato,
build e os testes da solução em cada pull request e push para `main`. Depois que
essas verificações passam, ele também constrói a imagem do `Dockerfile` sem
publicá-la.

Tags no formato `v*.*.*` acionam `.github/workflows/release.yml`: o candidato é
novamente validado, as imagens da API e do Worker são publicadas no Azure
Container Registry com tags imutáveis baseadas no commit, a API é implantada no
Azure App Service e o Worker em Azure Container Apps. Ambos são verificados pelo
pipeline.
O login no Azure usa OIDC, sem credencial de longa duração. A preparação dos
recursos, das permissões e do GitHub Environment `production` está documentada
em [`docs/azure-deployment.md`](docs/azure-deployment.md).

Os testes de persistência incluem cenários de integração com PostgreSQL real via
Testcontainers. Eles aplicam todas as migrations e verificam constraints,
transações e consultas específicas do provedor. É necessário que o Docker esteja
disponível e executar os testes com `RUN_POSTGRES_INTEGRATION_TESTS=true`; sem
essa opção, somente esses cenários são reportados como ignorados. O workflow de
CI habilita a opção e executa os testes contra um container efêmero.

O script `scripts/verify-unit-coverage.ps1` mede separadamente as assemblies de
Domain e Application e exige cobertura de linhas superior a 80% em cada uma. O
mesmo gate é executado pelo CI para impedir regressões na cobertura das regras de
negócio e dos casos de uso.

Health checks disponíveis:

- `/api/health/live`: confirma que o processo está ativo, sem consultar dependências;
- `/api/health/ready`: verifica PostgreSQL, Redis, RabbitMQ, gateway de pagamento,
  Azure Blob Storage e SMTP;
- `/api/health`: executa todas as verificações registradas.

## Coleção Postman

Importe `postman/ECommerceApi.postman_collection.json` e
`postman/Local.postman_environment.json`. Informe `email` e `password` apenas no
ambiente local do Postman. A requisição de login salva automaticamente access e
refresh tokens, enquanto criação de categoria, produto e pedido atualiza os IDs
usados nas requisições seguintes. Após cadastrar um cliente, copie do Mailpit o
token para `confirmationToken` e execute a confirmação antes do login. A coleção
não contém credenciais versionadas.

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

O Prometheus também coleta o Worker em `ecommerce-worker:8080`; traces e métricas
dos dois processos podem ser enviados ao mesmo collector configurando
`Observability:OtlpEndpoint`.

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
gateway. Além da assinatura, a API rejeita eventos desconhecidos e exige que o
campo `data.status` corresponda à transição indicada por `event` antes de acessar
o banco.

Um cliente pode solicitar o reembolso integral do próprio pedido pago, e um
administrador pode reembolsar qualquer pedido, por meio de:

```http
POST /api/v1/payments/{orderId}/refund
Content-Type: application/json

{"reason": "customer_request"}
```

A API usa uma chave idempotente ao chamar o gateway. Após a confirmação, pedido
e pagamento passam para `Refunded` e o estoque dos itens é restaurado na mesma
transação local. O webhook `payment.refunded` aplica a mesma transição quando o
reembolso é iniciado diretamente no gateway.

Ao iniciar um pagamento, a API salva o pagamento e uma intenção
`PaymentCreationRequested` na outbox dentro da mesma transação. A tentativa é
executada imediatamente e um serviço em segundo plano reprocessa intenções que
continuarem pendentes, usando o `Payment.Id` como chave de idempotência no
gateway.

Os processadores de pagamentos, e-mails e eventos de integração são jobs
recorrentes do Hangfire. O agendamento, o tamanho do lote e a quantidade de
workers são configurados em `OutboxProcessor`; o estado do scheduler fica no
schema `hangfire` do PostgreSQL. Cada job impede execuções concorrentes da mesma
tarefa, e falhas não tratadas ficam sob a política de retry durável do Hangfire.
O dashboard técnico do Hangfire não é exposto pela API.

Eventos de pedido e estoque armazenados na outbox são publicados no exchange
durável `ecommerce.events` do RabbitMQ com routing keys como `order.created` e
`stock.updated`. O evento de estoque informa `ProductId`, `AvailableStock`,
`OrderId` opcional, motivo e instante da mudança; o estoque físico e a reserva
permanecem encapsulados no domínio. A
publicação usa confirmação do broker e entrega persistente. Como o processamento
é *at-least-once*, consumidores devem deduplicar pelo `MessageId`, que corresponde
ao identificador da mensagem na outbox.
