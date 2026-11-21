using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Aplicacoes.Queries.ListarProjetosGitLab
{
    public sealed record ListarProjetosGitLabQuery(Guid AplicacaoId) : IRequest<Result<IReadOnlyList<ProjetoGitLabItemDto>>>;

    public sealed class ListarProjetosGitLabHandler(
        IAcessoAplicacaoQuery acesso,
        IAutorizacaoAplicacao autorizacao,
        IAplicacaoRepositorio aplicacoes,
        IProjetoRepositorio projetos,
        IGitLab gitlab) : IRequestHandler<ListarProjetosGitLabQuery, Result<IReadOnlyList<ProjetoGitLabItemDto>>>
    {
        private const string ArquivoDeCi = ".gitlab-ci.yml";
        private const int LeiturasEmParalelo = 8;

        public async Task<Result<IReadOnlyList<ProjetoGitLabItemDto>>> Handle(ListarProjetosGitLabQuery request, CancellationToken cancellationToken)
        {
            var id = await acesso.ResolverIdAsync(request.AplicacaoId, cancellationToken);
            if (id is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var autorizado = await autorizacao.AutorizarAsync(id.Value, OperacaoAplicacao.EditarCadastro, cancellationToken);
            if (!autorizado.IsSuccess)
                return Result<IReadOnlyList<ProjetoGitLabItemDto>>.Falha(autorizado.Errors.ToArray());

            var aplicacao = await aplicacoes.ObterAsync(id.Value, cancellationToken);
            if (aplicacao is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var lista = await gitlab.ListarProjetosDoGrupoAsync(aplicacao.GrupoGitLabCaminho, aplicacao.GrupoGitLabId, cancellationToken);
            if (!lista.IsSuccess)
                return Result<IReadOnlyList<ProjetoGitLabItemDto>>.Falha(lista.Errors.ToArray());

            var cadastrados = await projetos.ProjetosGitLabCadastradosAsync(lista.Value.Select(p => p.Id).ToList(), cancellationToken);

            using var limite = new SemaphoreSlim(LeiturasEmParalelo);
            var itens = await Task.WhenAll(lista.Value.Select(async p => new ProjetoGitLabItemDto
            {
                ProjetoGitLabId = p.Id,
                Caminho = p.Caminho,
                Nome = p.Nome,
                Cadastrado = cadastrados.Contains(p.Id),
                StackSugerida = await SugerirAsync(aplicacao.GrupoGitLabCaminho, p, limite, cancellationToken)
            }));

            return Result<IReadOnlyList<ProjetoGitLabItemDto>>.Sucesso(itens);
        }

        // Falha ao ler o arquivo de um projeto não derruba a lista: sem sugestão.
        private async Task<string?> SugerirAsync(string grupoCaminho, ProjetoGitLabDto projeto, SemaphoreSlim limite, CancellationToken cancellationToken)
        {
            await limite.WaitAsync(cancellationToken);
            try
            {
                var arquivo = await gitlab.ObterArquivoAsync(grupoCaminho, projeto.Id, ArquivoDeCi, projeto.BranchPadrao ?? "HEAD", cancellationToken);
                return arquivo.IsSuccess ? StackSugerida.Ler(arquivo.Value) : null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return null;
            }
            finally
            {
                limite.Release();
            }
        }
    }
}
