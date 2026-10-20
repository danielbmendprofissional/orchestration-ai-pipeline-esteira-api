using Esteira.Application.Interfaces.Usuario;
using Microsoft.Extensions.Options;

namespace Esteira.Api.Autenticacao
{
    public sealed class UsuarioAtual : IUsuarioAtual
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly AutenticacaoSettings _settings;

        public UsuarioAtual(IHttpContextAccessor accessor, IOptions<AutenticacaoSettings> settings)
        {
            _accessor = accessor;
            _settings = settings.Value;
        }

        public string Usuario => Claim(_settings.ClaimUsuario);

        public string Nome => Claim(_settings.ClaimNome) ?? Usuario;

        private string Claim(string tipo)
            => _accessor.HttpContext?.User?.FindFirst(tipo)?.Value;
    }
}
