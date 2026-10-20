using Esteira.Application.DTOs.Request;
using Esteira.Application.DTOs.Response;
using Esteira.Application.FluxosDeploy.Commands.AlterarEtapaEspecificaFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.AlterarEtapasFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy.Rules;
using Esteira.Application.FluxosDeploy.Commands.CancelarFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoNegocioFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoTecnicaFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarCrsBdFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarGmudFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarVersaoTarefasFluxoDeploy;
using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Tests.Common;
using Esteira.Domain.AprovadoresTecnicos.Entities;
using Esteira.Domain.AprovadoresTecnicos.Interfaces;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.ConfiguracoesSistema.Interfaces;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.Deploys.Interfaces;
using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.Etapas.Interfaces;
using Esteira.Domain.Etapas.Services;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using NSubstitute;

namespace Esteira.Application.Tests.FluxosDeploy.Cenarios
{
    internal sealed class Cenario
    {
        public static readonly string[] TodasEtapas = { "ABER", "VERSAO", "GMUD", "BD", "AET", "DEV", "QA", "HML", "AAN", "PRD" };

        public static readonly IReadOnlyList<EtapaEntity> Catalogo = new List<EtapaEntity>
        {
            EtapaEntity.Criar("ABER", "Abertura", 1, false),
            EtapaEntity.Criar("VERSAO", "Versão das tarefas", 2, false),
            EtapaEntity.Criar("GMUD", "Gmud", 3, false),
            EtapaEntity.Criar("BD", "Banco de Dados", 4, false),
            EtapaEntity.Criar("AET", "Equipe Técnica", 5, false),
            EtapaEntity.Criar("DEV", "Desenvolvimento", 6, false),
            EtapaEntity.Criar("QA", "Qualidade", 7, false),
            EtapaEntity.Criar("HML", "Homologação", 8, true),
            EtapaEntity.Criar("AAN", "Área de Negócio", 9, false),
            EtapaEntity.Criar("PRD", "Produção", 10, true)
        };

        private static readonly IEtapaRulesService[] Servicos =
        {
            new EtapaAberturaService(), new EtapaVersaoTarefasService(), new EtapaGmudService(),
            new EtapaAmbienteDevService(), new EtapaAmbienteHmlService(), new EtapaAmbienteQaService(),
            new EtapaAmbientePrdService(), new EtapaAreaNegocioService(), new EtapaEquipeTecnicaService(),
            new EtapaBancoDeDadosService()
        };

        private readonly List<FluxoDeployEntity> _fluxos = new();
        private readonly List<DeployEntity> _deploys = new();
        private readonly Dictionary<int, string> _statusTarefas = new();

        public readonly List<AprovadorTecnicoEntity> Aprovadores = new()
        {
            Construtor.Aprovador(1, "INFRA"),
            Construtor.Aprovador(2, "INFRA"),
            Construtor.Aprovador(3, "SEG")
        };

        public ConfiguracaoSistemaEntity Configuracao { get; set; } = Construtor.Configuracao();

        public readonly IFluxoDeployRepository FluxoRepo = Substitute.For<IFluxoDeployRepository>();
        public readonly IDeployRepository DeployRepo = Substitute.For<IDeployRepository>();
        public readonly IEtapaRepository EtapaRepo = Substitute.For<IEtapaRepository>();
        public readonly IConfiguracaoSistemaRepository ConfigRepo = Substitute.For<IConfiguracaoSistemaRepository>();
        public readonly IAprovadorTecnicoRepository AprovadorRepo = Substitute.For<IAprovadorTecnicoRepository>();
        public readonly IGerenciadorTarefas GerenciadorTarefas = Substitute.For<IGerenciadorTarefas>();

