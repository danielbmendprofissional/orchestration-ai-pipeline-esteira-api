# esteira-api

API da Esteira (.NET 10, DDD + CQRS). Vocabulário do domínio: `../CONTEXT-MAP.md` — nomes de classe, tabela e rota usam esses termos (Aplicação, Projeto, Entrega, Fluxo, Candidata, Ambiente, Destino, Deploy, Análise, Achado, Exceção). Decisões: `../docs/adr/`. Estado e backlog: `docs/PENDENCIAS.md` — o código ainda carrega o modelo herdado do sistema legado; a seção "Refatoração do modelo" diz o que sai, muda e entra.

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
- **Erros:** handler retorna `Result<T>` com `code` estável (`<area>.<motivo>`, ex.: `ambiente.ocupado`) — ADR-0003. `DomainException` é legado até a fatia B2.
- **Escrita** por repositório + `IUnitOfWork` no `EsteiraContext`; **leitura** por query repository no `EsteiraReadContext` projetando `QueryModel`. Paginação e ordenação no banco (`Skip`/`Take` na query) com headers `X-*` (ADR-0002); `BaseHandler.Paginacao` pagina em memória e é legado.
- **IDs:** `long Id` para chave e join, `Guid PublicId` (v7) em tudo que sai da API — ADR-0014. Rota recebe GUID e resolve para `long` uma vez.
- **Invariante entre agregados** vira restrição no banco (índice único + concorrência otimista), como a ocupação do Ambiente — ADR-0009.
- **Sistema externo** só atrás de porta em `Application/Interfaces/Integracoes`, adaptador no `AcL`. Escrita no GitLab só como **Ação do bot** gravada na transação (outbox) e executada por consumer — ADR-0010.
- **Duas superfícies:** front em `/api/v1/...` (sessão, ADR-0004); pipeline em `/api/pipeline/v1/...` (`id_token`, contrato versionado, ADR-0013). Relatório grande da pipeline: o endpoint valida, publica na fila e responde 202; o consumer processa.
- **Permissão** sai dos papéis da Aplicação + role `mor` (`CONTEXT-MAP.md`, Papéis), calculada no backend; o front só pergunta (`/permissoes`).
- **Configuração:** só as chaves do contrato em `../docs/agents/infra-local.md`, segredo em user-secrets. Chave nova = pedido em `../docs/mudancas-infra.md`.

## Pacotes

Só gratuitos/MIT: MediatR fica na 12.5.0, MassTransit na 8.x, asserções com AwesomeAssertions.

## Testes

xUnit v3 + AwesomeAssertions + NSubstitute. Um teste por critério EARS da spec. Regra de negócio: `Esteira.Domain.Tests`. Handler/orquestração: `Esteira.Application.Tests` com portas substituídas. Banco, índice único e concorrência: integração com Testcontainers (PostgreSQL real).

```bash
dotnet build Esteira.slnx
dotnet test --solution Esteira.slnx
```
