# Testes de aceitação entre serviços

A suíte `ECommerceAcceptance.slnx` valida os contratos e as garantias
distribuídas entre a API, PostgreSQL, duas instâncias do Worker independente,
RabbitMQ e o simulador Flask de pagamentos. Ela é intencionalmente separada de
`ECommerceApi.slnx`, para que o ciclo rápido de Domain, Application e demais
testes locais não dependa do Docker.

## Pré-requisitos

- Docker Desktop em execução;
- os repositórios `ECommerceApi`, `ECommerceWorker` e `ECommercePayment`
  disponíveis nos caminhos irmãos usados pelo ambiente local;
- .NET 10 SDK.

Caso os repositórios independentes estejam em outros diretórios, informe os
contextos absolutos em `ECOMMERCE_WORKER_CONTEXT` e
`ECOMMERCE_PAYMENT_CONTEXT`.

## Execução

No PowerShell, a partir do repositório da API:

```powershell
$env:RUN_CROSS_SERVICE_ACCEPTANCE_TESTS = "true"
dotnet test ECommerceAcceptance.slnx
Remove-Item Env:RUN_CROSS_SERVICE_ACCEPTANCE_TESTS
```

Sem a variável explícita, os testes são ignorados e nenhum container é criado.
A suíte cria um projeto Compose exclusivo por execução, usa portas dinâmicas,
duas bases efêmeras no mesmo PostgreSQL e duas instâncias do Worker. Nenhum
volume ou dado do ambiente de desenvolvimento é reutilizado. Ao terminar, ela
remove containers, redes e volumes, inclusive após falhas.

Por padrão, cada gate reconstrói três imagens locais estáveis para incluir o
código atual. Em uma repetição sem alterações, elas podem ser reutilizadas com
`ECOMMERCE_ACCEPTANCE_REUSE_IMAGES=true`; a fixture só aplicará `--no-build` se
as três existirem. Essa opção também permite repetir a suíte sem rede depois do
primeiro build, mas não deve ser usada após alterar API, Worker ou Payment.

## Cenários cobertos

- duas transações concorrendo pela última unidade de estoque;
- duas instâncias do Worker disputando a mesma Outbox;
- caminho `Outbox → Worker publisher → RabbitMQ → Worker consumer → ProcessedAt`;
- recusa de pagamento sem cancelar imediatamente o pedido e nova tentativa;
- webhook assinado do simulador concluindo o pedido aprovado;
- expiração do pedido, liberação da reserva e consumo do cancelamento;
- entrega duplicada protegida pela inbox;
- falha transitória com retry e esgotamento na DLQ;
- evento inválido encaminhado à DLQ;
- interrupção depois da confirmação do broker e antes da confirmação da Outbox,
  seguida por recuperação de lease e reprocessamento idempotente.

As esperas consultam estado observável com prazo máximo; não há `sleep` fixo nas
assertivas. Cada execução usa IDs únicos. Os testes verificam PostgreSQL,
propriedades das mensagens e filas reais do RabbitMQ, sem substituir serviços
por mocks.

## Diagnóstico

Antes da limpeza, a fixture preserva em `TestResults/acceptance/<run-id>`:

- `compose.log`, com os logs de todos os serviços;
- `persistent-state.json`, com o resumo do estado persistido;
- `rabbitmq-queues.txt`, com mensagens prontas e não confirmadas por fila.

O diretório é ignorado pelo Git. Em uma falha, use o identificador da pasta mais
recente para correlacionar os artefatos.
