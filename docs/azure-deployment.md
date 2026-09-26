# Deploy da API e do Worker no Azure

O workflow `.github/workflows/release.yml` valida a solução e publica imagens
imutáveis da API e do Worker no Azure Container Registry (ACR). A API é
implantada em Azure App Service Linux e validada por `/api/health/live`; o Worker
é implantado em Azure Container Apps e sua revisão precisa ficar pronta. O fluxo
é executado para tags semânticas como `v1.0.0` ou manualmente pelo GitHub Actions.

## Recursos necessários

- Um Azure Container Registry.
- Um Azure App Service Linux configurado para container único.
- Um Azure Container App sem ingress para o Worker, com pelo menos uma réplica.
- PostgreSQL, Redis, RabbitMQ, SMTP e armazenamento de blobs alcançáveis pelo
  App Service.
- Uma identidade do Microsoft Entra usada pelo GitHub via OIDC.

O App Service deve conseguir baixar imagens privadas do ACR. Prefira habilitar
sua identidade gerenciada, conceder a ela `AcrPull` no registry e configurar o
App Service para autenticar no ACR com essa identidade. Faça o mesmo com a
identidade gerenciada do Container App. A identidade federada do GitHub precisa
de `AcrPush` no registry e permissão para atualizar ambos os serviços.
Restrinja ambas as atribuições ao menor escopo possível.

## Ambiente `production` do GitHub

Crie um GitHub Environment chamado `production`. Proteções de aprovação podem
ser adicionadas nesse ambiente sem mudar o workflow.

Cadastre estes secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Eles identificam a credencial federada usada por `azure/login`; nenhum segredo
de longa duração do Azure é necessário.

Cadastre estas variables:

- `AZURE_CONTAINER_REGISTRY_NAME`: nome do ACR, sem `.azurecr.io`.
- `AZURE_RESOURCE_GROUP`: resource group do App Service.
- `AZURE_WEBAPP_NAME`: nome do App Service.
- `AZURE_WORKER_CONTAINER_APP_NAME`: nome do Container App que executa o Worker.

A credencial federada deve confiar no subject do environment:
`repo:<organization>/<repository>:environment:production`.

## Configuração da aplicação

Configure os valores de runtime no App Service, preferencialmente por referências
ao Azure Key Vault. Os nomes usam `__` para representar as seções do .NET:

- `ConnectionStrings__DefaultConnection`
- `Jwt__SecretKey`
- `RabbitMq__HostName`, `RabbitMq__UserName` e `RabbitMq__Password`
- `Redis__Configuration`
- `PaymentGateway__BaseUrl`, `PaymentGateway__CallbackUrl` e
  `PaymentGateway__WebhookSecret`
- `ProductImageStorage__ConnectionString`
- configurações SMTP sob `Email__*`

Defina também `ASPNETCORE_ENVIRONMENT=Production`,
`DatabaseInitialization__ApplyMigrationsOnStartup=true` e a origem permitida em
`ApiProtection__AllowedOrigins__0`. Como a imagem atende na porta 8080, configure
`WEBSITES_PORT=8080` no App Service. O segredo do webhook deve ser igual ao
configurado no gateway de pagamentos.

Defina `ApiProtection__UseHttpsRedirection=false`: o App Service termina TLS no
front-end e encaminha HTTP para o contêiner. A exigência de HTTPS deve permanecer
habilitada na configuração **HTTPS Only** do próprio App Service.

O seed é destinado a ambientes locais e fica desabilitado em produção. Caso um
bootstrap controlado seja necessário, configure `DatabaseSeed__Enabled=true` e
forneça `DatabaseSeed__AdministratorEmail` e
`DatabaseSeed__AdministratorPassword` por Key Vault; remova essas configurações
depois da primeira inicialização.

O workflow não modifica essas configurações: ele apenas implanta a imagem. Isso
evita substituir segredos durante cada release e mantém sua rotação independente
do ciclo de deploy.

No Container App do Worker, configure via secrets ou referências ao Key Vault:

- `ConnectionStrings__WorkerDatabase`
- `RabbitMq__HostName`, `RabbitMq__UserName` e `RabbitMq__Password`
- configurações SMTP sob `Email__*`

Use `DOTNET_ENVIRONMENT=Production` e mantenha o ingress desabilitado. O release
força `min-replicas=1`, pois o consumidor RabbitMQ precisa permanecer ativo mesmo
sem requisições HTTP.
Configure probes HTTP internas na porta `8080`: `/health/live` para liveness e
startup, e `/health/ready` para readiness. A exposição pública não é necessária.

## Publicar

Após o CI de `main` estar verde:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Os dois deploys sempre referenciam a tag imutável `sha-<commit>`, mesmo quando
também publicam a tag amigável da versão. Se uma verificação final falhar, o job
termina com erro e as imagens anteriores permanecem disponíveis no ACR para
rollback.
