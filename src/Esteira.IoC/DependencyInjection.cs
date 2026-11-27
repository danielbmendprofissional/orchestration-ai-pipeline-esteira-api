using FluentValidation;
using Esteira.Application.Behavios;
using Esteira.Application.Common.Events;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Data.Fatias;
using Esteira.Data.Aplicacoes;
using Esteira.Application.Interfaces.Cache;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Data.Common.Events;
using Esteira.Data.Common.Persistence;
using Esteira.Infra.Cache;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            services.TryAddSingleton(TimeProvider.System);
            services.AddScoped<IAuditoria, AuditoriaEf>();
            services.AddScoped<IAcessoAplicacaoQuery, AcessoAplicacaoQuery>();
            services.AddScoped<IAutorizacaoAplicacao, AutorizacaoAplicacao>();
            services.AddScoped<IPessoaRepositorio, PessoaRepositorio>();
            services.AddScoped<IRegistradorDePessoa, RegistradorDePessoa>();
            services.AddScoped<IAplicacaoRepositorio, AplicacaoRepositorio>();
            services.AddScoped<IAplicacaoQuery, AplicacaoQuery>();
            services.AddScoped<IPainelAplicacaoQuery, PainelAplicacaoQuery>();
            services.AddScoped<IProjetoRepositorio, ProjetoRepositorio>();
            services.AddScoped<Esteira.Domain.Entregas.IEntregaRepositorio, Esteira.Data.Entregas.EntregaRepositorio>();
            services.AddScoped<Esteira.Domain.Entregas.IOcupacaoAmbienteRepositorio, Esteira.Data.Entregas.OcupacaoAmbienteRepositorio>();
            services.AddScoped<IPapelRepositorio, PapelRepositorio>();
            services.AddScoped<IAcaoBotRepositorio, AcaoBotRepositorio>();
            services.AddScoped<PreparadorDeDecisao>();
            services.AddScoped<IAcoesDaFatiaQuery, AcoesDaFatiaQuery>();
            services.AddScoped<IOpcoesDeRevisao, OpcoesDeRevisao>();
            services.AddScoped<RevisaoDoPainel>();
            services.AddScoped<RegistrarProgressoDaAcao>();
            services.AddScoped<ContextoGitLabDaAcao>();
            services.AddScoped<ConclusaoDaAcaoBot>();
            services.AddScoped<ExecutorDeAcaoBot>();
            services.AddScoped<ConsultaDePipelines>();
            services.AddScoped<IPapelQuery, PapelQuery>();
            services.AddScoped<IPessoaQuery, PessoaQuery>();

            //Feature
            var application = typeof(ValidationBehavior<,>).Assembly;
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(application));
            services.AddValidatorsFromAssembly(application);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CacheBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        }
    }
}
