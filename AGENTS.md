# AGENTS.md

## Repository overview

This repository is a .NET e-commerce backend built with Clean Architecture principles. The solution is organized into separate projects by responsibility and dependency direction.

### Project boundaries

- `src/ECommerce.Api`: HTTP layer. Hosts controllers, middleware, OpenAPI configuration, dependency wiring, and startup composition.
- `src/ECommerce.Application`: use cases, MediatR handlers, validators, DTOs, abstractions, and orchestration logic.
- `src/ECommerce.Domain`: domain model, entities, value objects, enums, and business rules without infrastructure references.
- `src/ECommerce.Persistence`: EF Core persistence layer, DbContext, repository implementations, migrations, and unit-of-work.
- `src/ECommerce.Infrastructure`: external integrations and security-related infrastructure implementations.
- `src/ECommerce.Shared`: shared primitives such as result wrappers and reusable cross-cutting types.

## Architectural conventions

1. Dependency direction is inward.
   - `Api` depends on `Application`.
   - `Application` depends on `Domain` and `Shared`.
   - `Persistence` and `Infrastructure` implement abstractions defined in `Application`.
   - `Domain` must not reference infrastructure or API projects.

2. Business logic lives in the domain and application layers.
   - Domain entities should contain validation and state transitions when possible.
   - Application handlers should orchestrate business rules, validate requests, invoke repositories, and coordinate unit-of-work commits.
   - The API layer should be thin and avoid business logic.

3. Use the established patterns already used in the repo.
   - MediatR for request/handler dispatch.
   - FluentValidation for request validation.
   - Mapster for DTO mapping.
   - EF Core + PostgreSQL via `AppDbContext` and repositories.
   - `Result<T>` and failure-first flow for domain/application operations.

4. Keep composition in `DependencyInjection.cs` files.
   - `src/ECommerce.Api/DependencyInjection.cs` wires API services and app-level registrations.
   - `src/ECommerce.Application/DependencyInjection.cs` registers MediatR, validators, behaviors, and Mapster config.
   - `src/ECommerce.Persistence/DependencyInjection.cs` registers DbContext, repositories, and unit-of-work.
   - `src/ECommerce.Infrastructure/DependencyInjection.cs` registers infrastructure services.

## Naming and structure conventions

- Handlers are grouped by feature under folders such as `Products/Handlers`, `Users/Handlers`, `Orders/Handlers`, `Auth/Handlers`.
- Validators live beside feature handlers in `Validators` folders.
- Repositories implement interfaces from `ECommerce.Application.Abstractions.Persistence`.
- Commands, queries, DTOs, and handlers should follow the existing feature-based naming pattern.
- Prefer composing features around use-case folders rather than creating unrelated utility folders.

## Persistence and data access conventions

- Use `AppDbContext` in `src/ECommerce.Persistence/Contexts` for all EF Core access.
- Repositories should be scoped services and should implement the relevant application abstraction.
- Mutations should typically go through a repository plus `IUnitOfWork` commit pattern.
- PostgreSQL is the configured database provider (`UseNpgsql`).
- Keep schema changes in migrations rather than ad-hoc database modifications.

## Application behavior conventions

- Prefer returning `Result<T>` values from handlers instead of throwing for expected validation or business failures.
- Validation should be handled both at the request level (`FluentValidation`) and in domain entities when state rules are intrinsic to the entity.
- Use `IUnitOfWork` when a handler performs multiple persistence-related operations.
- Keep handlers focused on a single responsibility and avoid leaking persistence concerns into the API layer.

## API conventions

- Controllers in `src/ECommerce.Api/Controllers` should be endpoint-focused and thin.
- Route formatting should remain lowercase and consistent with the existing `RouteOptions` configuration.
- Use the existing middleware and exception-handling pattern for cross-cutting concerns.
- OpenAPI/Swagger configuration belongs in the API project, not the application layer.

## Testing conventions

- Tests are split by layer:
  - `ECommerce.Domain.Tests`
  - `ECommerce.Application.Tests`
  - `ECommerce.Persistence.Tests`
- Prefer testing real behavior and domain rules rather than mock-only assertions.
- Add failing tests for regressions before implementing a fix when changing business logic.

## Build and validation commands

Use the repo's .NET solution when validating changes:

```bash
dotnet restore
 dotnet test ECommerceApi.slnx
```

If running a specific project is needed:

```bash
dotnet test src/ECommerce.Application/ECommerce.Application.csproj
 dotnet test src/ECommerce.Persistence/ECommerce.Persistence.csproj
```

## Agent instructions

- Respect project boundaries and never move logic across layers just to make it convenient.
- Prefer the existing conventions in the repo over introducing new frameworks or patterns.
- If a change affects API contracts, validate whether DTOs, validators, and handlers still match the current feature structure.
- Do not add infrastructure dependencies into the domain layer.
- Keep code aligned with the repository's feature-oriented architecture and clean separation of concerns.

## Summary

The solution is organized around Clean Architecture and feature-based use cases, with MediatR, FluentValidation, Mapster, and EF Core as the primary implementation conventions. Follow those patterns consistently when adding or modifying code.
