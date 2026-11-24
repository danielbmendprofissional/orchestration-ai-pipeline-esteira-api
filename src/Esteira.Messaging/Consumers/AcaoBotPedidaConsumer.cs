using Esteira.Application.Fatias.Decisoes;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Esteira.Messaging.Consumers
{
    // Sinaliza erro transitório do GitLab: a mensagem falha para a política de retry do endpoint reentregá-la
    // (o executor, ao retomar uma Ação executando, é idempotente). P2b.4 formaliza backoff/limite e a virada para falhou.
    public sealed class AcaoBotTransitoriaException(string codigo)
        : Exception($"Falha transitória ao executar a Ação do bot ({codigo}).")
    {
    }

    // Consumer fino da Ação do bot (spec P2b): o executor decide e escreve no GitLab; aqui só se traduz o resultado
    // para a mensagem. Aguardando dependência não escreve nada e não falha: a Ação do front será republicada quando a
    // da API confirmar.
    public sealed class AcaoBotPedidaConsumer(ExecutorDeAcaoBot executor, ILogger<AcaoBotPedidaConsumer> log)
        : IConsumer<AcaoBotPedida>
    {
        public async Task Consume(ConsumeContext<AcaoBotPedida> context)
        {
            var acaoId = context.Message.AcaoId;
            var resultado = await executor.ExecutarAsync(acaoId, context.CancellationToken);

            switch (resultado.Situacao)
            {
                case SituacaoDaExecucao.Transitoria:
                    throw new AcaoBotTransitoriaException(resultado.CodigoErro ?? "gitlab.indisponivel");

                case SituacaoDaExecucao.AguardandoDependencia:
                    log.LogInformation("Ação {Acao} aguardando a Ação da API confirmar; será republicada.", acaoId);
                    break;

                case SituacaoDaExecucao.NaoImplementada:
                    log.LogInformation("Ação {Acao} do tipo {Tipo} ainda não é executada (P2b.3).", acaoId, context.Message.Tipo);
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

    // Retry básico só para o transitório (P2b.4 formaliza a política). Sem elemento novo: retry em memória do MassTransit.
    public sealed class AcaoBotPedidaConsumerDefinition : ConsumerDefinition<AcaoBotPedidaConsumer>
    {
        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<AcaoBotPedidaConsumer> consumerConfigurator,
            IRegistrationContext context)
        {
            endpointConfigurator.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
        }
    }
}
