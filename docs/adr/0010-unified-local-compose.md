# ADR 0010: Ambiente local unificado com Docker Compose

- Status: implementado
- Data: 2026-09-27

## Contexto

API, Worker independente e simulador de pagamentos vivem em repositórios
separados e hoje possuem composições parciais. Isso dificulta reproduzir o fluxo
completo e permite divergências de rede, credenciais, health checks e versões.

O ambiente local não deve depender de recursos ou credenciais reais da Azure.

## Decisão

Será mantido um Compose de integração capaz de iniciar com um único comando:
API .NET, Worker C#/.NET independente, Payment Flask, PostgreSQL, Redis, RabbitMQ,
Azurite, Mailpit, Prometheus e Grafana.

Cada aplicação continuará com Dockerfile e ciclo de release próprios. O Compose
referenciará contextos locais explicitamente, usará rede interna para comunicação,
health checks para ordenação e `.env` não versionado para segredos locais.

Azurite continuará simulando Azure Blob Storage e nenhuma conta Azure será
necessária. Volumes persistentes não serão apagados ou terão credenciais alteradas
automaticamente durante atualizações normais.

## Consequências

- O fluxo completo poderá ser reproduzido e testado localmente com um comando.
- O orquestrador precisará conhecer os caminhos dos três repositórios.
- Portas publicadas serão mínimas e preferencialmente ligadas ao loopback.
- Mudanças de credenciais em volumes existentes exigirão migração explícita.
- Um smoke test deverá validar health/readiness e o fluxo essencial após o start.

## Implementação

- `docker-compose.yml` constrói os três contextos locais, conecta dez serviços a
  redes isoladas pelo nome do projeto Compose e ordena a inicialização por
  health checks.
- `scripts/start-local.ps1` inicializa segredos ausentes, executa `compose up`
  com `--wait` e chama `scripts/smoke-test-local.ps1`.
- O smoke test valida containers, endpoints, comunicação API/Payment, criação e
  aprovação de pagamento e as filas do Worker.
- `docs/local-development.md` documenta caminhos, portas, persistência, ausência
  de recursos Azure reais e a migração explícita de credenciais persistidas.
