using Esteira.Data.Common.Persistence;
using Esteira.IoC;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Esteira.Integration.Tests.Infraestrutura
{
    // Monta o mesmo registro da API (AddMensageria + AddDependencyInjection) contra os containers.
    internal static class HostDeTeste
    {
        public static IHost Criar(
            InfraestruturaFixture infra,
            string ambienteMensageria,
            Action<IBusRegistrationConfigurator>? consumidores = null)
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(infra.Configuracao(ambienteMensageria));

            builder.Services.AddDbContext<EsteiraContext>(options => options.UseNpgsql(infra.ConnectionString));
            builder.Services.AddMensageria(builder.Configuration, producao: false, consumidores);
            builder.Services.AddDependencyInjection();

            return builder.Build();
        }

        public static string AmbienteUnico() => $"t{Guid.NewGuid():N}"[..12];
    }
}
