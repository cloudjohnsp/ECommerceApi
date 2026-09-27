# Arquitetura e decisões

## Dependências

```text
ECommerce.Api
    ├── ECommerce.Application
    ├── ECommerce.Infrastructure
    └── ECommerce.Persistence

ECommerce.Application
    ├── ECommerce.Domain
    └── ECommerce.Shared

ECommerce.Infrastructure ──► contratos da Application
ECommerce.Persistence    ──► contratos da Application
ECommerce.Worker         ──► ECommerce.Shared
```

O domínio não referencia ASP.NET Core, EF Core, RabbitMQ, Redis ou serviços
externos. Application descreve casos de uso e portas; Persistence e
Infrastructure implementam essas portas. O Worker possui armazenamento próprio
no schema PostgreSQL `worker` e compartilha somente o contrato serializado dos
eventos.

As rotas operacionais `/api/health`, `/api/health/live` e `/api/health/ready`
são atendidas pelo middleware de health checks. O alias `/api/v1/health` é uma
rota de liveness separada e não sombreia o diagnóstico completo das dependências.

## Validação

A validação não é repetida igualmente em todas as camadas:

1. Controllers validam apenas aspectos HTTP, identidade e autorização.
2. FluentValidation rejeita formatos e campos obrigatórios antes do handler.
   Quando o contrato retorna `Result` ou `Result<T>`, cada mensagem distinta é
   devolvida como erro do próprio resultado, sem usar exceção como fluxo esperado.
3. Entidades protegem invariantes e transições de estado com `Result`.
4. Configurações Fluent API definem tipos, relações, índices e constraints de
   persistência; elas não substituem regras de negócio.
5. PostgreSQL é a última barreira para unicidade, integridade referencial e
   concorrência. Check constraints também impedem estoque ou reserva negativos,
   reserva acima do estoque físico, valores monetários não positivos, quantidades
   inválidas e estados desconhecidos. Um índice único em `(order_id, product_id)`
   replica no banco a regra de que um produto aparece no máximo uma vez por pedido.

Falhas esperadas retornam `Result`; exceções ficam reservadas para condições
inesperadas e são convertidas em `ProblemDetails` sanitizado pelo middleware da
API. Respostas de erro incluem `traceId` e `correlationId` para localizar os logs
correspondentes sem revelar detalhes internos da exceção.

## Catálogo

Atualizações e desativações de categorias são executadas em transação e usam
`FOR UPDATE`. Isso serializa mutações da mesma categoria, evita que uma alteração
de nome concorrente reative ou sobrescreva uma desativação e mantém a invalidação
do cache estritamente posterior ao commit. A criação ou atualização de produto
também bloqueia a categoria escolhida até o commit, portanto um vínculo novo não
pode ser confirmado enquanto a categoria é desativada concorrentemente.
Criação e renomeação também adquirem um advisory lock pelo slug normalizado antes
da consulta autoritativa de unicidade, evitando que requisições concorrentes
convertam uma disputa de nome esperada em violação de índice.

O upload de imagem não mantém transação PostgreSQL aberta durante a chamada ao
Blob Storage. Depois do upload, uma transação curta bloqueia e revalida o produto
antes de gravar os metadados. Se ele tiver sido desativado ou a persistência
falhar, o blob recém-criado é removido de forma compensatória.

## Pedidos, estoque e pagamentos

Criar um pedido ou adicionar um item bloqueia os produtos envolvidos, reserva
estoque e persiste pedido e evento de outbox na mesma transação. Cancelar um
pedido pendente libera a reserva.
Somente uma conta ativa com role `Customer` pode ser associada como cliente de
um novo pedido; administradores podem operar e consultar pedidos, mas não são
aceitos como identidade comercial do pedido. A criação bloqueia o cliente antes
dos produtos, impedindo que uma desativação concorrente seja intercalada entre
a autorização comercial e o commit. Produtos inativos são rejeitados mesmo que
ainda possuam estoque físico.

A desativação de produto usa o mesmo bloqueio pessimista das reservas e ajustes
de estoque. Assim, ela não persiste uma cópia obsoleta do agregado nem sobrescreve
uma reserva ou consumo que tenha ocorrido concorrentemente.

