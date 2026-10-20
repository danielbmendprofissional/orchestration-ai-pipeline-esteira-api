# Esteira API — pendências e melhorias

Documento de contexto para continuar a evolução do projeto. Atualizado em 2026-10-20.

## Contexto do projeto

- **O que é:** API que controla o fluxo de deploy de um sistema por etapas (abertura, versão das tarefas, GMUD, banco de dados, aprovação técnica, DEV, QA, HML, área de negócio, PRD), recebendo os deploys e as análises de segurança das pipelines.
- **Nome:** o projeto se chama **Esteira**. `FluxoDeploy` é apenas a entidade (agregado) do domínio.
- **Arquitetura:** DDD + CQRS.
  - Agregados `FluxoDeploy` (etapas, CRS, aprovações técnicas e de negócio, GMUD, versão das tarefas) e `Deploy` (análises → artefatos).
  - Contexto de escrita `EsteiraContext` e de leitura `EsteiraReadContext` (sem rastreamento) sobre o mesmo schema.
  - Specifications ficam em `Esteira.Data`. Application organizada em Commands/Queries por agregado.
  - Pipeline do MediatR: Validação → Autorização → Cache → Transação.
  - Domain events (`FluxoDeployConcluido`, `FluxoDeployCancelado`) são despachados dentro do `SaveChanges`, na mesma transação.
- **Stack:** .NET 10, EF Core 10 + Npgsql (PostgreSQL), JWT Bearer, Serilog, OpenAPI + Scalar, MassTransit 8.5.11 (RabbitMQ), MediatR 12.5.0, FluentValidation 12, Quartz 4, xUnit v3 (Microsoft Testing Platform), AwesomeAssertions, NSubstitute.
- **Banco:** migrations em `src/Esteira.Data/Common/Persistence/Migracoes`. A view de resumo está em `scripts/banco/views/VW_FLUXO_DEPLOY_RESUMO.sql` e é embutida na migration. Script completo em `scripts/banco/esteira.sql`.
- **Integrações:** ficam atrás de portas em `Application/Interfaces/Integracoes`; os adaptadores atuais em `Esteira.AcL/Integracoes` são "não configurados" e respondem **501**.
  - `IGerenciadorTarefas`: só `ObterTarefas(ids)`, usado na etapa AAN para conferir se as tarefas vinculadas estão aprovadas.
  - `IScannerDast`: execução e resultado da análise DAST.
  - `IMigracaoBancoDados`: reservada para migração automática de banco.

## Regras importantes já implementadas

- Dois fluxos abertos por sistema, no máximo. Um fluxo nunca entra numa etapa que o outro está executando, e o mais novo não entra em PRD antes do mais antigo concluir.
- Voltar etapa com outro fluxo aberto: quem está em HML/AAN/PRD só volta dentro desses três.
- Deploy com dois fluxos: vai para o fluxo com a etapa da branch em execução; a versão é validada por projeto.
- Nova versão das tarefas (projeto ou versão diferente) apaga as aprovações de negócio vinculadas.
- Etapa AAN exige ao menos uma tarefa vinculada e todas aprovadas.
- Aprovação técnica só com a etapa AET em execução; o criador do fluxo não pode aprovar; o mesmo aprovador pode alterar o próprio veredito.
- Somente o criador do fluxo altera o fluxo (`AutorizacaoBehavior`, 403).
- Análise DAST: uma por URL ao mesmo tempo (verificação + índice único parcial). Pedido para URL já em scan é ignorado e registrado em log.

## Restrições de licença

- **MediatR:** ficar na 12.5.0 (13+ é comercial).
- **MassTransit:** ficar na 8.x (9 é comercial).
- **FluentAssertions:** substituído por AwesomeAssertions (8+ é comercial).
- Usar apenas pacotes gratuitos ou MIT.

## Pendências

### Segurança e acesso
- [ ] **`[Authorize]` nos endpoints.** Hoje todos estão abertos; só a regra "somente o criador altera" protege as alterações. Exigir autenticação quando o provedor de identidade for definido.
- [ ] **Permissões completas.** Perfil de gestão (diretoria/supervisão de TI) via claim do JWT com acesso total; definir quem é o aprovador de negócio e restringir à etapa AAN.
- [ ] **"Liberar fluxo" sem validação**, só para gestão, com auditoria obrigatória (quem, quando, por quê).
- [ ] **Burla de tag em HML.** A API valida só o nome da tag: cortando uma tag com a versão do fluxo 1 em cima do código do fluxo 2, o código entra no HML do fluxo 1. Opções:
  1. Enviar o SHA do commit no deploy e só aceitar em HML/PRD um SHA que já passou por DEV/QA no mesmo fluxo e projeto (coluna nova).
  2. Consultar o GitLab no deploy e rejeitar a tag cujo commit contém o commit deployado pelo outro fluxo.
  3. Processo: tags e branches protegidas, MR obrigatório.

