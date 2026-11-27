using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Eventos;
using MassTransit;
using MediatR;

namespace Esteira.Application.Eventos
{
    // Mensagem do outbox: registrar (ou atualizar) o hook do GitLab no Projeto (Id interno). Só identificador.
    public sealed record RegistrarWebhookPedido(long ProjetoId);

    // Configuração do hook: `GitLab:Webhook:Secret` e `Webhook:UrlPublica` (só contrato de configuração; nunca em log).
    public interface IOpcoesDoWebhook
    {
        string? Segredo { get; }

        // Base pública da API que o GitLab alcança (sem a rota).
        string? UrlPublica { get; }
    }

    public sealed class WebhookProjetoDto
    {
        public string Estado { get; set; } = default!;
        public string? CodigoErro { get; set; }
        public DateTime AtualizadaEm { get; set; }
    }

    public static class WebhookErros
    {
        public const string RotaDoWebhook = "/api/webhooks/gitlab";
        public const string BotNaoConfigurado = "webhook.bot-nao-configurado";
        public const string SemPermissao = "webhook.sem-permissao";
        public const string GitLabIndisponivel = "webhook.gitlab-indisponivel";

        public static readonly Erro NaoReexecutavel = new(
            "webhook.nao-reexecutavel", "O registro do webhook só pode ser reexecutado quando falhou.", TipoErro.Conflict);

        public static WebhookProjetoDto Dto(WebhookProjetoEntity w) => new()
        {
            Estado = EnumKebab.Texto(w.Estado),
            CodigoErro = w.CodigoErro,
            AtualizadaEm = w.AtualizadaEm
        };
    }

    // Registro do hook no GitLab (consumer do outbox). Idempotente: lista os hooks do Projeto, atualiza o que tem a
    // mesma URL e só cria quando não há. Falha vira `falhou` com código, sem derrubar o cadastro do Projeto.
    // Só age sobre webhook `pendente`: mensagem repetida de um webhook já resolvido não chama o GitLab.
    public sealed class RegistradorDeWebhook(
        IWebhookProjetoRepositorio webhooks,
        IProjetoRepositorio projetos,
        IAplicacaoRepositorio aplicacoes,
        IGitLabHooks gitlab,
        IOpcoesDoWebhook opcoes,
        IUnitOfWork uow,
        TimeProvider relogio)
    {
        public async Task RegistrarAsync(long projetoId, CancellationToken cancellationToken = default)
        {
            var webhook = await webhooks.ObterPorProjetoAsync(projetoId, cancellationToken);
            if (webhook is null || webhook.Estado != EstadoWebhookProjeto.Pendente)
                return;

            var agora = relogio.GetUtcNow().UtcDateTime;
            var projeto = await projetos.ObterPorIdAsync(projetoId, cancellationToken);
            var aplicacao = projeto is null ? null : await aplicacoes.ObterAsync(projeto.AplicacaoId, cancellationToken);
            if (projeto is null || aplicacao is null)
                return;

            if (string.IsNullOrWhiteSpace(opcoes.Segredo) || string.IsNullOrWhiteSpace(opcoes.UrlPublica))
            {
                await FalharAsync(webhook, WebhookErros.BotNaoConfigurado, agora);
                return;
            }

            var hook = new HookParaRegistrar(opcoes.UrlPublica.Trim().TrimEnd('/') + WebhookErros.RotaDoWebhook, opcoes.Segredo);
            var grupo = aplicacao.GrupoGitLabCaminho;

            var existentes = await gitlab.ListarAsync(grupo, projeto.ProjetoGitLabId, cancellationToken);
            if (!existentes.IsSuccess)
            {
                await FalharAsync(webhook, CodigoDoWebhook(existentes.Errors), agora);
                return;
            }

            var mesmaUrl = existentes.Value.FirstOrDefault(h => string.Equals(h.Url?.Trim(), hook.Url, StringComparison.OrdinalIgnoreCase));
            long hookId;
            if (mesmaUrl is not null)
            {
                var atualizado = await gitlab.AtualizarAsync(grupo, projeto.ProjetoGitLabId, mesmaUrl.Id, hook, cancellationToken);
                if (!atualizado.IsSuccess)
                {
                    await FalharAsync(webhook, CodigoDoWebhook(atualizado.Errors), agora);
                    return;
                }

                hookId = mesmaUrl.Id;
            }
            else
            {
                var criado = await gitlab.CriarAsync(grupo, projeto.ProjetoGitLabId, hook, cancellationToken);
                if (!criado.IsSuccess)
                {
                    await FalharAsync(webhook, CodigoDoWebhook(criado.Errors), agora);
                    return;
                }

                hookId = criado.Value;
            }

            webhook.Registrar(hookId, agora);
            await uow.SaveChangesAsync();
        }

        private async Task FalharAsync(WebhookProjetoEntity webhook, string codigo, DateTime agora)
        {
            webhook.Falhar(codigo, agora);
            await uow.SaveChangesAsync();
        }

        private static string CodigoDoWebhook(IReadOnlyList<Erro> erros)
            => erros.FirstOrDefault()?.Code switch
            {
                "gitlab.bot-nao-configurado" => WebhookErros.BotNaoConfigurado,
                "gitlab.sem-permissao" => WebhookErros.SemPermissao,
                _ => WebhookErros.GitLabIndisponivel
            };
    }

