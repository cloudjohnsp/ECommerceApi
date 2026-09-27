# ADR 0011: Testes de aceitação entre os serviços

- Status: aceito; implementação pendente
- Data: 2026-09-27

## Contexto

As suítes unitárias e de integração existentes validam muitos componentes, mas
não comprovam integralmente o fluxo entre API, PostgreSQL, Worker independente,
RabbitMQ e simulador de pagamentos. Os maiores riscos restantes envolvem
concorrência, falhas parciais e compatibilidade dos contratos.

## Decisão

Será criada uma suíte de aceitação executada contra dependências reais isoladas,
preferencialmente via Testcontainers ou Compose dedicado. Ela não reutilizará
dados nem volumes do ambiente de desenvolvimento.

A suíte cobrirá ao menos:

- duas reservas concorrendo pela última unidade;
- múltiplas tentativas e recusa sem cancelamento imediato;
- expiração e liberação de reserva;
- duas instâncias do Worker disputando o mesmo Outbox;
- `Outbox → Worker → RabbitMQ → ProcessedAt`;
- duplicidade, retry, DLQ e interrupção entre publish e confirmação;
- fluxo ponta a ponta com webhook do simulador de pagamentos.

Os testes deverão verificar estado persistido e mensagens observáveis, evitando
assertivas baseadas apenas em mocks ou tempo fixo.

## Consequências

- Regressões distribuídas serão detectadas antes da implantação.
- A suíte será mais lenta e terá dependências de Docker.
- Cenários precisarão de IDs únicos, espera limitada e limpeza determinística.
- Falhas deverão preservar logs e dados suficientes para diagnóstico.
- Testes rápidos de Domain e Application continuarão separados desse gate.
