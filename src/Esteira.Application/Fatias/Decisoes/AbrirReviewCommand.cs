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
    // Não implementa ICommand/ITransactionRequest de propósito: o handler abre e fecha a transação, porque a corrida de
    // unicidade (duplo clique) precisa de rollback e releitura da primeira Ação, o que o behavior não deixa fazer.
    public sealed record AbrirReviewCommand(Guid AplicacaoId, string Codigo) : IRequest<Result<AcoesPedidasDto>>;

    // `Repositorio`: api | frontend. `Tipo`/`Estado` em kebab-case.
    public sealed record AcaoPedidaDto(Guid Id, string Tipo, string Estado, string Repositorio, long MrIid);

    public sealed record AcoesPedidasDto(IReadOnlyList<AcaoPedidaDto> Acoes);

    // Mensagem de outbox (ADR-0010): publicada na mesma transação da Ação; a P2 não registra consumer, a P2b sim.
    public sealed record AcaoBotPedida(Guid AcaoId, string Tipo, string FatiaCodigo, string ChaveIdempotencia);

    public sealed class AbrirReviewHandler(
        PreparadorDeDecisao preparador, IPessoaRepositorio pessoas, IAcaoBotRepositorio acoes, IUnitOfWork uow,
        IAuditoria auditoria, IPublishEndpoint publicador, TimeProvider relogio) : IRequestHandler<AbrirReviewCommand, Result<AcoesPedidasDto>>
    {
        private sealed record Planejada(RepositorioFatia Repositorio, MrAbertaDaFatia Mr, int Tentativa, string Chave);

        public async Task<Result<AcoesPedidasDto>> Handle(AbrirReviewCommand request, CancellationToken cancellationToken)
        {
            var preparada = await preparador.PrepararAsync(request.AplicacaoId, request.Codigo, OperacaoAplicacao.AbrirReview, cancellationToken);
            if (!preparada.IsSuccess)
                return Result<AcoesPedidasDto>.Falha(preparada.Errors.ToArray());

            var p = preparada.Value;
            var pessoa = await pessoas.ObterPorSubAsync(p.Sub, cancellationToken);
            if (pessoa is null)
                return Result<AcoesPedidasDto>.Falha(DecisaoErros.PessoaNaoRegistrada);

            // Uma Ação por repositório, na MR aberta mais antiga; a da API primeiro.
            var historico = await acoes.ListarDaFatiaAsync(p.AplicacaoId, p.Fatia.Codigo, cancellationToken);
            var plano = new List<Planejada>();
            foreach (var mr in p.MrsAbertas.GroupBy(m => m.Repositorio).OrderBy(g => g.Key).Select(g => g.OrderBy(m => m.Iid).First()))
            {
                var anteriores = historico.Where(a => a.Tipo == TipoAcaoBot.AbrirReview && a.ProjetoId == mr.ProjetoId && a.MrIid == mr.Iid).ToList();
                if (anteriores.Any(a => a.Estado is EstadoAcaoBot.Pedida or EstadoAcaoBot.Executando or EstadoAcaoBot.Confirmada))
                    return Result<AcoesPedidasDto>.Falha(DecisaoErros.ReviewJaPedido);

                // Falhou/cancelada ficam no histórico: cada novo pedido é a tentativa seguinte.
                var tentativa = anteriores.Count + 1;
                plano.Add(new Planejada(mr.Repositorio, mr, tentativa,
                    AcaoBotEntity.MontarChave(p.AplicacaoId, p.Fatia.Codigo, TipoAcaoBot.AbrirReview, mr.ProjetoId, mr.Iid, tentativa)));
            }

            var existentes = await ObterExistentesAsync(plano, cancellationToken);
            if (existentes is not null)
                return Resposta(plano, existentes);

            await uow.BeginTransactionAsync();
            try
            {
                var criadas = new List<AcaoBotEntity>();
                foreach (var item in plano)
                {
                    var criada = AcaoBotEntity.Criar(
                        p.AplicacaoId, p.Fatia.Codigo, item.Mr.ProjetoId, item.Mr.Iid, TipoAcaoBot.AbrirReview, item.Tentativa,
                        dependeDeAcaoId: criadas.Count > 0 ? criadas[0].Id : null, pessoa.Id, comentario: null, relogio.GetUtcNow().UtcDateTime);
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

                    // A Ação do front aponta para a da API: o Id vem do banco, dentro da mesma transação.
                    if (criadas.Count == 1 && plano.Count > 1)
                        await uow.SaveChangesAsync();
                }

                await uow.SaveChangesAsync();
                await uow.CommitAsync();
                return Resposta(plano, criadas);
            }
            catch (ViolacaoDeUnicidadeException violacao) when ((violacao.Restricao ?? string.Empty).Contains("ChaveIdempotencia", StringComparison.OrdinalIgnoreCase))
            {
                // Duplo clique concorrente: a outra requisição gravou primeiro. Desfaz a nossa e devolve a Ação que ganhou.
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

        // Todas as Ações do plano já gravadas (mesma chave) ou null.
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
                a.PublicId, EnumKebab.Texto(a.Tipo), EnumKebab.Texto(a.Estado), PainelErros.Texto(plano[i].Repositorio), a.MrIid)).ToList()));
    }
}
