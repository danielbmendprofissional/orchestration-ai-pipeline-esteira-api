# Esteira API — pendências e melhorias

<!-- pendencias: ignorar — os itens abaixo são fatias do mapa (specs/README.md: P1, P2, E1 a E8…); o /proximo já as conta lá, contar aqui seria em dobro -->

Documento de contexto para continuar a evolução do projeto. Atualizado em 2026-10-22, depois do discovery que redesenhou o domínio (ADR-0007, ADR-0008, glossário em `CONTEXT-MAP.md`).

## Contexto do projeto

- **O que é:** API que **decide se cada deploy pode seguir** e **orquestra as promoções no GitLab**: todo Fluxo de deploy começa aqui, a pipeline do template-ci chama a Esteira (`esteira-abrir` · `esteira-gate` · `esteira-resultado`), as aprovações humanas vivem aqui e um bot executa tag/merge/play no GitLab. Vocabulário: `CONTEXT-MAP.md` (Aplicação, Projeto, Fatia, Entrega, Fluxo, Artefato, Ambiente, Destino, Deploy, Análise, Achado, Exceção, Liberação…).
- **Nome:** o projeto se chama **Esteira**. `FluxoDeploy` é apenas a entidade (agregado) do domínio.
- **Arquitetura:** DDD + CQRS.
  - Contexto de escrita `EsteiraContext` e de leitura `EsteiraReadContext` (sem rastreamento) sobre o mesmo schema.
  - Specifications ficam em `Esteira.Data`. Application organizada em Commands/Queries por agregado.
  - Pipeline do MediatR: Validação → Autorização → Cache → Transação.
  - Domain events despachados dentro do `SaveChanges`, na mesma transação.
- **Stack:** .NET 10, EF Core 10 + Npgsql (PostgreSQL), Serilog, OpenAPI + Scalar, MassTransit 8.5.11 (RabbitMQ), MediatR 12.5.0, FluentValidation 12, Quartz 4, xUnit v3 (Microsoft Testing Platform), AwesomeAssertions, NSubstitute. Autenticação: BFF + Keycloak (ADR-0004).
- **Sem dado em uso:** o backend nunca rodou em produção. A refatoração é livre: migrations recomeçam numa base nova, sem compatibilidade com o contrato herdado do sistema legado (`POST /api/artefato`).

## Restrições de licença

- **MediatR:** ficar na 12.5.0 (13+ é comercial).
- **MassTransit:** ficar na 8.x (9 é comercial).
- **FluentAssertions:** substituído por AwesomeAssertions (8+ é comercial).
- Usar apenas pacotes gratuitos ou MIT.

## Refatoração do modelo (discovery 2026-10-21/22)

A fatia M0 (2026-10-22) removeu todo o modelo herdado do sistema legado e deixou o esqueleto; o que está em "Muda" e "Entra" nasce nas fatias de `specs/README.md`.

> **Atualizado em 2026-11-20 para o modelo trunk-based (ADRs 0018 a 0025).** Candidata (`-rcN`), branches `dev`/`qa`/`hml`, AET-1/AET-2, sincronização para baixo e promoção por MR de estágio **não existem mais**. O vocabulário vigente é Fatia → Entrega → Fluxo → Liberação, com Artefato por SHA e versão `X.Y.Z`; as fatias que implementam cada item estão no mapa (P1, P2, E1 a E8, X1, X2, O1).

