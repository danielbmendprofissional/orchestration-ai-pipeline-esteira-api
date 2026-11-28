using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Aplicacoes.Commands.DefinirWorkspace
{
    // ProjetoGitLabId nulo limpa o workspace (o campo é opcional no cadastro).
    public sealed record DefinirWorkspaceCommand(Guid AplicacaoId, long? ProjetoGitLabId) : IRequest<Result>, ITransactionRequest;

    public sealed class DefinirWorkspaceHandler(
        IAcessoAplicacaoQuery acesso,
        IAutorizacaoAplicacao autorizacao,
        IAplicacaoRepositorio aplicacoes,
        IProjetoRepositorio projetos,
        IGitLab gitlab,
        IUnitOfWork uow,
        IAuditoria auditoria) : IRequestHandler<DefinirWorkspaceCommand, Result>
    {
        public static readonly Erro ForaDoGrupo = new("aplicacao.workspace-fora-do-grupo", "Este projeto não pertence ao grupo do GitLab da Aplicação.", TipoErro.Failure);

        public static readonly Erro EProjeto = new("aplicacao.workspace-e-projeto", "Este projeto já está cadastrado como Projeto; o workspace não pode ser um Projeto.", TipoErro.Conflict);

        public static readonly Erro NaoEncontrado = new("aplicacao.workspace-nao-encontrado", "Projeto do GitLab não encontrado.", TipoErro.Validation);

        public async Task<Result> Handle(DefinirWorkspaceCommand request, CancellationToken cancellationToken)
        {
            var id = await acesso.ResolverIdAsync(request.AplicacaoId, cancellationToken);
            if (id is null)
                return Result.Falha(AutorizacaoAplicacao.NaoEncontrada);

            var autorizado = await autorizacao.AutorizarAsync(id.Value, OperacaoAplicacao.EditarCadastro, cancellationToken);
            if (!autorizado.IsSuccess)
                return autorizado;

            var aplicacao = await aplicacoes.ObterAsync(id.Value, cancellationToken);
            if (aplicacao is null)
                return Result.Falha(AutorizacaoAplicacao.NaoEncontrada);

            var antes = Estado(aplicacao);

            if (request.ProjetoGitLabId is null)
            {
                aplicacao.LimparWorkspace();
            }
            else
            {
                var projetoId = request.ProjetoGitLabId.Value;
                var lido = await gitlab.ObterProjetoAsync(aplicacao.GrupoGitLabCaminho, projetoId, cancellationToken);
                if (!lido.IsSuccess)
                    return Result.Falha(lido.Errors.ToArray());
                if (lido.Value is null)
                    return Result.Falha(NaoEncontrado);
                if (!lido.Value.PertenceAoGrupo)
                    return Result.Falha(ForaDoGrupo);
                if (await projetos.ProjetoGitLabCadastradoAsync(projetoId, cancellationToken))
                    return Result.Falha(EProjeto);

                var definido = aplicacao.DefinirWorkspace(projetoId, lido.Value.Caminho);
                if (!definido.IsSuccess)
                    return definido;
            }

            auditoria.Registrar("Aplicacao", aplicacao.PublicId, AcaoAuditoria.Alterou, antes, Estado(aplicacao), new DadosDoRegistro(aplicacao.Id, CategoriaAuditoria.Cadastro));
            await uow.SaveChangesAsync();
            return Result.Sucesso();
        }

        private static object Estado(AplicacaoEntity a) => new { a.WorkspaceGitLabId, a.WorkspaceGitLabCaminho };
    }
}
