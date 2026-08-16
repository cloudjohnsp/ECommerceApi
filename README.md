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

O health check fica disponível em `/health`.
