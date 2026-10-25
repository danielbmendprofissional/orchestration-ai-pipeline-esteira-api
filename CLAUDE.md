# esteira-api

API da Esteira (.NET 10, DDD + CQRS). Vocabulário do domínio: `../CONTEXT-MAP.md` — nomes de classe, tabela e rota usam esses termos (Aplicação, Projeto, Entrega, Fluxo, Candidata, Ambiente, Destino, Deploy, Análise, Achado, Exceção). Decisões: `../docs/adr/`. Estado e backlog: `docs/PENDENCIAS.md`. O código é um esqueleto (fatia M0): infraestrutura transversal sem agregado de negócio; os agregados nascem nas fatias A1 e E1, test-first.

## Camadas

Dependência aponta para dentro: `Domain` ← `Application` ← `Data` · `AcL` · `Messaging` · `Infra.Cache`. `Api` expõe, `IoC` compõe.

| Projeto | Mora aqui |
|---|---|
| `Esteira.Domain` | agregados (`*Entity`), value objects (`*ValueObject`), serviços de domínio, domain events, interfaces de repositório de escrita. **Toda regra de negócio.** |
| `Esteira.Application` | Commands/Queries/Handlers/Validators por agregado (`<Agregado>/Commands/<Acao>/`), behaviors do MediatR, portas (`Interfaces/Integracoes`), interfaces de query repository, DTOs |
| `Esteira.Data` | `EsteiraContext` (escrita) e `EsteiraReadContext` (leitura sem tracking), configurations `Write/` e `Read/`, `QueryModels`, `Specifications`, repositórios, migrations (`Common/Persistence/Migracoes`) |
| `Esteira.AcL` | adaptadores das portas: GitLab, e-mail — tudo que fala com sistema externo |
| `Esteira.Messaging` | consumers do RabbitMQ (interno) |
| `Esteira.Api` | controllers finos (montam command/query, devolvem resultado), middleware, autenticação |

## Padrões

- **Handler orquestra, domínio decide.** O handler carrega agregados, chama o método do domínio e salva; `if` de regra de negócio mora na entidade ou no serviço de domínio, onde o teste de Domain alcança.
- **Erros** (ADR-0003): handler e fábrica de VO (`Criar`) devolvem `Result`/`Result<T>` com `Erro(code, message, TipoErro)`; `code` estável `<area>.<motivo>` em kebab-case (`ambiente.ocupado`, `url.invalida`). A Api traduz em status + `{ errors: [...] }`; só o inesperado vira Problem Details 500 com `traceId`. `Guard` é só para erro de programação (`ArgumentException`/`ArgumentNullException`), nunca para entrada de usuário. Todo validator do FluentValidation declara `.WithErrorCode(...)` (sem ele sai `validacao.campo-invalido`). Não existem exceptions de negócio.
- **Escrita** por repositório + `IUnitOfWork` no `EsteiraContext`; **leitura** por query repository no `EsteiraReadContext` projetando `QueryModel`. Paginação e ordenação no banco (`Skip`/`Take` na query) com headers `X-*` (ADR-0002): a consulta de lista recebe `PaginacaoDto` (lido por `Request.ObterPaginacao()`), chama `PaginarAsync` com a ordenação padrão obrigatória (projeção member-init) e devolve `Result<PaginaDto<T>>`; o filtro de resultado escreve o array puro e os headers.
- **IDs:** `long Id` para chave e join, `Guid PublicId` (v7, gerado no `EntityBase`) em tudo que sai da API — ADR-0014. Rota recebe GUID e resolve para `long` uma vez.
- **Banco:** tudo no schema `esteira` (ADR-0016); contexto liga ao Postgres só por `EsteiraPostgres.UsarPostgres`. Migrations com `esteira_owner`, runtime com `esteira_app` (sem DDL).
- **Datas** em UTC (`DateTime` UTC ou `DateTimeOffset`), coluna `timestamptz` — padrão do Npgsql, o mesmo do outbox.
- **Invariante entre agregados** vira restrição no banco (índice único + concorrência otimista), como a ocupação do Ambiente — ADR-0009.
- **Sistema externo** só atrás de porta em `Application/Interfaces/Integracoes`, adaptador no `AcL`. Escrita no GitLab só como **Ação do bot** gravada na transação (outbox) e executada por consumer — ADR-0010.
- **Mensageria:** registro único em `Esteira.IoC/Mensageria.cs` (`AddMensageria`), usado pela API e pelos testes de integração. Publicar dentro de um command pelo `IPublishEndpoint` do escopo: a mensagem vai para a tabela de outbox no mesmo commit. Consumer novo entra pelo parâmetro de consumidores.
- **Duas superfícies:** front em `/api/v1/...` (sessão, ADR-0004); pipeline em `/api/pipeline/v1/...` (`id_token`, contrato versionado, ADR-0013). Relatório grande da pipeline: o endpoint valida, publica na fila e responde 202; o consumer processa.
- **Sessão BFF** (ADR-0004): o navegador só tem o cookie opaco `esteira_session`; tokens do Keycloak no Redis (`ISessaoStore`, adaptador em `Infra.Cache`, só comandos simples e chaves com `Redis:KeyPrefix` — a ACL nega `SCAN`/`KEYS`). Fluxo OIDC próprio em `Esteira.Api/Autenticacao` (`FluxoLogin`, `FluxoSessao`, `IProvedorOidc`), esquema `Sessao` alimenta `IUsuarioAtual` (`Id`, `Usuario`, `Nome`, `Email`, `Perfis`, `EhMor`). **Sessão é exigida por padrão** (`FallbackPolicy`): endpoint público declara `[AllowAnonymous]`, rota mapeada (`MapHealthChecks`...) declara `.AllowAnonymous()`. Access token vencido → 401 `auth.access-token-expired` (sem refresh síncrono; o front chama `POST /auth/refresh`, que é `[AllowAnonymous]` de propósito: com o access token vencido a política padrão devolveria 401 e o refresh seria impossível — quem vale é o cookie, conferido no Redis). Login em andamento é amarrado ao navegador pelo cookie curto `esteira_login` (anti login CSRF); `id`/`state` vindos do cliente só entram em chave do Redis depois de `IdentificadorSeguro`. Nunca logar nem devolver token.
- **Permissão** sai dos papéis da Aplicação + role `mor` (`CONTEXT-MAP.md`, Papéis), calculada no backend; o front só pergunta (`/permissoes`).
- **Configuração:** só as chaves do contrato em `../docs/agents/infra-local.md`, segredo em user-secrets. Chave nova = pedido em `../docs/mudancas-infra.md`.

## Pacotes

Só gratuitos/MIT: MediatR fica na 12.5.0, MassTransit na 8.x, asserções com AwesomeAssertions.

## Testes

xUnit v3 + AwesomeAssertions + NSubstitute. Um teste por critério EARS da spec. Regra de negócio: `Esteira.Domain.Tests`. Handler/orquestração: `Esteira.Application.Tests` com portas substituídas. Banco, índice único, concorrência e mensageria: `Esteira.Integration.Tests` (Testcontainers com PostgreSQL, RabbitMQ e Redis reais — o Redis com a ACL da infra —, schema pelas migrations; precisa do Docker rodando). Teste HTTP de sessão usa o `ProvedorOidcFalso` no lugar do Keycloak; rota de teste que não deve exigir sessão leva `[AllowAnonymous]`..

```bash
dotnet build Esteira.slnx
dotnet test --solution Esteira.slnx
```
