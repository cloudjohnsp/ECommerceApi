# Deploy no Azure App Service

O workflow `.github/workflows/release.yml` valida a solução, publica uma imagem
imutável no Azure Container Registry (ACR), atualiza um Azure App Service Linux
e confirma o endpoint `/api/health/live`. Ele é executado para tags semânticas
como `v1.0.0` ou manualmente pelo GitHub Actions.

## Recursos necessários

- Um Azure Container Registry.
- Um Azure App Service Linux configurado para container único.
- PostgreSQL, Redis, RabbitMQ, SMTP e armazenamento de blobs alcançáveis pelo
  App Service.
- Uma identidade do Microsoft Entra usada pelo GitHub via OIDC.

O App Service deve conseguir baixar imagens privadas do ACR. Prefira habilitar
sua identidade gerenciada, conceder a ela `AcrPull` no registry e configurar o
App Service para autenticar no ACR com essa identidade. A identidade federada do
GitHub precisa de `AcrPush` no registry e permissão para atualizar o App Service.
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

O workflow não modifica essas configurações: ele apenas implanta a imagem. Isso
evita substituir segredos durante cada release e mantém sua rotação independente
do ciclo de deploy.

## Publicar

Após o CI de `main` estar verde:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

O deploy sempre referencia a tag imutável `sha-<commit>`, mesmo quando também
publica a tag amigável da versão. Se a verificação HTTP final falhar, o job
termina com erro e a imagem anterior permanece disponível no ACR para rollback.