### Sai
- [x] Etapas **GMUD**, **BD** (CRS de banco), **VERSAO** e a **AET única** — `FluxoDeployGmudEntity`, `FluxoDeployBdCrsEntity`, `FluxoDeployVersaoTarefasEntity`, os `Etapa*Service`/`*Rules` correspondentes, controllers `FluxoDeployBancoDadosCrs*`/`Vinculos` e o que for só deles.
- [x] **DAST próprio** — `IScannerDast`, `ScannerDastNaoConfigurado`, `AnaliseDastJob`, consumers/mensagens DAST, índice "uma análise por URL". Quem roda o ZAP é o template; o resultado chega pelo `esteira-resultado`.
- [x] **`IMigracaoBancoDados`** — migration roda no deploy do template.
- [x] **Regra dos 2 fluxos por sistema** e "deploy vai pro fluxo da etapa da branch".
- [x] **`POST /api/artefato`** (multipart com Semgrep/Trivy do sistema legado) e os DTOs `SemgrepDtoRequest`/`TrivyDtoRequest`.
- [x] **"Somente o criador altera o fluxo"** (`AutorizacaoBehavior`) — substituído pelos papéis por Aplicação.
- [x] **"Liberar fluxo sem validação"** (pendência antiga) — não existe: Esteira é fail-closed sem bypass; o MOR aprova com autoaprovação marcada.
- [x] **"Burla de tag em HML"** (pendência antiga) — resolvida: tags `v*` protegidas e criadas só pelo bot, linhagem validada pelo `controlar-pipeline`, regra de descendência na Esteira.

### Muda
- [ ] **Sistema → Aplicação.** Aplicação aponta para um grupo do GitLab e tem Projetos (um repositório cada, com stack). `ConfiguracaoSistema` vira a **política de severidade da Aplicação por Ambiente** (`LimitesSeveridadeValueObject` por Ambiente; padrão: dev avisa, qa/hml/prd bloqueiam CRITICAL+HIGH).
- [ ] **Entrega por seleção de Fatias** (ADR-0022): uma **Entrega** agrupa 1 ou mais **Fluxos**, um por Projeto tocado, aberta por um humano a partir das Fatias Prontas para Entrega. Tipos normal e hotfix; o tipo da mudança (minor ou major) é declarado na Abertura e a versão `X.Y.Z` só nasce na confirmação em produção (ADR-0023). Fatias: E1, E1b.
- [ ] **Etapas:** normal `Abertura → QA → HML → PRD → Liberação`; hotfix é uma Entrega de tipo hotfix com o Atalho. Não há `DEV`, AET-1, AET-2 nem AAN antes de produção (ADR-0023). Fatias: E1b, E3.
- [ ] **Deploy** de um Artefato (imagem por SHA, gerada pela pipeline do Fluxo) num **Destino** de um Ambiente (`qa`, `hml`, produção); um Deploy por Destino. Fatia: E4.
- [ ] **Análise** sai do Deploy: **do Artefato** (código/imagem, roda uma vez) ou **do Deploy** (smoke, DAST, agente de qa). **Achado**. Fatia: E2.
- [ ] **Concorrência/ocupação:** `qa` sem fila (o Artefato novo supera o anterior), `hml` e produção um Fluxo por vez por Projeto; Projeto "em rollback" não recebe nada até a correção (ADR-0021). Fatia: E4.
- [ ] **Aprovações:** qa e hml pelo aprovador técnico, produção pelo Publicador, Liberação pelo aprovador de negócio; autoaprovação do MOR marcada, com motivo (ADR-0023). Fatias: E3, E7.
- [ ] **Painel de Fatias** (ADR-0024): leitor do contrato de leitura (mapa, spec, issues, bloco de execução), estados da Fatia, review e aprovação da MR. Fatias: P1, P2.
- [ ] **Flags e Liberação** (ADR-0019, 0025): estado do flag por Ambiente na Esteira, job do GitLab, `rollout restart`, detecção de divergência. Fatia: E7.

