using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using FluentValidation;
using MediatR;

namespace Esteira.Application.Aplicacoes.Commands.CadastrarProjeto
{
    // ProjetoGitLabId é nullable de propósito: a ausência cai no validator (code estável), não no 400 genérico.
    public sealed record CadastrarProjetoCommand(Guid AplicacaoId, long? ProjetoGitLabId, string Stack)
        : IRequest<Result<ProjetoResumoDto>>, ITransactionRequest;

    public sealed class CadastrarProjetoValidator : AbstractValidator<CadastrarProjetoCommand>
    {
        public CadastrarProjetoValidator()
        {
            RuleFor(x => x.ProjetoGitLabId).NotNull().WithErrorCode("projeto.gitlab-id-obrigatorio").WithMessage("Informe o projeto do GitLab.");
            RuleFor(x => x.Stack)
                .Must(s => EnumKebab.TentarLer<Stack>(s, out _))
                .WithErrorCode("projeto.stack-invalida")
                .WithMessage("Stack inválida. Use dotnet, angular ou outro.");
        }
    }

    public sealed class CadastrarProjetoHandler(
        IAcessoAplicacaoQuery acesso,
        IAutorizacaoAplicacao autorizacao,
        IAplicacaoRepositorio aplicacoes,
        IProjetoRepositorio projetos,
        IGitLab gitlab,
        IUnitOfWork uow,
        IAuditoria auditoria,
        Esteira.Domain.Eventos.IWebhookProjetoRepositorio webhooks,
        MassTransit.IPublishEndpoint publicador,
        TimeProvider relogio) : IRequestHandler<CadastrarProjetoCommand, Result<ProjetoResumoDto>>
    {
        public async Task<Result<ProjetoResumoDto>> Handle(CadastrarProjetoCommand request, CancellationToken cancellationToken)
        {
            var aplicacaoId = await acesso.ResolverIdAsync(request.AplicacaoId, cancellationToken);
            if (aplicacaoId is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var autorizado = await autorizacao.AutorizarAsync(aplicacaoId.Value, OperacaoAplicacao.EditarCadastro, cancellationToken);
            if (!autorizado.IsSuccess)
                return Result<ProjetoResumoDto>.Falha(autorizado.Errors.ToArray());

            var aplicacao = await aplicacoes.ObterAsync(aplicacaoId.Value, cancellationToken);
            if (aplicacao is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var gitlabId = request.ProjetoGitLabId!.Value;

            var doGrupo = await gitlab.ListarProjetosDoGrupoAsync(aplicacao.GrupoGitLabCaminho, aplicacao.GrupoGitLabId, cancellationToken);
            if (!doGrupo.IsSuccess)
                return Result<ProjetoResumoDto>.Falha(doGrupo.Errors.ToArray());

            var projetoDoGitLab = doGrupo.Value.FirstOrDefault(p => p.Id == gitlabId);
            if (projetoDoGitLab is null)
                return ProjetoErros.ForaDoGrupo;

            if (await projetos.ProjetoGitLabCadastradoAsync(gitlabId, cancellationToken))
                return ProjetoErros.Duplicado;

            EnumKebab.TentarLer<Stack>(request.Stack, out var stack);
            var projeto = ProjetoEntity.Criar(aplicacaoId.Value, gitlabId, projetoDoGitLab.Caminho, projetoDoGitLab.Nome, stack);
            await projetos.AdicionarAsync(projeto, cancellationToken);

            // Grava já: os Ambientes precisam do Id do Projeto, e a corrida de unicidade aparece aqui.
            try
            {
                await uow.SaveChangesAsync();
            }
            catch (ViolacaoDeUnicidadeException violacao) when (ProjetoErros.DaViolacao(violacao) is not null)
            {
                return Result<ProjetoResumoDto>.Falha(ProjetoErros.DaViolacao(violacao)!);
            }

            foreach (var ambiente in Enum.GetValues<AmbienteDeploy>())
            {
                var criado = AmbienteProjetoEntity.Criar(projeto.Id, ambiente);
                await projetos.AdicionarAmbienteAsync(criado.Value, cancellationToken);
            }

            // T1.4: o registro do hook no GitLab é uma Ação do bot: linha pendente + mensagem no outbox, na mesma transação.
            await webhooks.AdicionarAsync(Esteira.Domain.Eventos.WebhookProjetoEntity.Criar(projeto.Id, relogio.GetUtcNow().UtcDateTime), cancellationToken);
            await publicador.Publish(new Esteira.Application.Eventos.RegistrarWebhookPedido(projeto.Id), cancellationToken);

            auditoria.Registrar("Projeto", projeto.PublicId, AcaoAuditoria.Criou, null, new
            {
                projeto.AplicacaoId,
                projeto.ProjetoGitLabId,
                projeto.Caminho,
                projeto.Nome,
                Stack = EnumKebab.Texto(projeto.Stack),
                projeto.Ativo,
                Ambientes = Enum.GetValues<AmbienteDeploy>().Select(EnumKebab.Texto).ToList()
            }, new DadosDoRegistro(projeto.AplicacaoId, CategoriaAuditoria.Cadastro));

            await uow.SaveChangesAsync();
            return Result<ProjetoResumoDto>.Sucesso(await projetos.MontarDtoAsync(projeto, cancellationToken));
        }
    }
}
