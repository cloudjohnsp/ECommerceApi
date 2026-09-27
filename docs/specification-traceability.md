# Rastreabilidade da especificação

Este documento relaciona a especificação funcional e técnica do projeto às
evidências mantidas no repositório. Ele não substitui os testes nem a descrição
das decisões em [`architecture.md`](architecture.md); seu objetivo é tornar a
auditoria do estado final repetível.

## Funcionalidades

| Requisito | Implementação | Evidência automatizada |
| --- | --- | --- |
| JWT, refresh token, rotação, logout e roles | `AuthController`, handlers de autenticação, `JwtTokenService` e repositório de refresh tokens | `AuthControllerTests`, `LoginHandlerTests`, `RefreshTokenHandlerTests`, `LogoutHandlerTests` e integrações PostgreSQL |
| Confirmação de e-mail e recuperação de senha | Tokens de uso único com hash, Outbox e entrega SMTP | `UserActionHandlersTests`, `UserEmailOutboxProcessorTests`, `UserActionTokenRepositoryTests` e `UserEmailOutboxJobTests` |
| Perfil, senha, role, desativação lógica e auditoria de usuário | Casos de uso em `Application/Users`, regras em `Domain/User` e histórico `jsonb` | testes de handlers de usuário, `UserTests`, `UserAuditRepositoryTests` e testes de autorização da API |
| Produtos, categorias, estoque e imagens | CRUDs, catálogo filtrável, agregado `Inventory`, metadados PostgreSQL e blobs via `IProductImageStorage` | `ProductHandlersTests`, `CategoryHandlersTests`, testes de domínio, repositórios e imagem |
| Pedidos e itens | Criação, inclusão de item, consulta, reconciliação e cancelamento com reserva transacional | `OrderHandlersTests`, `OrderTests`, `OrderRepositoryTests` e integrações PostgreSQL |
| Expiração de pedidos | Prazo persistido/configurável, job Hangfire em batches, cancelamento idempotente, liberação de reservas, Outbox e métricas | `OrderExpirationProcessorTests`, `OrderExpirationJobTests` e corridas reais no PostgreSQL |
| Pagamento e reembolso | Tentativas 1:N por pedido, chave de idempotência, uma tentativa pendente por vez, gateway HTTP, webhook HMAC, Outbox de criação e restauração de estoque | `PaymentTests`, `PaymentHandlersTests`, `PaymentCreationProcessorTests`, `PaymentGatewayClientTests`, `PaymentWebhookSignatureVerifierTests` e integrações PostgreSQL de retry/concorrência |
| Administração | Dashboard, relatório de vendas, estatísticas e feature flag | testes de handlers administrativos, `AdminReportingRepositoryTests` e `AdminControllerTests` |

## Arquitetura e padrões

| Requisito | Evidência |
| --- | --- |
| Clean Architecture | Projetos separados em `Api`, `Application`, `Domain`, `Persistence`, `Infrastructure` e `Shared`; `ProjectDependencyTests` protege a direção das referências. O Worker canônico está no repositório independente `ECommerceWorker` e não referencia assemblies da API |
| CQRS e MediatR | Commands, queries e handlers organizados por feature em `ECommerce.Application` |
| FluentValidation | Validators por feature e `ValidationBehaviour` registrado no pipeline do MediatR |
| Result Pattern | Primitivas em `ECommerce.Shared/Results` usadas pelo domínio e pelos casos de uso para falhas esperadas |
| Repository e Unit of Work | Contratos em `Application/Abstractions/Persistence` e implementações EF Core em `ECommerce.Persistence` |
| Specification Pattern | `ProductCatalogSpecification`, `OrderSearchSpecification` e aplicação pelo repositório |
| Dependency Injection e Options Pattern | Arquivos `DependencyInjection.cs` por camada e opções validadas na inicialização |
| Mapster | Mapeamentos de DTOs registrados pela Application |

## Dados, consistência e integrações

