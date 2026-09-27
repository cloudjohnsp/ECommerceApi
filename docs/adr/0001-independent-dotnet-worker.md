# ADR 0001: Worker .NET independente

- Status: aceito; migração pendente
- Data: 2026-09-27

## Contexto

O ecossistema possui uma API ASP.NET Core e processamento assíncrono para Outbox,
publicação no RabbitMQ e consumers idempotentes. Hoje existe um Worker dentro da
solução `ECommerceApi` e outro projeto no repositório independente
`ECommerceWorker`.

A decisão anterior descrevia o Worker independente como uma aplicação Go. O
Worker já foi iniciado em C#/.NET e essa tecnologia será preservada.

## Decisão

O repositório `ECommerceWorker` será a implementação canônica do Worker e terá
ciclo próprio de build, testes, imagem, versão e implantação.

Embora API e Worker usem .NET, o Worker não referenciará projetos ou assemblies
internos da API, incluindo `ECommerce.Domain`, `ECommerce.Application`,
`ECommerce.Persistence`, `ECommerce.Infrastructure` e `ECommerce.Shared`.

A integração ocorrerá exclusivamente por:

- contratos de transporte explícitos e versionados;
- tabelas PostgreSQL cuja interface seja documentada, incluindo o Outbox;
- exchanges, filas, routing keys e envelopes de eventos documentados no RabbitMQ.

Entidades de domínio e comandos internos não serão usados como contratos de
integração. Pequenos DTOs de transporte poderão ser representados em ambos os
repositórios; testes de compatibilidade protegerão seus formatos serializados.

O Worker atualmente contido na solução `ECommerceApi` é transitório. Suas
responsabilidades serão migradas incrementalmente para o repositório independente
e ele só será removido depois que testes de integração comprovarem equivalência.

## Consequências

- Os processos podem ser versionados e implantados independentemente.
- Alterações internas da API não forçam uma nova versão do Worker.
- Mudanças em contratos exigem versão, documentação e testes de compatibilidade.
- Alguma duplicação deliberada de DTOs de transporte é aceitável.
- A migração deve evitar dois publishers processando a mesma mensagem do Outbox.
