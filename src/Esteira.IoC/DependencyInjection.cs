using FluentValidation;
using Esteira.Application.Behavios;
using Esteira.Application.Common.Events;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
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
