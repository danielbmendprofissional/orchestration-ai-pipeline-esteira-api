using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Fatias;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Esteira.Messaging.Consumers
{
    // Sinaliza erro transitório do GitLab: a mensagem falha para a política de retry do endpoint reentregá-la
    // (o executor, ao retomar uma Ação executando, é idempotente). Na última tentativa o executor já grava falhou.
    public sealed class AcaoBotTransitoriaException(string codigo)
        : Exception($"Falha transitória ao executar a Ação do bot ({codigo}).")
    {
    }

    // Consumer fino da Ação do bot (spec P2b): o executor decide e escreve no GitLab; aqui só se traduz o resultado
    // para a mensagem. Aguardando dependência não escreve nada e não falha: a Ação do front será republicada quando a
    // da API confirmar.
    public sealed class AcaoBotPedidaConsumer(
        ExecutorDeAcaoBot executor, IOpcoesDeRevisao opcoes, ILogger<AcaoBotPedidaConsumer> log)
        : IConsumer<AcaoBotPedida>
    {
        public async Task Consume(ConsumeContext<AcaoBotPedida> context)
        {
            var acaoId = context.Message.AcaoId;
            // Tentativa atual = reentregas já feitas + 1; esgotado o Bot:TentativasMaximas o executor grava falhou.
            var resultado = await executor.ExecutarAsync(
                acaoId, context.GetRetryAttempt() + 1, opcoes.TentativasMaximas, context.CancellationToken);

            switch (resultado.Situacao)
            {
                case SituacaoDaExecucao.Transitoria:
                    throw new AcaoBotTransitoriaException(resultado.CodigoErro ?? "gitlab.indisponivel");

                case SituacaoDaExecucao.AguardandoDependencia:
                    log.LogInformation("Ação {Acao} aguardando a Ação da API confirmar; será republicada.", acaoId);
                    break;

                // abrir-review Angular: a pipeline foi criada; a consulta periódica é quem conclui a Ação. Sem retry.
                case SituacaoDaExecucao.AguardandoPipeline:
                    log.LogInformation("Ação {Acao} aguardando o fim da pipeline do preview.", acaoId);
                    break;

                case SituacaoDaExecucao.NaoImplementada:
                    log.LogInformation("Ação {Acao} do tipo {Tipo} não tem executor (tipo não tratado).", acaoId, context.Message.Tipo);
                    break;

                case SituacaoDaExecucao.NaoEncontrada:
                    log.LogWarning("Ação {Acao} não encontrada; mensagem descartada.", acaoId);
                    break;

                case SituacaoDaExecucao.Falhou:
                    log.LogWarning("Ação {Acao} falhou ({Codigo}).", acaoId, resultado.CodigoErro);
                    break;

                default:
                    log.LogInformation("Ação {Acao}: {Situacao}.", acaoId, resultado.Situacao);
                    break;
            }
        }
    }

    // Política de retry da Ação do bot (critério 9): `Bot:TentativasMaximas` tentativas no total, espera crescente.
    public static class PoliticaDeRetryDaAcaoBot
    {
        private static readonly TimeSpan EsperaInicial = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan EsperaMaxima = TimeSpan.FromSeconds(30);

        public static int Reentregas(int tentativasMaximas) => Math.Max(0, tentativasMaximas - 1);

        // 2s, 4s, 8s... até 30s.
        public static TimeSpan Espera(int reentrega)
            => TimeSpan.FromTicks(Math.Min(EsperaMaxima.Ticks, EsperaInicial.Ticks << Math.Min(Math.Max(reentrega, 1) - 1, 10)));
    }

    public sealed class AcaoBotPedidaConsumerDefinition : ConsumerDefinition<AcaoBotPedidaConsumer>
    {
        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<AcaoBotPedidaConsumer> consumerConfigurator,
            IRegistrationContext context)
        {
            var tentativas = int.TryParse((context.GetService(typeof(IConfiguration)) as IConfiguration)?["Bot:TentativasMaximas"], out var n) && n > 0 ? n : 3;
            var reentregas = PoliticaDeRetryDaAcaoBot.Reentregas(tentativas);
            if (reentregas > 0)
                endpointConfigurator.UseMessageRetry(r => r
                    .Intervals(Enumerable.Range(1, reentregas).Select(PoliticaDeRetryDaAcaoBot.Espera).ToArray())
                    .Handle<AcaoBotTransitoriaException>());
        }
    }
}
