using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;

namespace Esteira.Application.Fatias.Decisoes
{
    // Serviço que o executor da P2b usa para mudar o estado de uma Ação (spec P2, "Serviço para o executor"): só transições válidas
    // (a regra mora na AcaoBotEntity), API primeiro, auditoria e descarte do instantâneo do Painel ao confirmar ou falhar.
    // `acaoPublicId` é o `AcaoId` da mensagem AcaoBotPedida.
    public sealed class RegistrarProgressoDaAcao(
        IAcaoBotRepositorio acoes, IAcessoAplicacaoQuery acesso, IUnitOfWork uow, IAuditoria auditoria, IDescarteDoPainel painel, TimeProvider relogio)
    {
        private static readonly Erro NaoEncontrada = new("acao-bot.nao-encontrada", "Ação do bot não encontrada.", TipoErro.NotFound);

        public async Task<Result> RegistrarAsync(
            Guid acaoPublicId, EstadoAcaoBot estado, string? codigoErro = null, CancellationToken cancellationToken = default)
        {
            var acao = await acoes.ObterPorPublicIdAsync(acaoPublicId, cancellationToken);
            if (acao is null)
                return Result.Falha(NaoEncontrada);

            var agora = relogio.GetUtcNow().UtcDateTime;
            var antes = Instantaneo(acao);

            var dependencia = acao.DependeDeAcaoId is { } depId ? await acoes.ObterAsync(depId, cancellationToken) : null;
            var transicao = estado switch
            {
                EstadoAcaoBot.Executando => acao.IniciarAposDependencia(dependencia, agora),
                EstadoAcaoBot.Confirmada => acao.Confirmar(agora),
                EstadoAcaoBot.Falhou => acao.Falhar(codigoErro, agora),
                EstadoAcaoBot.Cancelada => acao.Cancelar(agora),
                _ => Result.Falha(new Erro("acao-bot.transicao-invalida", $"A ação não pode ir para {estado}.", TipoErro.Conflict))
            };
            if (!transicao.IsSuccess)
                return transicao;

            auditoria.Registrar("AcaoBot", acao.PublicId, AcaoAuditoria.Alterou, antes, Instantaneo(acao));

            // A da API falhou: as Ações que dependem dela (a do front) nunca vão rodar.
            if (estado == EstadoAcaoBot.Falhou)
            {
                var daFatia = await acoes.ListarDaFatiaAsync(acao.AplicacaoId, acao.FatiaCodigo, cancellationToken);
                foreach (var dependente in daFatia.Where(a => a.DependeDeAcaoId == acao.Id && a.Estado == EstadoAcaoBot.Pedida))
                {
                    var antesDep = Instantaneo(dependente);
                    if (dependente.CancelarSeDependenciaFalhou(acao, agora).IsSuccess)
                        auditoria.Registrar("AcaoBot", dependente.PublicId, AcaoAuditoria.Alterou, antesDep, Instantaneo(dependente));
                }
            }

            await uow.SaveChangesAsync();

            if (estado is EstadoAcaoBot.Confirmada or EstadoAcaoBot.Falhou)
            {
                var publicId = await acesso.ObterPublicIdAsync(acao.AplicacaoId, cancellationToken);
                if (publicId is not null)
                    await painel.DescartarAsync(publicId.Value.ToString(), cancellationToken);
            }

            return Result.Sucesso();
        }

        private static object Instantaneo(AcaoBotEntity a) => new { Estado = EnumKebab.Texto(a.Estado), a.CodigoErro };
    }
}
