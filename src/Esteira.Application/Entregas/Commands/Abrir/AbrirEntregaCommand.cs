using Esteira.Application.Aplicacoes;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using MediatR;

namespace Esteira.Application.Entregas.Commands.Abrir
{
    public sealed record FatiaParaAbrir(string Codigo, bool ComFlag);

    public sealed record FluxoParaAbrir(Guid ProjetoId, TipoMudanca TipoMudanca, IReadOnlyList<FatiaParaAbrir> Fatias);

    // Sem rota HTTP (E1): quem chama é a E1a. Devolve o PublicId da Entrega aberta.
    // ICommand (sem transação explícita): o TransactionBehavior salva só se o Result for sucesso.
    public sealed record AbrirEntregaCommand(Guid AplicacaoId, IReadOnlyList<FluxoParaAbrir> Fluxos)
        : IRequest<Result<Guid>>, ICommand;

    public sealed class AbrirEntregaHandler(
        IAcessoAplicacaoQuery acesso,
        IAutorizacaoAplicacao autorizacao,
        IAplicacaoRepositorio aplicacoes,
        IProjetoRepositorio projetos,
        IPessoaRepositorio pessoas,
        IEntregaRepositorio entregas,
        IUsuarioAtual usuario,
        TimeProvider relogio) : IRequestHandler<AbrirEntregaCommand, Result<Guid>>
    {
        private static readonly Erro SemPermissao = new(
            "entrega.sem-permissao", "Só Membro da Aplicação ou MOR abre uma Entrega.", TipoErro.Forbidden);

        private static readonly Erro ProjetoInvalido = new(
            "entrega.projeto-invalido", "O Projeto não pertence à Aplicação ou está inativo.", TipoErro.Validation);

        public async Task<Result<Guid>> Handle(AbrirEntregaCommand request, CancellationToken cancellationToken)
        {
            var id = await acesso.ResolverIdAsync(request.AplicacaoId, cancellationToken);
            if (id is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var permissao = await autorizacao.ObterAcessoAsync(id.Value, cancellationToken);
            if (!permissao.Pode(OperacaoAplicacao.AbrirEntrega))
                return SemPermissao;

            var aplicacao = await aplicacoes.ObterAsync(id.Value, cancellationToken);
            if (aplicacao is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var pessoa = await pessoas.ObterPorSubAsync(usuario.Id, cancellationToken);
            if (pessoa is null)
                return DecisaoErros.PessoaNaoRegistrada;

            var fluxos = new List<FluxoDaEntrega>();
            foreach (var item in request.Fluxos)
            {
                var projeto = await projetos.ObterPorPublicIdAsync(item.ProjetoId, cancellationToken);
                if (projeto is null)
                    return ProjetoInvalido;

                var qa = await projetos.ObterAmbienteAsync(projeto.Id, AmbienteDeploy.Qa, cancellationToken);
                fluxos.Add(new FluxoDaEntrega(
                    projeto, qa?.Habilitado ?? false, item.TipoMudanca,
                    (item.Fatias ?? []).Select(f => new FatiaDaEntrega(f.Codigo, f.ComFlag)).ToList()));
            }

            var entrega = EntregaEntity.Abrir(aplicacao, pessoa, fluxos, relogio.GetUtcNow().UtcDateTime);
            if (!entrega.IsSuccess)
                return Result<Guid>.Falha(entrega.Errors.ToArray());

            await entregas.AdicionarAsync(entrega.Value, cancellationToken);
            return Result<Guid>.Sucesso(entrega.Value.PublicId);
        }
    }
}
