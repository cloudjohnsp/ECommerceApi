# ADR 0005: Consolidação do Worker independente

- Status: aceito; implementado
- Data: 2026-09-27

## Contexto

Existia um Worker maduro dentro da solução `ECommerceApi` e um Worker C#/.NET em
repositório independente com escopo menor. Manter duas implementações como
definitivas criava ambiguidade operacional, duplicação de correções e risco de
dois processos executarem a mesma responsabilidade.

O ADR 0001 define o repositório independente como destino canônico.

## Decisão

As capacidades do Worker interno foram migradas para `ECommerceWorker`, incluindo
contratos locais, persistência, observabilidade e testes, sem referências aos
projetos da API.

O Compose principal consome uma imagem produzida pelo repositório independente,
mantendo um único proprietário ativo para as responsabilidades do Worker.

O projeto `src/ECommerce.Worker` e seus testes duplicados foram removidos da
solução principal depois da equivalência funcional, das integrações PostgreSQL e
da imagem independente serem validadas.

## Consequências

- O Worker possui build, testes, imagem e release independentes.
- A API não compila, testa nem publica o Worker.
- O Compose seleciona o Worker pela variável `ECOMMERCE_WORKER_IMAGE`.
- Contratos locais e testes de compatibilidade substituem referências compartilhadas.
- O histórico Git preserva a implementação removida para auditoria e rollback.
