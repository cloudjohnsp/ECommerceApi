# ADR 0008: Consumers idempotentes, retry limitado e DLQ

- Status: implementado
- Data: 2026-09-27

## Contexto

A entrega pelo RabbitMQ e a publicação do Outbox são *at least once*. Mensagens
podem ser repetidas, inválidas ou falhar de modo transitório. Requeue infinito
oculta poison messages, aumenta custo e impede progresso previsível da fila.

O Worker interno possui mecanismos maduros que ainda precisam ser consolidados no
Worker independente.

## Decisão

Cada consumer registrará `MessageId` em uma Inbox com constraint única e aplicará
efeitos e confirmação local na mesma transação. Redeliveries concluídas serão
reconhecidas sem repetir efeitos.

Falhas transitórias usarão número máximo de tentativas e atraso configurável.
Mensagens inválidas ou esgotadas serão encaminhadas para dead-letter exchange e
fila durável, com motivo sanitizado e metadados suficientes para diagnóstico.

Os consumers iniciais cobrirão pagamento concluído, cancelamento, estoque, e-mail,
invoice e analytics simulados. Nenhum consumer executará alteração financeira não
idempotente sem uma chave persistida.

## Consequências

- Duplicidade não gera invoice, e-mail ou mudança financeira adicional indevida.
- Poison messages deixam de bloquear ou circular indefinidamente.
- Operação precisará de métricas, alertas e procedimento de replay da DLQ.
- Replay exigirá preservar o `MessageId` original.
- Testes deverão cobrir concorrência, redelivery e limite de tentativas.

## Implementação

- O Worker independente usa `ConsumedIntegrationEvent.MessageId` como chave
  única da Inbox e um advisory lock PostgreSQL por mensagem. Inbox, projeções,
  invoice, analytics e intenção de notificação são confirmadas na mesma
  transação antes do ACK.
- `order.paid` (pagamento concluído), `order.cancelled`, `stock.updated` e
  `email.sent` possuem efeitos cobertos por testes. Invoice é única por pedido e
  analytics é única por `MessageId`.
- Falhas transitórias são republicadas com publisher confirm no exchange de
  retry e retornam após TTL configurável. `x-retry-count` limita as tentativas;
  mensagens inválidas ou esgotadas seguem para exchange e fila de dead-letter
  duráveis.
- A cópia para DLQ preserva corpo, `MessageId`, tipo, versão, correlação e routing
  key e acrescenta somente motivo sanitizado, contagem e instante. O runbook do
  Worker exige preservar o `MessageId` original no replay.
- Métricas distinguem consumo, duplicidade, falha, retry e dead-letter. Testes
  unitários cobrem redelivery e o limite, e testes PostgreSQL cobrem o lock
  concorrente, migrations e atomicidade dos efeitos.
