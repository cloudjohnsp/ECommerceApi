# Ambiente local unificado

O ambiente de integração local é orquestrado pelo `docker-compose.yml` deste
repositório. Ele constrói as três aplicações a partir de Dockerfiles mantidos
em seus próprios repositórios e inicia todas as dependências com um comando:

```powershell
.\scripts\start-local.ps1
```

O comando cria ou completa o `.env` sem exibir segredos, constrói as imagens,
aguarda os health checks e executa o smoke test. `-NoBuild` reutiliza imagens já
construídas e `-SkipSmokeTest` deve ser usado apenas quando a validação será
executada separadamente com `scripts/smoke-test-local.ps1`.

## Layout dos repositórios

Os caminhos são resolvidos a partir do diretório de `docker-compose.yml` e podem
ser ajustados no `.env` local:

```dotenv
ECOMMERCE_WORKER_CONTEXT=../../../personal-projects/ECommerceWorker
ECOMMERCE_PAYMENT_CONTEXT=../../../personal-projects/ECommercePayment
```

O contexto da API é `.`. Worker e Payment conservam Dockerfiles e ciclos de
release próprios; o Compose apenas os integra. Os dez serviços de runtime são
API .NET, Worker C#/.NET, Payment Flask, PostgreSQL, Redis, RabbitMQ, Azurite,
Mailpit, Prometheus e Grafana. Um job transitório e idempotente cria o database
`ecommerce_payment` no mesmo PostgreSQL antes de iniciar o Payment; a separação
evita colisões de schema entre EF Core e Alembic.

## Rede, portas e dados

Todos os containers compartilham a rede Docker interna criada para o projeto
Compose e se comunicam por nomes de serviço. Somente containers com interface
publicada também participam da rede bridge `local-access` do mesmo projeto,
necessária para o encaminhamento de portas do Docker Desktop. Os nomes recebem
o prefixo do projeto Compose, permitindo gates isolados em paralelo. PostgreSQL,
Redis, AMQP e SMTP não publicam portas no host. As interfaces úteis ao
desenvolvimento são publicadas sempre em `127.0.0.1`:

| Serviço | URL local |
|---|---|
| API | `http://127.0.0.1:8080` |
| Payment | `http://127.0.0.1:5002` |
| Azurite Blob | `http://127.0.0.1:10000` |
| Mailpit | `http://127.0.0.1:8025` |
| RabbitMQ Management | `http://127.0.0.1:15672` |
| Prometheus | `http://127.0.0.1:9090` |
| Grafana | `http://127.0.0.1:3001` |

Volumes nomeados preservam PostgreSQL, RabbitMQ, Redis, Azurite, Prometheus e
Grafana durante rebuilds e atualizações normais. Containers são recriados para
convergir rede e configuração, mas seus volumes não são removidos.
`start-local.ps1` não executa
`down -v` e nunca substitui valor não vazio no `.env`. Para parar sem apagar os
dados:

```powershell
docker compose --env-file .env down
```

Não use `down --volumes` a menos que a perda deliberada de todos os dados locais
seja aceitável.

Azurite é um emulador inteiramente local. A connection string usa somente a
conta pública padrão `devstoreaccount1`; não existe login, assinatura, recurso
ou credencial de uma conta Azure real, portanto esse ambiente não gera custo na
Azure.

## Health checks e smoke test

O Compose inicia dependências por readiness: banco e serviços básicos, Payment,
API, Worker, Prometheus e, por fim, Grafana. O smoke test confirma que os dez
containers estão saudáveis, consulta os endpoints públicos, verifica a chamada
interna da API ao Payment, cria e aprova um pagamento no simulador e confirma as
filas principal, de retry e DLQ do Worker.

Para repetir somente essa validação:

```powershell
.\scripts\smoke-test-local.ps1
```

## Migração explícita de credenciais persistidas

Alterar `POSTGRES_PASSWORD` ou `RABBITMQ_PASSWORD` no `.env` não modifica a
senha armazenada nos volumes já existentes. Uma rotação deve ser deliberada e
feita nesta ordem para não interromper o ambiente:

1. mantenha o valor atual no `.env` e inicie os serviços;
2. escolha a nova senha sem gravá-la no repositório ou em scripts versionados;
3. altere a senha persistida no próprio serviço: use `ALTER ROLE ... PASSWORD`
   em uma sessão `psql` autenticada para PostgreSQL e
   `rabbitmqctl change_password <usuario> <nova-senha>` para RabbitMQ;
4. atualize somente então o valor correspondente no `.env` local;
5. recrie API, Worker, Payment e o serviço alterado com
   `docker compose --env-file .env up --detach --force-recreate --wait`;
6. execute `.\scripts\smoke-test-local.ps1`.

Evite colocar a nova senha diretamente em histórico de shell compartilhado.
Se a senha persistida e o `.env` já estiverem divergentes, restaure
temporariamente o valor anterior no `.env`, inicie o serviço e siga o mesmo
procedimento. Apagar volumes é recuperação destrutiva, não migração.