| Requisito | Evidência |
| --- | --- |
| PostgreSQL, EF Core e migrations | `AppDbContext`, configurações Fluent API e migrations versionadas em `ECommerce.Persistence` |
| Seed | `DatabaseSeeder` idempotente e protegido por advisory lock |
| Índices e constraints | Configurações EF, `RelationalConstraintTests` e testes PostgreSQL de violações reais, incluindo reserva acima do estoque e produto duplicado no pedido |
| Transações | `IUnitOfWork`, bloqueios pessimistas e ordem global de locks documentada em `architecture.md` |
| Redis | Cache-aside resiliente para produtos e categorias, invalidação após commit e health check |
| RabbitMQ | API grava a Outbox atomicamente; Worker independente faz claim com `SKIP LOCKED`, lease, retry e publicação com confirmação, além de fila quorum, inbox e dead-letter |
| Eventos | Ciclo do pedido, falha/reembolso de pagamento, estoque e confirmação sanitizada de e-mail |
| Background jobs | Hangfire para pagamento, e-mail e expiração na API; publicação de eventos, consumidores e outboxes duráveis no Worker |
| Armazenamento de imagens | Azure Blob SDK com Azurite local; somente metadados são persistidos no PostgreSQL |

## Operação e segurança

| Requisito | Evidência |
| --- | --- |
| Docker | Imagem não-root da API e imagem independente do Worker; Compose com PostgreSQL, RabbitMQ, Redis, Azurite, Mailpit, Prometheus e Grafana e portas locais somente no loopback |
| Observabilidade | Serilog JSON, correlation id, OpenTelemetry, Prometheus, Grafana, métricas do Worker e health checks |
| OpenAPI | Documento OpenAPI e Swagger UI configurados pela API com esquema Bearer |
| Versionamento | Rotas `/api/v1`, compatibilidade das rotas legadas e headers de versões suportadas |
| Paginação, filtros, ordenação e busca | Catálogo, pedidos, histórico de usuário e relatório administrativo |
| Rate limiting e CORS | Políticas configuráveis e testes HTTP com `WebApplicationFactory` |
| CSRF | Não aplicável: a autenticação usa Bearer header e não cookies |
| SQL injection | Consultas EF parametrizadas e SQL explícito interpolado pelo provider |
| Segredos e callbacks | Valores operacionais ausentes dos arquivos versionados, `.env` ignorado, opções validadas na inicialização, callbacks restritos a origens HTTP(S) autorizadas e redirects desabilitados nos dois sentidos |
| CI | Restore, formato, build, testes, Testcontainers, cobertura e imagem da API neste repositório; o Worker possui workflow próprio no repositório independente |
| Cobertura | `scripts/verify-unit-coverage.ps1` exige mais de 80% em Domain e Application |
| Testes | xUnit, FluentAssertions, Moq, integração PostgreSQL/Testcontainers e HTTP com `WebApplicationFactory` |

## Implantação Azure

O repositório contém um workflow de release e um guia para uma possível
implantação futura da API em Azure App Service e ACR. Nenhuma
conta, assinatura ou credencial Azure é versionada. O ambiente GitHub
`production` não está configurado por decisão do mantenedor, portanto o fluxo
não efetua implantação nem cria custos atualmente.

No desenvolvimento, `Azure.Storage.Blobs` aponta para o Azurite. A conta
`devstoreaccount1`, sua chave conhecida e a porta `10000` pertencem ao emulador
local e não autenticam em uma assinatura Azure.

## Gates de conclusão

Uma alteração só preserva esta rastreabilidade quando passam:

```powershell
dotnet format ECommerceApi.slnx --verify-no-changes
dotnet build ECommerceApi.slnx --configuration Release
dotnet test ECommerceApi.slnx --configuration Release
.\scripts\verify-unit-coverage.ps1 -Configuration Release
```

Os testes dependentes de PostgreSQL real devem ser executados com Docker e
`RUN_POSTGRES_INTEGRATION_TESTS=true`; o CI aplica essa configuração.
