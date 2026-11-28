using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Auditoria
{
    public sealed class EscopoDeAuditoria(IUsuarioAtual usuario) : IEscopoDeAuditoria
    {
        public IQueryable<RegistroAuditoriaEntity> Restringir(IQueryable<RegistroAuditoriaEntity> registros, IQueryable<PapelAplicacaoEntity> papeis)
        {
            if (usuario.EhMor)
                return registros;

            var sub = usuario.Id;
            return registros.Where(r => r.AplicacaoId != null && papeis.Any(p => p.AplicacaoId == r.AplicacaoId && p.Usuario == sub));
        }
    }
}