Cada criação de inventário e transição relevante de reserva, consumo, liberação,
restauração ou ajuste também grava `stock.updated` na mesma transação. O contrato
público contém somente `ProductId`, `AvailableStock`, o `OrderId` opcional, o
motivo e o instante da alteração. `Stock` e `ReservedStock` continuam detalhes
privados do agregado de inventário.

O estoque não é decrementado de forma assíncrona pelo Worker. Essa decisão evita
venda acima da disponibilidade: aprovação do webhook bloqueia pagamento, pedido
e produtos e, atomicamente, confirma o pagamento, marca o pedido como pago,
consome as reservas e grava `order.paid`. Recusa libera as reservas; reembolso
restaura estoque físico.

O endpoint administrativo de atualização não aprova pagamentos. Ele somente
reconcilia um pedido pendente quando já existe `PaymentStatus.Paid`, aplicando as
mesmas alterações de estoque e outbox em uma transação.

## Outbox do gateway

A criação local de `Payment` e a intenção `PaymentCreationRequested` usam o
mesmo identificador e a mesma transação. A chamada HTTP ao gateway ocorre apenas
depois do commit. Se ela falhar, a intenção permanece pendente para o Hangfire.
Se o gateway responder e o processo cair antes do segundo commit, a mesma chave
de idempotência é reutilizada no retry.

Depois da chamada HTTP, o processador recarrega e bloqueia primeiro o pagamento
e depois a intenção antes de registrar o identificador externo. Ele nunca salva
a instância lida antes da chamada externa; assim, um webhook ou retry concorrente
que já tenha avançado o pagamento não pode ser sobrescrito por um estado
`Pending` obsoleto.
Uma criação só conclui essa intenção quando o gateway confirma o estado
`pending`; estados inesperados são tratados como resposta inválida e mantêm a
intenção disponível para diagnóstico e retry.

O gateway é a autoridade de `ExternalPaymentId`; o e-commerce mantém seu próprio
`Payment.Id` e nunca mantém uma transação PostgreSQL aberta durante uma chamada
externa.
O pagamento local preserva valor e moeda ISO normalizada. Como há apenas um
pagamento por pedido, tentativas repetidas com outra moeda são rejeitadas antes
de qualquer chamada ao gateway.
Uma resposta de reembolso só pode avançar o estado local quando confirma o mesmo
`ExternalPaymentId` enviado na solicitação e o status `refunded`.
Respostas 2xx do gateway ainda precisam usar um media type JSON e conter
identificador e status não vazios; violações do contrato são retornadas como
falha controlada da integração, sem expor exceções de desserialização.
Webhooks são autenticados por HMAC e o par `event`/`data.status` é validado antes
de abrir a transação, evitando que um payload contraditório aplique uma transição
financeira diferente da informada pelo gateway. Depois de bloquear o pagamento
local pelo `ExternalPaymentId`, o handler também exige que `reference`, `amount`
e `currency` correspondam ao pedido, valor e moeda persistidos antes de alterar
pagamento, pedido ou estoque.

Operações que precisam alterar pagamento, pedido e estoque adquirem os bloqueios
sempre na ordem `Payment → Order → Product`, com produtos ordenados pelo
identificador. A reconciliação administrativa, o webhook e o reembolso seguem a
mesma ordem para impedir ciclos de espera e deadlocks entre esses fluxos.

## Eventos e Worker

Eventos de integração são publicados cronologicamente a partir da outbox. O lote
para no primeiro erro para preservar causalidade. A publicação é *at least once*:
uma queda depois do publish e antes de marcar a outbox pode gerar redelivery.
As routing keys incluem os eventos de ciclo de vida do pedido, `stock.updated` e
`email.sent`.

O Worker usa:

- fila quorum durável e ACK manual;
- atraso antes de retry e limite de entregas;
- dead-letter exchange e fila para mensagens inválidas ou esgotadas;
- inbox com `MessageId` único;
- projeção local do pedido;
- projeção local de `AvailableStock` alimentada por `stock.updated`;
- projeção sanitizada das entregas alimentada por `email.sent`;
- nota fiscal simulada única por pedido pago;
- outbox de notificações, com lease e backoff exponencial;
- outbox de integração para publicar a confirmação do SMTP sem dual-write.

