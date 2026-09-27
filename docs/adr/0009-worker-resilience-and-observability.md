# ADR 0009: Resiliência e observabilidade do Worker

- Status: aceito; implementação pendente no Worker independente
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
