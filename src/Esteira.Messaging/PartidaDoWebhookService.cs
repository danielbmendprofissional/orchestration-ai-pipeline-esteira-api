using Esteira.Application.Eventos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Esteira.Messaging
{
    // Serviço de partida (T1.4, critério 10): uma vez, ao subir, enfileira o registro do hook dos Projetos ativos sem
    // WebhookProjeto. Um escopo por Projeto (a corrida de unicidade de um não contamina os outros); nunca derruba o host.
    public sealed class PartidaDoWebhookService(IServiceScopeFactory escopos, ILogger<PartidaDoWebhookService> log) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                IReadOnlyList<long> pendentes;
                using (var escopo = escopos.CreateScope())
                    pendentes = await escopo.ServiceProvider.GetRequiredService<PartidaDoWebhook>().ListarPendentesAsync(stoppingToken);

                var enfileirados = 0;
                foreach (var projetoId in pendentes)
                {
                    try
                    {
                        using var escopo = escopos.CreateScope();
                        if (await escopo.ServiceProvider.GetRequiredService<PartidaDoWebhook>().EnfileirarAsync(projetoId, stoppingToken))
                            enfileirados++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Outra instância enfileirou o mesmo Projeto primeiro (índice único) ou falha pontual: segue.
                        log.LogWarning("Não foi possível enfileirar o webhook do Projeto {Projeto} ({Tipo}).", projetoId, ex.GetType().Name);
                    }
                }

                if (enfileirados > 0)
                    log.LogInformation("Partida do webhook: {Quantidade} Projeto(s) enfileirado(s).", enfileirados);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                log.LogWarning("A partida do webhook falhou ({Tipo}); os Projetos sem hook ficam para a próxima subida.", ex.GetType().Name);
            }
        }
    }
}
