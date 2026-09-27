# ADR 0012: CI/CD coordenado entre os três repositórios

- Status: aceito; implementação pendente
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
