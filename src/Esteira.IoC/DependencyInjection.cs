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
            services.AddScoped<Esteira.Application.Entregas.Pipeline.IProjetoDaPipelineQuery, Esteira.Data.Entregas.ProjetoDaPipelineQuery>();
            services.AddScoped<Esteira.Application.Entregas.Pipeline.PorteiroDaPipeline>();
            // E1.4: passos 3–9 do `esteira-abrir`.
            services.AddScoped<Esteira.Application.Entregas.Pipeline.IAbrirPipeline, Esteira.Application.Entregas.Pipeline.AbrirPipelineHandler>();
            services.AddScoped<Esteira.Application.Entregas.Pipeline.IProjetoPorCaminhoQuery, Esteira.Data.Entregas.ProjetoPorCaminhoQuery>();
            services.AddScoped<Esteira.Application.Entregas.Pipeline.IDestinosDaPipelineQuery, Esteira.Data.Entregas.DestinosDaPipelineQuery>();
            // E1b: `Features:E1b`, lida da configuração a cada uso.
            services.AddScoped<Esteira.Application.Entregas.IOpcoesDaEntrega, OpcoesDaEntrega>();
            // X3: `Pipeline:TetoCustoPadraoUsd` (X3.3 consolida o uso no esteira-abrir e no detalhe).
            services.AddScoped<Esteira.Application.Aplicacoes.IOpcoesDosAgentes, OpcoesDosAgentes>();
            services.AddScoped<Esteira.Application.Entregas.Queries.Obter.IEntregaQuery,Esteira.Data.Entregas.EntregaQuery>();
            services.AddScoped<Esteira.Application.Entregas.Queries.Obter.IUrlDoGitLab, UrlDoGitLab>();
            services.AddScoped<IPapelRepositorio, PapelRepositorio>();
            services.AddScoped<IAcaoBotRepositorio, AcaoBotRepositorio>();
            services.AddScoped<Esteira.Domain.Eventos.IWebhookProjetoRepositorio, Esteira.Data.Eventos.WebhookProjetoRepositorio>();
            services.AddScoped<PreparadorDeDecisao>();
            services.AddScoped<IAcoesDaFatiaQuery, AcoesDaFatiaQuery>();
            services.AddScoped<IOpcoesDeRevisao, OpcoesDeRevisao>();
            services.AddScoped<RevisaoDoPainel>();
            services.AddScoped<RegistrarProgressoDaAcao>();
            services.AddScoped<ContextoGitLabDaAcao>();
            services.AddScoped<ConclusaoDaAcaoBot>();
            services.AddScoped<ExecutorDeAcaoBot>();
            services.AddScoped<ConsultaDePipelines>();
            // T1.3: webhook do GitLab (Pipeline Hook e Merge Request Hook).
            services.AddScoped<Esteira.Application.Eventos.IProjetoDoWebhookQuery, Esteira.Data.Eventos.ProjetoDoWebhookQuery>();
            services.AddScoped<Esteira.Application.Eventos.ITratadorDeWebhookGitLab, Esteira.Application.Eventos.TratadorDeWebhookGitLab>();
            services.AddScoped<Esteira.Application.Eventos.IOpcoesDoWebhook, OpcoesDoWebhook>();
            services.AddScoped<Esteira.Application.Eventos.RegistradorDeWebhook>();
            services.AddScoped<Esteira.Application.Eventos.PartidaDoWebhook>();
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
