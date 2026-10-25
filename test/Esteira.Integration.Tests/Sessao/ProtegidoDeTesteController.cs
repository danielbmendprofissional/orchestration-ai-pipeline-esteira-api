using Microsoft.AspNetCore.Mvc;

namespace Esteira.Integration.Tests.Sessao
{
    // Controller só de teste SEM [AllowAnonymous]: prova que a sessão é exigida por padrão (critério 15).
    [ApiController]
    [Route("api/teste-protegido")]
    public sealed class ProtegidoDeTesteController : ControllerBase
    {
        [HttpGet]
        public IActionResult Obter() => Ok("segredo");
    }
}
