using System.Text.Json;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Data.Aplicacoes
{
    public sealed class AuditoriaEf(EsteiraContext contexto, IUsuarioAtual usuario, TimeProvider relogio) : IAuditoria
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public void Registrar(string entidade, Guid entidadePublicId, AcaoAuditoria acao, object? antes, object? depois)
        {
            var registro = RegistroAuditoriaEntity.Criar(
                relogio.GetUtcNow().UtcDateTime,
                usuario.Id,
                entidade,
                entidadePublicId,
                acao,
                antes is null ? null : JsonSerializer.Serialize(antes, Json),
                depois is null ? null : JsonSerializer.Serialize(depois, Json));

            contexto.Add(registro);
        }
    }
}
