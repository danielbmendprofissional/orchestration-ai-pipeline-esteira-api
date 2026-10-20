using FluentValidation;
using Esteira.AcL.Integracoes;
using Esteira.Application.AprovadoresTecnicos.Interfaces;
using Esteira.Application.Behavios;
using Esteira.Application.Common.Events;
using Esteira.Application.Deploys.Commands.CriarDeploy;
using Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy.Rules;
using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Data.AprovadoresTecnicos.Repositories;
using Esteira.Data.Common.Events;
using Esteira.Data.Common.Persistence;
using Esteira.Data.ConfiguracoesSistema.Repositories;
using Esteira.Data.Deploys.Repositories;
using Esteira.Data.Etapas.Repositories;
using Esteira.Data.FluxosDeploy.Repositories;
using Esteira.Domain.AprovadoresTecnicos.Interfaces;
using Esteira.Domain.ConfiguracoesSistema.Interfaces;
using Esteira.Domain.Deploys.Interfaces;
using Esteira.Domain.Etapas.Interfaces;
using Esteira.Domain.Etapas.Services;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Application.Interfaces.Cache;
using Esteira.Infra.Cache;
using MediatR;
using Esteira.Application.Deploys.Interfaces;
using Esteira.Application.Etapas.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.IoC
{
    public static class DependencyInjection
    {
        public static void AddDependencyInjection(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

            //Service
            services.AddScoped<IFluxoDeployService, FluxoDeployService>();
            services.AddScoped<IEtapaService, EtapaService>();

            //Service - Etapa fluxo deploy
            services.AddScoped<IEtapaRulesService, EtapaAberturaService>();
            services.AddScoped<IEtapaRulesService, EtapaVersaoTarefasService>();
            services.AddScoped<IEtapaRulesService, EtapaGmudService>();
            services.AddScoped<IEtapaRulesService, EtapaAmbienteDevService>();
            services.AddScoped<IEtapaRulesService, EtapaAmbienteHmlService>();
            services.AddScoped<IEtapaRulesService, EtapaAmbienteQaService>();
            services.AddScoped<IEtapaRulesService, EtapaAmbientePrdService>();
            services.AddScoped<IEtapaRulesService, EtapaAreaNegocioService>();
            services.AddScoped<IEtapaRulesService, EtapaEquipeTecnicaService>();
            services.AddScoped<IEtapaRulesService, EtapaBancoDeDadosService>();

            //Rules
            services.AddScoped<IEtapaRules, AberturaRules>();
            services.AddScoped<IEtapaRules, VersaoTarefasRules>();
            services.AddScoped<IEtapaRules, GmudRules>();
            services.AddScoped<IEtapaRules, AmbienteDevRules>();
            services.AddScoped<IEtapaRules, AmbienteHmlRules>();
            services.AddScoped<IEtapaRules, AmbienteQaRules>();
            services.AddScoped<IEtapaRules, AmbientePrdRules>();
            services.AddScoped<IEtapaRules, AreaNegocioRules>();
            services.AddScoped<IEtapaRules, BancoDeDadosRules>();
            services.AddScoped<IEtapaRules, EquipeTecnicaRules>();

            //Repository - Write
            services.AddScoped<IFluxoDeployRepository, FluxoDeployRepository>();
            services.AddScoped<IDeployRepository, DeployRepository>();
            services.AddScoped<IEtapaRepository, EtapaRepository>();
            services.AddScoped<IAprovadorTecnicoRepository, AprovadorTecnicoRepository>();
            services.AddScoped<IConfiguracaoSistemaRepository, ConfiguracaoSistemaRepository>();

            //Repository - Read
            services.AddScoped<IFluxoDeployQueryRepository, FluxoDeployQueryRepository>();
            services.AddScoped<IDeployQueryRepository, DeployQueryRepository>();
            services.AddScoped<IEtapaQueryRepository, EtapaQueryRepository>();
            services.AddScoped<IAprovadorTecnicoQueryRepository, AprovadorTecnicoQueryRepository>();

            //Integrações
            services.AddScoped<IScannerDast, ScannerDastNaoConfigurado>();
            services.AddScoped<IGerenciadorTarefas, GerenciadorTarefasNaoConfigurado>();

            //Feature
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssemblyContaining<CriarDeployHandler>());
            services.AddValidatorsFromAssembly(typeof(CriarDeployValidator).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AutorizacaoBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CacheBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        }
    }
}
