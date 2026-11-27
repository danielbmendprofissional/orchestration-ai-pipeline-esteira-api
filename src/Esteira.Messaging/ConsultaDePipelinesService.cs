using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Fatias;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Esteira.Messaging
{
    // Roda a ConsultaDePipelines a cada Review:IntervaloConsultaSegundos (padrão 60; rede de segurança do webhook, T1). Cria um escopo por volta (a consulta
    // e as opções são scoped) e nunca derruba o host quando uma volta falha.
    public sealed class ConsultaDePipelinesService(IServiceScopeFactory escopos, ILogger<ConsultaDePipelinesService> log)
        : BackgroundService
    {
        private const int IntervaloPadraoSegundos = 60;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var segundos = IntervaloPadraoSegundos;
                try
                {
                    using var escopo = escopos.CreateScope();
                    var opcoes = escopo.ServiceProvider.GetService<IOpcoesDeRevisao>();
                    if (opcoes is not null && opcoes.IntervaloConsultaSegundos > 0)
                        segundos = opcoes.IntervaloConsultaSegundos;

                    await escopo.ServiceProvider.GetRequiredService<ConsultaDePipelines>().ExecutarAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log.LogWarning("Uma volta da consulta de pipelines falhou ({Tipo}); segue na próxima.", ex.GetType().Name);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(segundos), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
