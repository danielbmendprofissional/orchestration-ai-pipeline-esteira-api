using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Integration.Tests.Http
{
    // Controller só de teste: devolve o esquema e o IP que a API enxergou depois dos middlewares.
    [AllowAnonymous]
    [ApiController]
    [Route("api/teste-encaminhado")]
    public sealed class EncaminhamentoDeTesteController : ControllerBase
    {
        [HttpGet]
        public IActionResult Obter() => Ok(new { esquema = Request.Scheme, ip = HttpContext.Connection.RemoteIpAddress?.ToString() });
    }
}