### Entra
- [ ] **Integração com a pipeline:** `esteira-abrir` (valida Entrega aberta/Fluxo/Ambiente, devolve configuração, Destinos e flags; contrato v2), `esteira-gate` (aplica política + Exceções, responde OK/erro), `esteira-resultado` (202; processado por fila). Autenticação por `id_tokens` do GitLab (JWKS). Fail-closed.
- [ ] **Bot do GitLab** (um por grupo, token só em secret): comentar, fechar, reabrir e mergear a MR da Fatia, disparar os processos da pipeline (`fluxo`, `promocao`, `flag`, `rotulo`, `preview`), aplicar a versão `X.Y.Z`, play do job de produção. Webhook de pipeline para status ao vivo. A `main` só aceita merge do bot (ADR-0021, 0025). Fatias: P2, E4, E7.
- [ ] **Cancelamento** com destino do código (reverter/absorver) e, numa Entrega, cancelar tudo ou desvincular. Fatia: E6.
- [ ] **Rollback** de produção por SHA, só no Ambiente afetado e sem migrations, em ordem inversa à dependência; Projeto "em rollback" até a correção para frente; migração `contract` bloqueia ou avisa (ADR-0025). Fatia: E6.
- [ ] **Exceção** de achado (motivo, pedinte, aprovador, validade) no lugar do `.security-ignore`.
- [ ] **Papéis por Aplicação** (Membro, Aprovador técnico, Aprovador de negócio, Publicador) + **MOR** global (Keycloak). Substitui `AprovadorTecnicoEntity`/equipe e o "perfil de gestão".
- [ ] **Notas de entrega** (oasdiff do OpenAPI, diff de rotas Angular, screenshots do agente de qa, issues; texto por agente).
- [ ] **Notificação por e-mail** + caixa de pendências ("aguardando você"). Depende de SMTP na infra (`docs/mudancas-infra.md`).
- [ ] **Backups pré-publicação** visíveis na tela (os 3 últimos por Aplicação); restore é manual, só MOR.

## Pendências técnicas

### Segurança e acesso
- [ ] **Audit trail.** A Esteira orquestra o GitLab com um token de bot (ADR-0008): no GitLab todo merge/tag aparece como o bot, então quem clicou, quando e por quê só existe aqui. Integrar a aplicação de audit trail que o Daniel já tem (a ser trazida pro workspace). Cobre também metadados (DataCriação, DataAtualização, usuário) e o módulo de atividade.
- [x] **`[Authorize]` nos endpoints** — feito na B3: sessão BFF (ADR-0004) exigida por padrão (`FallbackPolicy`). Falta (E1): endpoints da pipeline (`/api/pipeline/v1`) autenticados por `id_token` do GitLab, com esquema próprio e `[AllowAnonymous]` para a sessão.
- [ ] **Endurecimento da sessão (revisão de segurança da B3, itens aceitos ou fora do código).** (a) `invalid_grant` no refresh revoga todas as sessões do usuário, também quando é só refresh expirado/SSO ocioso — risco aceito: derruba os outros dispositivos, mas o Keycloak não distingue reuso de expiração de forma estável; revisar se incomodar. (b) Rate limit por IP em `/api/v1/auth/*` e cache curto no `/health` (hoje anônimo e dispara Postgres/Redis/RabbitMQ/Keycloak a cada chamada). (c) `/metrics` e `/health` restritos por rede/Ingress em produção. (d) Cookie com prefixo `__Host-` (exige `Secure`, só fora de Development) e HSTS. (`ForwardedHeaders` já está configurado fora de Development; no `deploy/k8s` o Service da API só pode ser alcançado pelo Ingress — e o Ingress precisa mandar `X-Forwarded-Proto` —, senão qualquer cliente falsifica esquema e IP no log.) (e) `Keycloak:Authority` e `ConnectionStrings:Redis` com TLS (`ssl=true`) nos ambientes publicados — pedido de infra; hoje os tokens ficam em texto no Redis, protegidos só pela ACL. (f) Conferir `aud`/`iss` do `id_token` além do `nonce`. (g) `dotnet list package --vulnerable` no CI.
- [ ] **Status de erro do provedor fora do ar.** `auth.provedor-indisponivel` (Keycloak indisponível no refresh) sai como `Failure` → 422 porque o mapa de status da ADR-0003 não tem 502/503. Avaliar um tipo de erro próprio quando o front tratar retry.
- [ ] **Endpoint de permissões para o front.** `GET /api/fluxo-deploy/{id}/permissoes` devolvendo o que o usuário pode fazer, calculado pelas mesmas regras do backend (papéis da Aplicação + MOR).

