# ADR 0008: Consumers idempotentes, retry limitado e DLQ

- Status: aceito; migração pendente
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