        public Cenario()
        {
            FluxoRepo.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci => _fluxos.FirstOrDefault(f => f.Id == ci.Arg<int>()));
            FluxoRepo.ObterPorSistemaSomenteEtapasAsync(Arg.Any<string>(), Arg.Any<StatusEnum>(), Arg.Any<CancellationToken>())
                .Returns(ci => (IReadOnlyList<FluxoDeployEntity>)_fluxos
                    .Where(f => f.Sistema == ci.ArgAt<string>(0) && f.Status == ci.ArgAt<StatusEnum>(1))
                    .ToList());
            FluxoRepo.ObterUltimaVersaoSistema(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ci => _fluxos.Where(f => f.Sistema == ci.Arg<string>()).Select(f => f.Versao).LastOrDefault());
            FluxoRepo.When(r => r.AddAsync(Arg.Any<FluxoDeployEntity>(), Arg.Any<CancellationToken>()))
                .Do(ci => _fluxos.Add(ci.Arg<FluxoDeployEntity>().ComId(_fluxos.Count + 1)));

            DeployRepo.ObterPorFluxosAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
                .Returns(ci => (IReadOnlyList<DeployEntity>)_deploys
                    .Where(d => ci.Arg<IEnumerable<int>>().Contains(d.IdFluxoDeploy))
                    .ToList());

            EtapaRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Catalogo);
            EtapaRepo.ObterCatalogoAsync(Arg.Any<CancellationToken>()).Returns(Catalogo);

