using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.FluxosDeploy.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Domain.FluxosDeploy.Interfaces
{
    public interface IFluxoDeployService
    {  
        void DeployValido(FluxoDeployEntity fluxoDeploy, string ambiente);
        void ValidarNovoFluxo(FluxoDeployEntity fluxoDeploy);
        void ValidarSelecaoEtapaEspecificaQuandoHaMaisDeUmFluxo(FluxoDeployEntity fluxoAtual, string etapaDesejada, FluxoDeployEntity fluxoAberto = null);
        void ValidarEntradaEtapa(FluxoDeployEntity fluxoDeploy, string etapa, FluxoDeployEntity outroFluxo = null);
        FluxoDeployEntity ObterFluxoAtual(IEnumerable<FluxoDeployEntity> fluxos, IEnumerable<DeployEntity> deploys, string branch, string tag, int idProjetoGit);
        
    }
}
