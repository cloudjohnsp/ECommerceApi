# ADR 0005: Consolidação do Worker independente

- Status: aceito; migração pendente
- Data: 2026-09-27

## Contexto

Existe um Worker maduro dentro da solução `ECommerceApi` e um Worker C#/.NET em
repositório independente com escopo ainda menor. Manter duas implementações como
definitivas cria ambiguidade operacional, duplicação de correções e risco de dois
processos executarem a mesma responsabilidade.

O ADR 0001 define o repositório independente como destino canônico.

## Decisão

As capacidades do Worker interno serão migradas incrementalmente para
`ECommerceWorker`. Cada etapa deverá levar contratos locais, persistência,
observabilidade e testes necessários sem adicionar referências aos projetos da
API.

Durante a transição, cada responsabilidade terá um único proprietário ativo por
ambiente. Feature flags ou configuração de implantação serão usadas para evitar
dois publishers ou consumers concorrentes não planejados.

O projeto `src/ECommerce.Worker` só será removido da solução principal depois de
comprovada a equivalência funcional e operacional do Worker independente.

## Consequências

- A migração pode ser entregue em etapas reversíveis.
- Código interno da API poderá servir como referência, mas não como dependência.
- Compose, documentação e CI precisarão mudar conforme a propriedade for transferida.
- A remoção final reduzirá duplicação e permitirá releases independentes.
- Rollback exigirá preservar temporariamente a implementação anterior desativada.
