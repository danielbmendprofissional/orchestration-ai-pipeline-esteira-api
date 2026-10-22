using Esteira.Data.Common.Persistence;
using Esteira.IoC.Configuracoes;
using Esteira.Messaging.Configuracao;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.IoC
{
    public static class Mensageria
    {
        // RabbitMQ só interno (ADR-0008) com outbox/inbox em tabela no EsteiraContext (ADR-0010):
        // publicar dentro de um command grava na transação do SaveChanges e entrega depois do commit;
        // consumer recebe cada mensagem uma vez só.
        public static IServiceCollection AddMensageria(
            this IServiceCollection services,
            IConfiguration configuration,
            bool producao,
            Action<IBusRegistrationConfigurator>? registrarConsumidores = null)
        {
            var ambiente = AmbienteMensageria.Resolver(configuration["RabbitMQ:Ambiente"] ?? string.Empty, producao);

            // Sem isso o host sobe antes das filas estarem ligadas e a primeira publicação se perde.
            services.Configure<MassTransitHostOptions>(opcoes => opcoes.WaitUntilStarted = true);

            services.AddMassTransit(x =>
            {
                registrarConsumidores?.Invoke(x);

                x.AddEntityFrameworkOutbox<EsteiraContext>(outbox =>
                {
                    outbox.UsePostgres();
                    outbox.UseBusOutbox();
                });

                x.SetEndpointNameFormatter(new SufixoAmbienteEndpointNameFormatter(KebabCaseEndpointNameFormatter.Instance, ambiente));
                x.AddConfigureEndpointsCallback((context, _, endpoint) => endpoint.UseEntityFrameworkOutbox<EsteiraContext>(context));

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.MessageTopology.SetEntityNameFormatter(new SufixoAmbienteEntityNameFormatter(cfg.MessageTopology.EntityNameFormatter, ambiente));

                    cfg.Host(
                        Configuracao.Obrigatoria(configuration, "RabbitMQ:Host"),
                        ushort.Parse(Configuracao.Obrigatoria(configuration, "RabbitMQ:Port")),
                        Configuracao.Obrigatoria(configuration, "RabbitMQ:VirtualHost"),
                        h =>
                        {
                            h.Username(Configuracao.Obrigatoria(configuration, "RabbitMQ:UserName"));
                            h.Password(Configuracao.Obrigatoria(configuration, "RabbitMQ:Password"));
                        });
                    cfg.ConfigureEndpoints(context);
                });
            });

            return services;
        }
    }
}
