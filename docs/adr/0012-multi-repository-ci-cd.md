# ADR 0012: CI/CD coordenado entre os três repositórios

- Status: aceito; implementado
- Data: 2026-09-27

## Contexto

API, Worker e simulador de pagamentos possuem tecnologias e ciclos independentes.
O pipeline atual da API não valida sozinho contratos, imagens e o fluxo integrado
dos três repositórios. Por decisão do mantenedor, publicação no GitHub e deploy na
Azure não estão habilitados neste momento.

## Decisão

Cada repositório terá pipeline próprio para restore, formato/lint, build, testes e
imagem. Um pipeline de integração coordenará versões conhecidas dos três projetos,
subirá dependências efêmeras e executará contratos, integrações PostgreSQL e
RabbitMQ e smoke test do Compose completo.

Imagens serão identificadas por commit e construídas de forma reproduzível. A
promoção para qualquer ambiente será separada da validação e dependerá de
credenciais e aprovação configuradas explicitamente no provedor escolhido.

Enquanto não houver infraestrutura remota, scripts locais deverão oferecer os
mesmos gates sem criar recursos externos ou custos em nuvem.

## Consequências

- Falhas de um projeto ou contrato impedirão promover o conjunto incompatível.
- Releases individuais continuarão possíveis quando preservarem compatibilidade.
- O pipeline de integração precisará fixar revisões dos três repositórios.
- Segredos permanecerão fora do código e dos artefatos de log.
- A decisão não autoriza publicação, push ou criação de recursos Azure.

## Implementação

Os três repositórios possuem gates próprios. API e Worker executam restore,
verificação de formato, build, testes PostgreSQL e build da imagem; o Payment
executa instalação, lint Ruff, pytest e build da imagem. Os mesmos
gates podem ser executados localmente pelos scripts `scripts/verify-local.ps1`
de cada repositório.

O conjunto compatível é fixado em `integration/revisions.json`. O gate em
`scripts/verify-multi-repository.ps1` valida essas revisões, executa os gates
individuais, constrói imagens `sha-<commit>` com bases fixadas por digest e
confere o label OCI de revisão. Em seguida, ele executa a suíte de aceitação
entre serviços e o Compose completo com PostgreSQL, RabbitMQ, Redis, Azurite,
Mailpit, API, Worker, Payment, Prometheus e Grafana em recursos efêmeros.

O workflow manual `.github/workflows/integration.yml` executa o mesmo script a
partir dos commits fixados. Ele possui somente permissão de leitura e não faz
login em registry, push, promoção, deploy nem criação de recursos externos. A
promoção permanece um processo separado, dependente de credenciais e aprovação
explicitamente configuradas.

O procedimento operacional, o versionamento das imagens, a atualização do lock
e o diagnóstico de falhas estão documentados em
`docs/multi-repository-ci.md`.