            ConfigRepo.GetBySistemaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Configuracao);
            ConfigRepo.ExisteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

            AprovadorRepo.GetByUsuarioAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ci => Aprovadores.FirstOrDefault(a => a.Usuario == ci.Arg<string>()));
            AprovadorRepo.ObterPorEquipeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ci => (IReadOnlyList<AprovadorTecnicoEntity>)Aprovadores.Where(a => a.Equipe == ci.Arg<string>()).ToList());
            AprovadorRepo.ObterPorIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
                .Returns(ci => (IReadOnlyList<AprovadorTecnicoEntity>)Aprovadores.Where(a => ci.Arg<IEnumerable<int>>().Contains(a.Id)).ToList());
            AprovadorRepo.ObterEquipesTecnicasAsync(Arg.Any<CancellationToken>())
                .Returns(_ => Aprovadores.Where(a => a.Status == StatusEnum.ATIVO).Select(a => a.Equipe).Distinct().ToList());

            GerenciadorTarefas.ObterTarefas(Arg.Any<List<int>>())
                .Returns(ci => ci.Arg<List<int>>()
                    .Select(id => new TarefaDto { Id = id, Status = _statusTarefas.GetValueOrDefault(id, "Pendente") })
                    .ToList());
        }

        public FluxoDeployEntity Fluxo(int id) => _fluxos.Single(f => f.Id == id);

        public IReadOnlyList<DeployEntity> DeploysDoFluxo(int id) => _deploys.Where(d => d.IdFluxoDeploy == id).ToList();

        public StatusEnum StatusEtapa(int id, string sigla) => Fluxo(id).StatusEtapa(sigla);

        public void StatusTarefa(int idTarefa, string status) => _statusTarefas[idTarefa] = status;

        public DeployEntity Deploy(int idFluxo, string branch, StatusEnum statusAnalise = StatusEnum.CONCLUIDO, params string[] severidades)
        {
            var deploy = Construtor.Deploy(_deploys.Count + 1, idFluxo, branch).ComAnalise(status: statusAnalise, severidades: severidades);
            _deploys.Add(deploy);
            return deploy;
        }

        public async Task<FluxoDeployEntity> CriarFluxo(params string[] etapas)
        {
            await new CriarFluxoDeployHandler(FluxoRepo, new FluxoDeployService(), EtapaRepo, new EtapaService(), ConfigRepo)
                .Handle(new CriarFluxoDeployCommand("sistema", "usuario", (etapas.Length == 0 ? TodasEtapas : etapas).ToList()), CancellationToken.None);
            return _fluxos.Last();
        }

        public Task Avancar(int id)
            => new AlterarEtapaSeguinteFluxoDeployHandler(FluxoRepo, EtapaRepo, Regras(), new FluxoDeployService())
                .Handle(new AlterarEtapaSeguinteFluxoDeployCommand { Id = id }, CancellationToken.None);

        public async Task AvancarAte(int id, string sigla)
        {
            while (!Fluxo(id).EtapaEstaExecutando(sigla))
                await Avancar(id);
        }

        public Task Voltar(int id, string etapa)
            => new AlterarEtapaEspecificaFluxoDeployHandler(FluxoRepo, new FluxoDeployService())
                .Handle(new AlterarEtapaEspecificaFluxoDeployCommand { Id = id, Etapa = etapa }, CancellationToken.None);

        public Task Reconfigurar(int id, params string[] etapas)
            => new AlterarEtapasFluxoDeployHandler(FluxoRepo, EtapaRepo, new FluxoDeployService())
                .Handle(new AlterarEtapasFluxoDeployCommand { Id = id, Etapas = etapas.ToList() }, CancellationToken.None);

        public Task Cancelar(int id)
            => new CancelarFluxoDeployHandler(FluxoRepo).Handle(new CancelarFluxoDeployCommand { Id = id }, CancellationToken.None);

        public Task VersaoTarefas(int id, int idProjeto, int idVersao)
            => new CriarVersaoTarefasFluxoDeployHandler(FluxoRepo)
                .Handle(new CriarVersaoTarefasFluxoDeployCommand(id, idProjeto, idVersao), CancellationToken.None);

        public Task Gmud(int id, int? idGmud, string justificativa = null)
            => new CriarGmudFluxoDeployHandler(FluxoRepo, Construtor.ConfiguracaoTarefas)
                .Handle(new CriarGmudFluxoDeployCommand(id, "usuario", justificativa, idGmud), CancellationToken.None);

        public Task Crs(int id, params string[] crs)
            => new CriarCrsBdFluxoDeployHandler(FluxoRepo)
                .Handle(new CriarCrsBdFluxoDeployCommand
                {
                    IdFluxoDeploy = id,
                    CrsBd = crs.Select(c => new BdCrsDtoRequest { Crs = c, Url = $"https://tarefas.exemplo/issues/{c}" }).ToList()
                }, CancellationToken.None);

        public Task AprovacaoTecnica(int id, string usuario, bool aprovado, string justificativa = null)
            => new CriarAprovacaoTecnicaFluxoDeployHandler(FluxoRepo, AprovadorRepo)
                .Handle(new CriarAprovacaoTecnicaFluxoDeployCommand(id, usuario, aprovado ? 'S' : 'N', justificativa), CancellationToken.None);

        public Task AprovacoesNegocio(int id, params int[] tarefas)
            => new CriarAprovacaoNegocioFluxoDeployHandler(FluxoRepo)
                .Handle(new CriarAprovacaoNegocioFluxoDeployCommand
                {
                    IdFluxoDeploy = id,
                    AprovacoesNegocio = tarefas.Select(t => new AprovacaoNegocioDtoRequest { Tarefa = t, Url = $"https://tarefas.exemplo/issues/{t}" }).ToList()
                }, CancellationToken.None);

        public async Task PrepararTodasAsEtapas(int id)
        {
            await VersaoTarefas(id, 1, 2);
            await Gmud(id, 7);
            await Crs(id, "CRS-1");
            await AprovacoesNegocio(id, 10);
            StatusTarefa(10, "Aprovado");
            foreach (var branch in new[] { "DEV", "QA", "HML", "PRD" })
                Deploy(id, branch);
        }

        private IEtapaRules[] Regras() => new IEtapaRules[]
        {
            new AberturaRules(Servicos),
            new VersaoTarefasRules(Servicos),
            new GmudRules(Servicos),
            new BancoDeDadosRules(Servicos),
            new EquipeTecnicaRules(Servicos, AprovadorRepo),
            new AmbienteDevRules(DeployRepo, ConfigRepo, Servicos),
            new AmbienteQaRules(DeployRepo, ConfigRepo, Servicos),
            new AmbienteHmlRules(DeployRepo, ConfigRepo, Servicos),
            new AreaNegocioRules(Servicos, GerenciadorTarefas),
            new AmbientePrdRules(DeployRepo, ConfigRepo, Servicos)
        };
    }
}
