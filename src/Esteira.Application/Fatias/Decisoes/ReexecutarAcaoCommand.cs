using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Extensions;
using Esteira.Application.Fatias.Consultas;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using MassTransit;
using MediatR;

namespace Esteira.Application.Fatias.Decisoes
{
    // Reexecutar uma Ação falhada (P2b): cria a Ação seguinte da mesma MR. Mesmo desenho do Aprovar/Reprovar (o handler abre e
    // fecha a transação por causa da corrida de unicidade do duplo clique). Quem executa no GitLab é o executor.
    public sealed record ReexecutarAcaoCommand(Guid AplicacaoId, string Codigo, Guid AcaoId) : IRequest<Result<AcoesPedidasDto>>;

    public static class ReexecucaoErros
    {
        public static readonly Erro AcaoNaoEncontrada = new(
            "acao.nao-encontrada", "Ação não encontrada nesta Fatia.", TipoErro.NotFound);

        public static readonly Erro NaoReexecutavel = new(
            "acao.nao-reexecutavel", "Só a última Ação que falhou de cada tipo na MR pode ser reexecutada.", TipoErro.Conflict);
    }

    public sealed class ReexecutarAcaoHandler(
        PreparadorDeDecisao preparador, IAcessoAplicacaoQuery acesso, IAutorizacaoAplicacao autorizacao, IPessoaRepositorio pessoas,
        IAcaoBotRepositorio acoes, IUnitOfWork uow, IAuditoria auditoria, IPublishEndpoint publicador, TimeProvider relogio)
        : IRequestHandler<ReexecutarAcaoCommand, Result<AcoesPedidasDto>>
    {
        public async Task<Result<AcoesPedidasDto>> Handle(ReexecutarAcaoCommand request, CancellationToken cancellationToken)
        {
            // Papel antes de qualquer leitura da Ação: quem não tem papel na Aplicação nem descobre se a Ação existe (404).
            var id = await acesso.ResolverIdAsync(request.AplicacaoId, cancellationToken);
            if (id is null)
                return Result<AcoesPedidasDto>.Falha(AutorizacaoAplicacao.NaoEncontrada);

            var papeis = await autorizacao.ObterAcessoAsync(id.Value, cancellationToken);
            if (!papeis.EhMor && !papeis.TemPapel)
                return Result<AcoesPedidasDto>.Falha(AutorizacaoAplicacao.NaoEncontrada);

            var original = await acoes.ObterPorPublicIdAsync(request.AcaoId, cancellationToken);
            if (original is null || original.AplicacaoId != id.Value
                || !string.Equals(original.FatiaCodigo, request.Codigo?.Trim(), StringComparison.OrdinalIgnoreCase))
                return Result<AcoesPedidasDto>.Falha(ReexecucaoErros.AcaoNaoEncontrada);

            // A operação é a do pedido original: abrir-review é AbrirReview; aprovar e reprovar são DecidirFatia.
            var operacao = original.Tipo == TipoAcaoBot.AbrirReview ? OperacaoAplicacao.AbrirReview : OperacaoAplicacao.DecidirFatia;
            if (!papeis.Pode(operacao))
                return Result<AcoesPedidasDto>.Falha(AutorizacaoAplicacao.PapelInsuficiente);

            var preparada = await preparador.PrepararAsync(request.AplicacaoId, original.FatiaCodigo, operacao, cancellationToken);
            if (!preparada.IsSuccess)
                return Result<AcoesPedidasDto>.Falha(preparada.Errors.ToArray());

            var p = preparada.Value;
            var pessoa = await pessoas.ObterPorSubAsync(p.Sub, cancellationToken);
            if (pessoa is null)
                return Result<AcoesPedidasDto>.Falha(DecisaoErros.PessoaNaoRegistrada);

            string chavePrincipal = string.Empty;
            string? chaveFront = null;
            await uow.BeginTransactionAsync();
            try
            {
                // Trava por Fatia antes de reler: pedidos concorrentes da mesma Fatia se enfileiram.
                await acoes.TravarFatiaAsync(p.AplicacaoId, p.Fatia.Codigo, cancellationToken);
                var historico = await acoes.ListarDaFatiaAsync(p.AplicacaoId, p.Fatia.Codigo, cancellationToken);
                var falhada = historico.FirstOrDefault(a => a.Id == original.Id);
                if (falhada is null)
                {
                    await uow.RollbackAsync();
                    return Result<AcoesPedidasDto>.Falha(ReexecucaoErros.AcaoNaoEncontrada);
                }

                chavePrincipal = ChaveSeguinte(p, falhada, historico);

                // Duplo clique: o mesmo pedido já gravado volta como está, antes das regras (a falhada já não é a última).
                var repetida = await acoes.ObterPorChaveAsync(chavePrincipal, cancellationToken);
                if (repetida is not null && repetida.ReexecucaoDeAcaoId == falhada.Id)
                {
                    await uow.RollbackAsync();
                    var recriadas = historico.Where(a => a.DependeDeAcaoId == repetida.Id && a.ReexecucaoDeAcaoId is not null).ToList();
                    return Resposta(p, [repetida, .. recriadas]);
                }

                if (falhada.Estado != EstadoAcaoBot.Falhou || HaMaisNova(historico, falhada))
                {
                    await uow.RollbackAsync();
                    return Result<AcoesPedidasDto>.Falha(ReexecucaoErros.NaoReexecutavel);
                }

                var mr = p.MrsAbertas.FirstOrDefault(m => m.ProjetoId == falhada.ProjetoId && m.Iid == falhada.MrIid);
                if (mr is null)
                {
                    await uow.RollbackAsync();
                    return Result<AcoesPedidasDto>.Falha(DecisaoErros.SemMrAberta);
                }

                if (falhada.Tipo is TipoAcaoBot.Aprovar or TipoAcaoBot.Reprovar && DecisaoErros.HaDecisaoEmAndamento(historico))
                {
                    await uow.RollbackAsync();
                    return Result<AcoesPedidasDto>.Falha(DecisaoErros.DecisaoEmAndamento);
                }

                // A Ação do front que foi cancelada porque esta da API falhou volta junto, dependendo da nova.
                var front = mr.Repositorio == RepositorioFatia.Api ? AcharFrontCancelada(p, falhada, historico) : null;
                if (front is not null)
                    chaveFront = ChaveSeguinte(p, front, historico);

                var criadas = new List<AcaoBotEntity>();
                var nova = Criar(p, pessoa.Id, falhada, falhada.DependeDeAcaoId, historico);
                if (!nova.IsSuccess)
                {
                    await uow.RollbackAsync();
                    return Result<AcoesPedidasDto>.Falha(nova.Errors.ToArray());
                }

                await Registrar(nova.Value, falhada, cancellationToken);
                criadas.Add(nova.Value);

                if (front is not null)
                {
                    // O Id da nova vem do banco, dentro da mesma transação.
                    await uow.SaveChangesAsync();
                    var recriada = Criar(p, pessoa.Id, front, nova.Value.Id, historico);
                    if (!recriada.IsSuccess)
                    {
                        await uow.RollbackAsync();
                        return Result<AcoesPedidasDto>.Falha(recriada.Errors.ToArray());
                    }

                    await Registrar(recriada.Value, front, cancellationToken);
                    criadas.Add(recriada.Value);
                }

                await uow.SaveChangesAsync();
                await uow.CommitAsync();
                return Resposta(p, criadas);
            }
            catch (ViolacaoDeUnicidadeException violacao) when ((violacao.Restricao ?? string.Empty).Contains("ChaveIdempotencia", StringComparison.OrdinalIgnoreCase))
            {
                // Duplo clique concorrente: a outra requisição gravou primeiro. Desfaz a nossa e devolve as Ações que ganharam.
                await uow.RollbackAsync();
                var vencedora = await acoes.ObterPorChaveAsync(chavePrincipal, cancellationToken);
                if (vencedora is null)
                    throw;

                var achadas = new List<AcaoBotEntity> { vencedora };
                if (chaveFront is not null && await acoes.ObterPorChaveAsync(chaveFront, cancellationToken) is { } frontVencedora)
                    achadas.Add(frontVencedora);

                return Resposta(p, achadas);
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }

            async Task Registrar(AcaoBotEntity acao, AcaoBotEntity falhadaOrigem, CancellationToken ct)
            {
                await acoes.AdicionarAsync(acao, ct);
                // Sem o comentário: ele fica só na Ação.
                auditoria.Registrar("AcaoBot", acao.PublicId, AcaoAuditoria.Criou, null, new
                {
                    Fatia = acao.FatiaCodigo, Tipo = EnumKebab.Texto(acao.Tipo), Estado = EnumKebab.Texto(acao.Estado),
                    acao.ProjetoId, acao.MrIid, acao.ChaveIdempotencia, ReexecucaoDe = falhadaOrigem.PublicId
                }, new DadosDoRegistro(acao.AplicacaoId, CategoriaAuditoria.Fatias, ObjetoCodigo: acao.FatiaCodigo));
                await publicador.Publish(new AcaoBotPedida(acao.PublicId, EnumKebab.Texto(acao.Tipo), acao.FatiaCodigo, acao.ChaveIdempotencia), ct);
            }

            Result<AcaoBotEntity> Criar(DecisaoPreparada prep, long pessoaId, AcaoBotEntity origem, long? dependeDe, IReadOnlyList<AcaoBotEntity> hist)
                => AcaoBotEntity.Criar(
                    prep.AplicacaoId, prep.Fatia.Codigo, origem.ProjetoId, origem.MrIid, origem.Tipo, Tentativa(origem, hist),
                    dependeDe, pessoaId, origem.Comentario, relogio.GetUtcNow().UtcDateTime, origem.Id);
        }

        // Mesma contagem do Aprovar/Reprovar/Abrir review: tentativa + 1 por falha ou cancelamento anterior da mesma MR.
        private static int Tentativa(AcaoBotEntity a, IReadOnlyList<AcaoBotEntity> historico)
            => 1 + historico.Count(x => x.Tipo == a.Tipo && x.ProjetoId == a.ProjetoId && x.MrIid == a.MrIid
                && x.Estado is EstadoAcaoBot.Falhou or EstadoAcaoBot.Cancelada);

        private static string ChaveSeguinte(DecisaoPreparada p, AcaoBotEntity a, IReadOnlyList<AcaoBotEntity> historico)
            => AcaoBotEntity.MontarChave(p.AplicacaoId, p.Fatia.Codigo, a.Tipo, a.ProjetoId, a.MrIid, Tentativa(a, historico));

        private static bool HaMaisNova(IReadOnlyList<AcaoBotEntity> historico, AcaoBotEntity a)
            => historico.Any(x => x.Id > a.Id && x.Tipo == a.Tipo && x.ProjetoId == a.ProjetoId && x.MrIid == a.MrIid);

        private static AcaoBotEntity? AcharFrontCancelada(DecisaoPreparada p, AcaoBotEntity falhadaApi, IReadOnlyList<AcaoBotEntity> historico)
            => historico.FirstOrDefault(x => x.DependeDeAcaoId == falhadaApi.Id && x.Tipo == falhadaApi.Tipo && x.Estado == EstadoAcaoBot.Cancelada
                && !HaMaisNova(historico, x)
                && p.MrsAbertas.Any(m => m.ProjetoId == x.ProjetoId && m.Iid == x.MrIid));

        private static Result<AcoesPedidasDto> Resposta(DecisaoPreparada p, IReadOnlyList<AcaoBotEntity> acoes)
            => Result<AcoesPedidasDto>.Sucesso(new AcoesPedidasDto(acoes.Select(a => new AcaoPedidaDto(
                a.PublicId, EnumKebab.Texto(a.Tipo), EnumKebab.Texto(a.Estado),
                PainelErros.Texto(p.Aplicacao.Projetos.FirstOrDefault(x => x.Id == a.ProjetoId)?.Repositorio ?? RepositorioFatia.Api), a.MrIid)).ToList()));
    }
}