    // Partida: enfileira o registro dos Projetos ativos sem WebhookProjeto (os cadastrados antes da T1).
    public sealed class PartidaDoWebhook(
        IWebhookProjetoRepositorio webhooks, IUnitOfWork uow, IPublishEndpoint publicador, TimeProvider relogio)
    {
        public Task<IReadOnlyList<long>> ListarPendentesAsync(CancellationToken cancellationToken = default)
            => webhooks.ListarProjetosAtivosSemWebhookAsync(cancellationToken);

        // Cria a linha `pendente` e a mensagem na mesma transação; false se o Projeto já tem linha.
        public async Task<bool> EnfileirarAsync(long projetoId, CancellationToken cancellationToken = default)
        {
            if (await webhooks.ObterPorProjetoAsync(projetoId, cancellationToken) is not null)
                return false;

            await webhooks.AdicionarAsync(WebhookProjetoEntity.Criar(projetoId, relogio.GetUtcNow().UtcDateTime), cancellationToken);
            await publicador.Publish(new RegistrarWebhookPedido(projetoId), cancellationToken);
            await uow.SaveChangesAsync();
            return true;
        }
    }

    public sealed record ReexecutarWebhookCommand(Guid ProjetoId) : IRequest<Result<WebhookProjetoDto>>, ITransactionRequest;

    // Só o MOR (OperacaoAplicacao.EditarAplicacao). Webhook que não está `falhou` (ou não existe) é 409.
    public sealed class ReexecutarWebhookHandler(
        IAutorizacaoAplicacao autorizacao,
        IProjetoRepositorio projetos,
        IWebhookProjetoRepositorio webhooks,
        IPublishEndpoint publicador,
        TimeProvider relogio) : IRequestHandler<ReexecutarWebhookCommand, Result<WebhookProjetoDto>>
    {
        public async Task<Result<WebhookProjetoDto>> Handle(ReexecutarWebhookCommand request, CancellationToken cancellationToken)
        {
            var projeto = await projetos.ObterPorPublicIdAsync(request.ProjetoId, cancellationToken);
            if (projeto is null)
                return ProjetoErros.NaoEncontrado;

            var autorizado = await autorizacao.AutorizarAsync(projeto.AplicacaoId, OperacaoAplicacao.EditarAplicacao, cancellationToken);
            if (!autorizado.IsSuccess)
                return Result<WebhookProjetoDto>.Falha(autorizado.Errors.ToArray());

            var webhook = await webhooks.ObterPorProjetoAsync(projeto.Id, cancellationToken);
            if (webhook is null || !webhook.Reexecutar(relogio.GetUtcNow().UtcDateTime).IsSuccess)
                return WebhookErros.NaoReexecutavel;

            await publicador.Publish(new RegistrarWebhookPedido(projeto.Id), cancellationToken);
            return Result<WebhookProjetoDto>.Sucesso(WebhookErros.Dto(webhook));
        }
    }
}
