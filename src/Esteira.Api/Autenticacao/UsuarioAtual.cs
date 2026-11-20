using Esteira.Application.Interfaces.Usuario;
using Microsoft.Extensions.Options;

namespace Esteira.Api.Autenticacao
{
    // Lê os claims que o esquema `Sessao` montou a partir do token guardado no Redis.
    public sealed class UsuarioAtual : IUsuarioAtual
    {
        public const string ClaimId = "sub";
        public const string ClaimEmail = "email";
        public const string ClaimPerfil = "perfil";

        // Role global do realm (CONTEXT-MAP, Papéis): pode tudo em todas as Aplicações.
        public const string PerfilMor = "mor";

        private readonly IHttpContextAccessor _accessor;
        private readonly AutenticacaoSettings _settings;

        public UsuarioAtual(IHttpContextAccessor accessor, IOptions<AutenticacaoSettings> settings)
        {
            _accessor = accessor;
            _settings = settings.Value;
        }

        public string Id => Claim(ClaimId);

        public string Usuario => Claim(_settings.ClaimUsuario);

        public string Nome => Claim(_settings.ClaimNome) ?? Usuario;

        public string Email => Claim(ClaimEmail);

        public IReadOnlyList<string> Perfis
            => _accessor.HttpContext?.User?.FindAll(ClaimPerfil).Select(c => c.Value).ToArray() ?? [];

        public bool EhMor => Perfis.Contains(PerfilMor, StringComparer.OrdinalIgnoreCase);

        private string Claim(string tipo)
            => _accessor.HttpContext?.User?.FindFirst(tipo)?.Value;
    }
}
