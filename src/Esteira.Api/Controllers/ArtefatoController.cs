using Esteira.Application.Deploys.Commands.CriarDeploy;
using Esteira.Application.Deploys.Queries.ObterArtefato;
using Esteira.Application.DTOs.Request;
using Esteira.Application.DTOs.Response;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text.Json.Serialization;
using Esteira.Api.Paginacao;

namespace Esteira.Api.Controllers
{
    [Route("api/artefato")]
    [ApiController]
    public class ArtefatoController : ControllerBase
    {

        private readonly IMediator _mediator;

        public ArtefatoController(IMediator mediator)
        {
            _mediator = mediator;
        }


        [HttpPost]
        public async Task<IActionResult> InserirAnalise(
                                            [FromForm] int idProjetoGit,
                                            [FromForm] string branch,
                                            [FromForm] string sistema,
                                            [FromForm] int pipeline,
                                            [FromForm] string nomeProjetoGit,
                                            [FromForm] string linguagem,
                                            [FromForm] string tag,
                                            [FromForm] string ferramenta,
                                            [FromForm] string url,
                                            IFormFile arq)
        {

            string fileContent = string.Empty;
            if (arq != null)
            {
                using (var reader = new StreamReader(arq.OpenReadStream()))
                {
                    fileContent = reader.ReadToEnd();
                }
            }


            CriarDeployCommand command = new CriarDeployCommand
            {
                FerramentaArtefatos = ferramenta.ToUpper(),
                ArtefatoDto = ferramenta.ToUpper() == "SEMGREP" ?
                                JsonConvert.DeserializeObject<SemgrepDtoRequest>(fileContent) :
                              ferramenta.ToUpper() == "TRIVY" ?
                                JsonConvert.DeserializeObject<TrivyDtoRequest>(fileContent) :
                              null,
                Branch = branch,
                IdPipeline = pipeline,
                IdProjetoGit = idProjetoGit,
                Linguagem = linguagem,
                NomeProjetoGit = nomeProjetoGit,
                Tag = tag,
                Url = url,
                Sistema = sistema
            };
            return Ok(await _mediator.Send(command));

        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ObterArtefato([FromRoute] int id) => Ok(await _mediator.Send(new ObterArtefatoQuery { Id = id }));
    }
}