### Mensageria e eventos
- [x] **Outbox e inbox com tabela** (ADR-0010) — feito na M0 (`MassTransit.EntityFrameworkCore` + Postgres), coberto por `Esteira.Integration.Tests`.
- [ ] **Integration events.** `IIntegrationEvent` gerado pelos domain event handlers e publicado pelo outbox após o commit (ex.: fluxo concluído → avisar outros sistemas).
- [ ] **Retry** nos consumers que só gravam no banco (processamento de relatórios).
- [ ] **Exchanges por ambiente.** Hoje é sufixo `-hml`/`-prd` no nome. Avaliar virtual host por ambiente.

### Observabilidade e performance
- [ ] **Disponibilidade é requisito de produção:** com fail-closed, Esteira fora do ar = nada publica. Health, alerta e runbook à altura (`docs/agents/observabilidade-backup.md`).
- [ ] **Aquecimento e cold start.** As primeiras chamadas de cada consulta custam mais (JIT e compilação de consultas do EF). Avaliar ReadyToRun no publish e aquecimento das consultas em segundo plano no startup.
- [ ] **Cache centralizado.** Já existe `ICacheService` + `CacheBehavior`; candidatos: papéis por Aplicação, política de severidade, JWKS do GitLab.

### Qualidade
- [x] **Testes de integração** — `Esteira.Integration.Tests` (Testcontainers) criado na M0; cada fatia nova acrescenta os seus.
- [ ] **Resumo para listagens** — a view `VW_FLUXO_DEPLOY_RESUMO` saiu na M0; a listagem de Entregas/Fluxos (E1) decide se volta como view ou projeção.

## Como rodar

Pré-requisito: infra compartilhada no ar e os segredos da casa `esteira` no user-secrets da API (uma vez, e de novo depois de cada `apply` que troque segredo):

```powershell
node <ai-infra>\configs\infra\infra-config.mjs secrets esteira --csproj=src\Esteira.Api\Esteira.Api.csproj
```

```bash
dotnet build Esteira.slnx
dotnet test --solution Esteira.slnx
dotnet tool restore
dotnet ef database update --project src/Esteira.Data --startup-project src/Esteira.Api --context EsteiraContext   # usa ConnectionStrings:PostgresMigrations (esteira_owner)
dotnet run --project src/Esteira.Api --launch-profile http   # http://localhost:5105 — /health, /metrics, /scalar/v1
```

**Login em dev (sessão BFF).** Precisa do Redis e do Keycloak da infra no ar e do `Keycloak:ClientSecret` no user-secrets. O `redirect_uri` é `http://localhost:4200/api/v1/auth/callback` (`Autenticacao:UrlPublica`), então a API é acessada pela origem do front: `ng serve` em `esteira-frontend` (o `proxy.conf.json` manda `/api` para `localhost:5105`). Abra `http://localhost:4200/api/v1/auth/login`, entre com o usuário `dev` (senha em `PROJECT_ESTEIRA_KEYCLOAK_DEV_PASSWORD`, no env da infra) e confira `http://localhost:4200/api/v1/usuarios/me` (`perfis` com `mor`). Logout: `/api/v1/auth/logout`. Suba a API pelo perfil `http` (porta 5105): em Development a API **não** redireciona para HTTPS, porque o login do BFF precisa ficar na origem `4200` do proxy — um redirect para a `7239` do perfil `https` leva o navegador para outra origem e o callback termina em 401. Fora de Development o redirect vale e a API confia nos `X-Forwarded-For`/`X-Forwarded-Proto` do Ingress.

Configuração: só as chaves do contrato (`../docs/agents/infra-local.md`: `ConnectionStrings:Postgres`/`PostgresMigrations`, `RabbitMQ:*`, `Keycloak:*`, `OTEL_*`), vindas do user-secrets em dev. No `appsettings.json` fica só o que é do projeto: `Serilog`, `Autenticacao` (claims, HTTPS, `UrlPublica`), `RabbitMQ:Ambiente`, `Cors`. Sem chave obrigatória, a API não sobe e diz qual falta.
