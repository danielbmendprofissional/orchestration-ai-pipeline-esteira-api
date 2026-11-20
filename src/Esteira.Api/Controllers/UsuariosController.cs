using Esteira.Application.Interfaces.Usuario;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    public sealed record UsuarioLogadoDto(string Usuario, string Nome, string Email, IReadOnlyList<string> Perfis);

    [ApiController]
    [Route("api/v1/usuarios")]
    public class UsuariosController(IUsuarioAtual usuarioAtual) : ControllerBase
    {
        // Quem sou eu: o front chama uma vez no boot (ADR-0004). A sessão é exigida pela política padrão.
        [HttpGet("me")]
        public IActionResult Me()
            => Ok(new UsuarioLogadoDto(usuarioAtual.Usuario, usuarioAtual.Nome, usuarioAtual.Email, usuarioAtual.Perfis));
    }
}