Inbox, projeções, nota e intenção de notificação são confirmadas antes do ACK. Ao
enviar uma notificação, o Worker marca a entrega e cria `email.sent` na mesma
transação local; outra etapa publica o evento com confirmação, lease e retry. O
SMTP continua sendo *at least once*: uma falha depois da aceitação pelo servidor
e antes de `SentAt` pode repetir a mensagem, limitação documentada do protocolo.
Consumidores concorrentes do mesmo `MessageId` são serializados por advisory lock
transacional antes da segunda consulta à inbox. A constraint única permanece como
barreira final, sem transformar uma redelivery simultânea em falha e novo retry.

## Segurança

- JWT é validado na borda e refresh tokens são armazenados com hash e rotacionados.
- A rotação abre uma transação e bloqueia o token por hash com `FOR UPDATE`, de
  modo que duas requisições concorrentes não possam criar duas cadeias válidas.
- Reutilizar um refresh token revogado invalida a família ativa do usuário,
  inclusive um token emitido por uma rotação concorrente que terminou primeiro.
- Logout bloqueia o token apresentado e revoga todos os refresh tokens ativos do
  usuário; assim, logout concorrente com rotação não deixa uma sessão residual.
- Troca ou redefinição de senha, alteração de role e desativação também revogam
  todos os refresh tokens na mesma unidade de trabalho da mutação e auditoria.
- A troca autenticada exige a senha atual; a recuperação sem essa credencial
  exige o token de uso único enviado por e-mail. Ambos usam a mesma política de
  complexidade para a nova senha.
- Confirmação de e-mail e redefinição de senha bloqueiam o token de uso único com
  `FOR UPDATE`; somente uma requisição concorrente pode consumi-lo.
- A solicitação de redefinição bloqueia o usuário por e-mail antes de invalidar o
  token anterior e gravar token + e-mail na outbox, deixando apenas um token
  utilizável mesmo quando duas solicitações chegam juntas.
- Cadastro e troca de e-mail adquirem um advisory lock transacional pela forma
  normalizada do endereço antes da consulta autoritativa de unicidade. O cadastro
  pode fazer uma consulta preliminar fora da transação, mas sempre repete a decisão
  sob o lock. O índice único continua sendo a barreira final, enquanto o lock
  transforma disputas normais pela mesma identidade em uma decisão serializada e
  controlada na Application.
- Quando um e-mail de conta é aceito pelo SMTP, a intenção original é concluída e
  um `email.sent` sanitizado é gravado atomicamente para publicação posterior.
  Destinatário, nome, conteúdo e token não fazem parte do evento de integração.
- Clientes recebem escopo pelo claim de identidade; identificadores enviados no
  corpo não permitem operar em nome de outro usuário.
- CORS aceita somente origens configuradas e rate limiting protege globalmente e
  com limite mais restritivo os endpoints de autenticação.
- O webhook de pagamento limita o corpo a 64 KiB antes de alocar o payload
  completo ou despachar o caso de uso, inclusive quando `Content-Length` não é
  informado.
- Em implantação atrás de um ingress isolado, o processamento opt-in de um único
  salto de `X-Forwarded-For` e `X-Forwarded-Proto` preserva o cliente usado pelo
  rate limiting e o esquema HTTPS. A opção permanece desabilitada por padrão
  para impedir spoofing quando a API recebe tráfego direto.
- Todas as respostas, inclusive falhas, desabilitam MIME sniffing e framing e
  restringem o envio de referrer, câmera, geolocalização e microfone por headers.
- A API usa bearer tokens em headers, não cookies de autenticação; portanto CSRF
  não é aplicável ao modelo atual.
- EF Core parametriza consultas. SQL explícito usa interpolação parametrizada.
- Segredos não são versionados e são validados na inicialização.

## Operação

A API e o Worker possuem logs JSON, OpenTelemetry, métricas Prometheus e health
checks. O Compose sobe PostgreSQL, Redis, RabbitMQ, Azurite, Mailpit, API, Worker,
Prometheus e Grafana. O release publica imagens imutáveis: API no Azure App
Service e Worker no Azure Container Apps.

O CI executa restore, formatação, build, testes, integrações PostgreSQL com
Testcontainers, gate de cobertura superior a 80% para Domain/Application e build
das duas imagens.
