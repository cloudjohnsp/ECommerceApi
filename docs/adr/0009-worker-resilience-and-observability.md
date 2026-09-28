# ADR 0009: Resiliência e observabilidade do Worker

- Status: implementado
- Data: 2026-09-27

## Contexto

O Worker será um processo de longa duração responsável por polling, publicação e
consumo. Sem sinais operacionais e limites explícitos, indisponibilidade externa,
backlog ou encerramento do container podem causar perda de diagnóstico, pressão
excessiva ou interrupção abrupta do trabalho.

## Decisão

O Worker independente fornecerá endpoints separados de liveness e readiness.
Readiness refletirá as dependências essenciais para executar o papel habilitado no
processo, sem transformar indisponibilidade transitória em encerramento imediato.

Logs estruturados, métricas Prometheus e traces OpenTelemetry incluirão
`CorrelationId`, `MessageId`, `OrderId`, `PaymentId` e `OutboxMessageId` quando
aplicáveis. Segredos e payloads sensíveis não serão registrados.

Polling interval, batch size, concorrência, timeouts e connection pools serão
configuráveis e limitados. `CancellationToken` será propagado, novos trabalhos
serão interrompidos no shutdown e operações em curso terão prazo de drenagem.

## Consequências

- Orquestradores poderão distinguir processo vivo de processo pronto.
- Backlog, retries, DLQ e latências ficarão mensuráveis.
- Dashboards e alertas precisarão acompanhar as novas métricas.
- Shutdown poderá deixar claims expirarem e serem retomados com segurança.
- Configurações inválidas deverão falhar na inicialização com mensagem sanitizada.

## Implementação

- `/health/live` não consulta dependências; `/health/ready` verifica PostgreSQL,
  RabbitMQ e SMTP somente quando o papel de e-mail está habilitado.
- Serilog, ActivitySource e métricas carregam os identificadores disponíveis sem
  registrar payload, destinatário, token ou segredo. `PaymentId` permanece
  ausente porque nenhum contrato atual do Worker o transporta.
- Backlogs das outboxes, idade das mensagens, duração por operação, retry e DLQ
  são exportados ao Prometheus. O Compose provisiona dashboard do Worker e
  regras de alerta para indisponibilidade, backlog, retry, DLQ e p95.
- `WorkerRuntime` limita concorrência, pool EF, timeouts RabbitMQ/PostgreSQL e
  drenagem. Batch, polling, leases, tentativas e SMTP também têm intervalos
  validados com `ValidateOnStart`.
- No shutdown, novos trabalhos deixam de ser aceitos, o consumer é cancelado e
  operações em curso recebem prazo de drenagem; após ele, o token é cancelado e
  mensagens/claims podem ser retomados com segurança.
- Testes cobrem drenagem bem-sucedida, bloqueio de novos trabalhos e cancelamento
  ao atingir o prazo. A validação integrada cobre health, métricas e startup.
