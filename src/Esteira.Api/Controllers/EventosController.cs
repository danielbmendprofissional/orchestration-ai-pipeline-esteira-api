using System.Text;
using System.Threading.Channels;
using Esteira.Api.Erros;
using Esteira.Api.Eventos;
using Esteira.Application.Eventos;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Resultados;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    // Tempo real (T1.2, ADR-0011): SSE em `GET /api/v1/eventos`. Sessão obrigatória (política padrão). A flag `Features:T1`
    // governa só o front: aqui nada depende dela. Sem replay: o front recarrega uma vez ao reconectar.
    [ApiController]
    [Route("api/v1")]
    public class EventosController(
        IUsuarioAtual usuario,
        IPainelAplicacaoQuery aplicacoes,
        IDistribuidorDeEventos distribuidor,
        ConexoesDeEventos conexoes,
        EventosOptions opcoes,
        TimeProvider relogio) : ControllerBase
    {
        private static readonly Erro LimiteDeConexoes = new(
            "eventos.limite-conexoes", "Há conexões demais abertas para esta pessoa. Feche alguma aba e tente de novo.", TipoErro.TooManyRequests);

        [HttpGet("eventos")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Abrir(CancellationToken cancellationToken)
        {
            using var vaga = conexoes.TentarAbrir(usuario.Id, opcoes.MaximoDeConexoesPorPessoa);
            if (vaga is null)
                return new ObjectResult(Result.Falha(LimiteDeConexoes));

            // A autorização é calculada UMA vez na abertura (mesma regra do Painel); como a conexão dura no máximo
            // `DuracaoMaxima`, o navegador reconecta e ela é recalculada.
            var visiveis = (await aplicacoes.ListarVisiveisAsync(usuario.Id, usuario.EhMor, cancellationToken)).Select(a => a.Id).ToHashSet();
            using var assinante = distribuidor.Assinar(visiveis);

            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-store";
            Response.Headers["X-Accel-Buffering"] = "no";
            await Response.StartAsync(cancellationToken);

            using var fim = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var prazo = new CancellationTokenSource(opcoes.DuracaoMaxima, relogio);
            using var ligados = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, prazo.Token);
            using var ping = new PeriodicTimer(opcoes.IntervaloDePing, relogio);

            try
            {
                await EscreverAsync(": conectado\n\n", ligados.Token);
                await LacoAsync(assinante.Eventos, ping, ligados.Token);
            }
            catch (OperationCanceledException)
            {
                // prazo de 5 min ou cliente foi embora: encerra normalmente
            }

            return new EmptyResult();
        }

        private async Task LacoAsync(ChannelReader<EventoDeTempoReal> leitor, PeriodicTimer ping, CancellationToken ct)
        {
            Task<bool>? leitura = null;
            Task<bool>? tique = null;

            while (!ct.IsCancellationRequested)
            {
                leitura ??= leitor.WaitToReadAsync(ct).AsTask();
                tique ??= ping.WaitForNextTickAsync(ct).AsTask();
                await Task.WhenAny(leitura, tique);

                if (tique.IsCompleted)
                {
                    if (!await tique)
                        return;
                    tique = null;
                    await EscreverAsync(": ping\n\n", ct);
                }

                if (leitura.IsCompleted)
                {
                    if (!await leitura)
                        return;
                    leitura = null;
                    while (leitor.TryRead(out var evento))
                        await EscreverAsync($"event: {evento.Tipo}\ndata: {CodecDeEventos.Serializar(evento)}\n\n", ct);
                }
            }
        }

        private async Task EscreverAsync(string texto, CancellationToken ct)
        {
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(texto), ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}
