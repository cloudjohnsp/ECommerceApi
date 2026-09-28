# Gate coordenado dos três repositórios

O e-commerce mantém ciclos independentes para API (.NET), Worker (.NET) e
simulador de pagamentos (Flask). Cada repositório possui seu próprio gate de
restore/instalação, formato ou lint, build, testes e imagem. Este repositório
também contém o gate coordenado que comprova a compatibilidade do conjunto.

Nenhum desses gates publica imagens, faz push, implanta na Azure ou cria recurso
remoto. A promoção existente permanece separada, protegida pelo ambiente
`production` e indisponível enquanto credenciais e aprovação não forem
configuradas explicitamente.

## Revisões conhecidas

[`integration/revisions.json`](../integration/revisions.json) fixa os commits do
Worker e do Payment. `api.revision = self` significa o commit atualmente
executado pelo checkout da API, evitando a referência circular de um commit a
seu próprio hash. O script recusa dependências em outras revisões e, por padrão,
recusa qualquer repositório com alterações não commitadas.

Para atualizar o conjunto compatível:

1. valide e faça commit no Worker ou Payment;
2. substitua o hash correspondente em `integration/revisions.json`;
3. execute o gate coordenado completo;
4. faça commit do novo lock na API.

O parâmetro `-AllowDirty` existe somente para validação pré-commit. Nesse modo,
a tag recebe o sufixo `-dirty`, deixando explícito que a imagem não representa
exatamente o commit informado.

## Execução local

A partir de `ECommerceApi`:

```powershell
.\scripts\verify-multi-repository.ps1
```

Os caminhos padrão correspondem aos checkouts locais usados pelo projeto. Para
outra organização de diretórios:

```powershell
.\scripts\verify-multi-repository.ps1 `
  -WorkerPath C:\repos\ECommerceWorker `
  -PaymentPath C:\repos\ECommercePayment
```

O script executa, em ordem:

1. validação das revisões e da limpeza dos três worktrees;
2. gates rápidos e integrações PostgreSQL de API e Worker;
3. lint Ruff e testes pytest do Payment;
4. cobertura mínima de Domain e Application;
5. imagens com base fixada por digest, label OCI e tag `sha-<commit>`;
6. suíte de aceitação com PostgreSQL, duas instâncias do Worker e RabbitMQ;
7. Compose completo com dez serviços, portas aleatórias e volumes efêmeros;
8. smoke test de readiness, comunicação interna, pagamento e topologia RabbitMQ;
9. coleta de manifesto, estado e logs, seguida por limpeza determinística.

Dependências Python são instaladas em um virtualenv temporário. Para usar um
ambiente já preparado sem reinstalar pacotes:

```powershell
.\scripts\verify-multi-repository.ps1 `
  -PaymentPythonPath C:\repos\ECommercePayment\.test-venv\Scripts\python.exe `
  -SkipPaymentInstall
```

As opções `Skip*` servem para diagnóstico incremental; não equivalem ao gate de
conclusão.

## Imagens e artefatos

Cada imagem recebe:

- tag local `ecommerce-<serviço>:sha-<commit>`;
- label `org.opencontainers.image.revision=<commit>`;
- base de runtime/build fixada por digest no Dockerfile.

O script grava em `TestResults/coordinated/<timestamp>` o manifesto resolvido,
os IDs de conteúdo das imagens e os logs do Compose completo. A suíte de
aceitação preserva seus próprios diagnósticos em `TestResults/acceptance`.
Esses diretórios são ignorados pelo Git.

## Pipeline remoto opcional

`.github/workflows/integration.yml` é exclusivamente manual
(`workflow_dispatch`). Ele lê o lock, faz checkout dos commits conhecidos e
executa o mesmo script usado localmente. Possui apenas permissão de leitura e
faz upload temporário dos diagnósticos; não contém etapa de login, push,
promoção ou deploy.

Como o mantenedor ainda não habilitou a infraestrutura remota, o workflow não é
necessário para validar o projeto hoje. O script local é a fonte operacional do
gate e não gera custo de nuvem.
