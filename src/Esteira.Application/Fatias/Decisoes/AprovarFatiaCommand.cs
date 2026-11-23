using Esteira.Application.Common.Extensions;
using Esteira.Application.Aplicacoes;
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
    // Mesmo desenho do AbrirReview: não implementa ICommand/ITransactionRequest porque a corrida de unicidade (duplo clique)
    // precisa de rollback e releitura da primeira Ação, o que o TransactionBehavior não deixa fazer. Só registra o pedido:
    // mergear no GitLab é do executor (P2b).
    public sealed record AprovarFatiaCommand(Guid AplicacaoId, string Codigo) : IRequest<Result<AcoesPedidasDto>>;

    public sealed class AprovarFatiaHandler(
        PreparadorDeDecisao preparador, IPessoaRepositorio pessoas, IAcaoBotRepositorio acoes, IUnitOfWork uow,
        IAuditoria auditoria, IPublishEndpoint publicador, TimeProvider relogio) : IRequestHandler<AprovarFatiaCommand, Result<AcoesPedidasDto>>
    {
        private sealed record Planejada(MrAbertaDaFatia Mr, int Tentativa, string Chave);

        public async Task<Result<AcoesPedidasDto>> Handle(AprovarFatiaCommand request, CancellationToken cancellationToken)
        {
            var preparada = await preparador.PrepararAsync(request.AplicacaoId, request.Codigo, OperacaoAplicacao.DecidirFatia, cancellationToken);
            if (!preparada.IsSuccess)
                return Result<AcoesPedidasDto>.Falha(preparada.Errors.ToArray());

            var p = preparada.Value;
            var semCi = p.MrsAbertas.Where(m => !string.Equals(m.StatusPipeline, "success", StringComparison.OrdinalIgnoreCase)).ToList();
            if (semCi.Count > 0)
                return Result<AcoesPedidasDto>.Falha(DecisaoErros.CiNaoVerde(semCi));

            var pessoa = await pessoas.ObterPorSubAsync(p.Sub, cancellationToken);
            if (pessoa is null)
                return Result<AcoesPedidasDto>.Falha(DecisaoErros.PessoaNaoRegistrada);

            // Uma Ação por MR aberta, API primeiro. Aprovação que falhou ou foi cancelada fica no histórico e o novo pedido é a
            // tentativa seguinte; a ainda pedida/executando/confirmada mantém a tentativa e cai na mesma chave (duplo clique).
            IReadOnlyList<Planejada> plano = [];
            await uow.BeginTransactionAsync();
            try
            {
                // Trava por Fatia antes de reler: Aprovar e Reprovar concorrentes se enfileiram e o segundo enxerga o primeiro.
                await acoes.TravarFatiaAsync(p.AplicacaoId, p.Fatia.Codigo, cancellationToken);
                var historico = await acoes.ListarDaFatiaAsync(p.AplicacaoId, p.Fatia.Codigo, cancellationToken);
                plano = p.MrsAbertas.OrderBy(m => m.Repositorio).ThenBy(m => m.Iid).Select(mr =>
                {
                    var tentativa = 1 + historico.Count(a => a.Tipo == TipoAcaoBot.Aprovar && a.ProjetoId == mr.ProjetoId && a.MrIid == mr.Iid
                        && (a.Estado == EstadoAcaoBot.Falhou || a.Estado == EstadoAcaoBot.Cancelada));
                    return new Planejada(mr, tentativa, AcaoBotEntity.MontarChave(p.AplicacaoId, p.Fatia.Codigo, TipoAcaoBot.Aprovar, mr.ProjetoId, mr.Iid, tentativa));
                }).ToList();

                var existentes = await ObterExistentesAsync(plano, cancellationToken);
                if (existentes is not null)
                {
                    await uow.RollbackAsync();
                    return Resposta(plano, existentes);
                }

                if (DecisaoErros.HaDecisaoEmAndamento(historico))
                {
                    await uow.RollbackAsync();
                    return Result<AcoesPedidasDto>.Falha(DecisaoErros.DecisaoEmAndamento);
                }

                var criadas = new List<AcaoBotEntity>();
                var temFront = plano.Any(x => x.Mr.Repositorio != RepositorioFatia.Api);
                foreach (var item in plano)
                {
                    // A Ação do front aponta para a primeira da API: o Id vem do banco, dentro da mesma transação.
                    long? dependeDe = item.Mr.Repositorio != RepositorioFatia.Api && plano[0].Mr.Repositorio == RepositorioFatia.Api
                        ? criadas[0].Id
                        : null;
                    var criada = AcaoBotEntity.Criar(
                        p.AplicacaoId, p.Fatia.Codigo, item.Mr.ProjetoId, item.Mr.Iid, TipoAcaoBot.Aprovar, item.Tentativa,
                        dependeDe, pessoa.Id, comentario: null, relogio.GetUtcNow().UtcDateTime);
                    if (!criada.IsSuccess)
                    {
                        await uow.RollbackAsync();
                        return Result<AcoesPedidasDto>.Falha(criada.Errors.ToArray());
                    }

                    var acao = criada.Value;
                    await acoes.AdicionarAsync(acao, cancellationToken);
                    auditoria.Registrar("AcaoBot", acao.PublicId, AcaoAuditoria.Criou, null, new
                    {
                        Fatia = acao.FatiaCodigo, Tipo = EnumKebab.Texto(acao.Tipo), Estado = EnumKebab.Texto(acao.Estado),
                        acao.ProjetoId, acao.MrIid, acao.ChaveIdempotencia
                    });
                    await publicador.Publish(
                        new AcaoBotPedida(acao.PublicId, EnumKebab.Texto(acao.Tipo), acao.FatiaCodigo, acao.ChaveIdempotencia), cancellationToken);
                    criadas.Add(acao);

                    if (criadas.Count == 1 && temFront)
                        await uow.SaveChangesAsync();
                }

                await uow.SaveChangesAsync();
                await uow.CommitAsync();
                return Resposta(plano, criadas);
            }
            catch (ViolacaoDeUnicidadeException violacao) when ((violacao.Restricao ?? string.Empty).Contains("ChaveIdempotencia", StringComparison.OrdinalIgnoreCase))
            {
                // Duplo clique concorrente: a outra requisição gravou primeiro. Desfaz a nossa e devolve as Ações que ganharam.
                await uow.RollbackAsync();
                var vencedoras = await ObterExistentesAsync(plano, cancellationToken);
                if (vencedoras is null)
                    throw;

                return Resposta(plano, vencedoras);
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }

        private async Task<IReadOnlyList<AcaoBotEntity>?> ObterExistentesAsync(IReadOnlyList<Planejada> plano, CancellationToken ct)
        {
            var achadas = new List<AcaoBotEntity>();
            foreach (var item in plano)
            {
                var acao = await acoes.ObterPorChaveAsync(item.Chave, ct);
                if (acao is null)
                    return null;
                achadas.Add(acao);
            }

            return achadas;
        }

        private static Result<AcoesPedidasDto> Resposta(IReadOnlyList<Planejada> plano, IReadOnlyList<AcaoBotEntity> acoes)
            => Result<AcoesPedidasDto>.Sucesso(new AcoesPedidasDto(acoes.Select((a, i) => new AcaoPedidaDto(
                a.PublicId, EnumKebab.Texto(a.Tipo), EnumKebab.Texto(a.Estado), PainelErros.Texto(plano[i].Mr.Repositorio), a.MrIid)).ToList()));
    }
}