### Integrações
- [ ] **Gerenciador de tarefas (GitLab).** Implementar `IGerenciadorTarefas` em `Esteira.AcL`, vinculando issues/specs/features do GitLab ao fluxo. Sem isso a etapa AAN responde 501.
- [ ] **Scanner DAST.** Implementar `IScannerDast` com a ferramenta escolhida (ex.: OWASP ZAP). Avaliar cancelamento forçado de scan para não sobrecarregar.
- [ ] **Migração automática de banco.** Implementar `IMigracaoBancoDados` e ligar à etapa de banco de dados.
- [ ] **IA no GitLab.** Code-review-agent em DEV e qa-agent/smoke em QA, enviando o resultado para a API como uma análise do fluxo.

### Mensageria e eventos
- [ ] **Outbox e inbox com tabela.** Hoje é em memória (`UseInMemoryInboxOutbox`), então reiniciar o processo perde o controle. Com EF Core 10 o `MassTransit.EntityFrameworkCore` passa a ser compatível; avaliar com PostgreSQL.
- [ ] **Integration events.** `IIntegrationEvent` gerado pelos domain event handlers e publicado pelo outbox após o commit (ex.: fluxo concluído → avisar outros sistemas).
- [ ] **Retry.** Avaliar `UseMessageRetry` nos consumers que só gravam no banco. Nunca no consumer DAST, que dispara scan antes de gravar.
- [ ] **Exchanges por ambiente.** Hoje é sufixo `-hml`/`-prd` no nome. Avaliar virtual host por ambiente.

### Domínio e funcionalidades
- [ ] **Tecnologias e projetos por fluxo.** Cada fluxo declara as tecnologias e os projetos (ex.: Angular → todos; Java → os projetos Java da aplicação). A etapa de ambiente só conclui com todos os deploys de todas as tecnologias concluídos, e o usuário sempre vê quais deploys fazem parte do fluxo. Exige tabela nova. Também permitiria mais de 2 fluxos por sistema (por tecnologia).
- [ ] **Metadados e módulo de atividade.** DataCriação, DataAtualização e usuário em todas as alterações (interceptor do EF) e um módulo de logs/atividade: gestão vê tudo, usuário vê só o seu.
- [ ] **GMUD e versão das tarefas sem trava de etapa.** Hoje podem ser atualizadas em qualquer etapa com o fluxo em execução. Decidir se só devem ser alteradas com a etapa correspondente em execução, e se a data de execução da GMUD deve mudar na atualização.
- [ ] **Endpoint de permissões para o front.** `GET /api/fluxo-deploy/{id}/permissoes` devolvendo o que o usuário pode fazer, calculado pelas mesmas regras do backend.

### Observabilidade e performance
- [ ] **Saída de logs.** Serilog hoje escreve só no console; definir o destino (arquivo, Seq, Loki, Grafana etc.).
- [ ] **Cache da configuração de autenticação (JWKS)**, se o provedor exigir.
- [ ] **Aquecimento e cold start.** As primeiras chamadas de cada consulta custam mais (JIT e compilação de consultas do EF). Avaliar ReadyToRun no publish e aquecimento das consultas em segundo plano no startup.
- [ ] **Cache centralizado.** Já existe `ICacheService` + `CacheBehavior`; levantar outras consultas candidatas (aprovadores por equipe, configuração do sistema).

### Qualidade
- [ ] **Testes de integração.** Os testes atuais são de Domain e Application com mocks. Criar testes da camada Data e da API contra PostgreSQL real (ex.: Testcontainers).
- [ ] **Testes do consumer DAST.** Cobrir: URL ocupada não chama o scanner; URL livre chama e registra; falha do scanner marca erro.
- [ ] **Conferir a view de resumo.** Foi reescrita para PostgreSQL; validar se o formato dos textos agregados é o esperado pelo front.

## Como rodar

```bash
dotnet build Esteira.slnx
dotnet test --solution Esteira.slnx
dotnet tool restore
ESTEIRA_CONNECTION="Host=localhost;Database=esteira;Username=postgres;Password=postgres" \
  dotnet ef database update --project src/Esteira.Data --startup-project src/Esteira.Api --context EsteiraContext
```

Configurações em `src/Esteira.Api/appsettings.json`: `ConnectionStrings:EsteiraConnection`, `Autenticacao`, `RabbitMQ`, `GerenciadorTarefas`, `Cors`. Documentação da API em `/scalar/v1` no ambiente Development.
