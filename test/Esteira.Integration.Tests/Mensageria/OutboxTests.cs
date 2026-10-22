using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Data.Common.Persistence;
using Esteira.Integration.Tests.Infraestrutura;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Esteira.Integration.Tests.Mensageria
{
    // Critérios 1–4 da spec M0 (specs/feature/base/01-esqueleto-do-modelo.md), ADR-0010.
    public class OutboxTests(InfraestruturaFixture infra)
    {
        private static readonly TimeSpan Entrega = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan Silencio = TimeSpan.FromSeconds(5);

        private static void RegistrarConsumidor(IBusRegistrationConfigurator x) => x.AddConsumer<ConsumidorDeTeste>();

        private static async Task PublicarEmTransacaoAsync(IHost host, Guid marcador, bool confirmar)
        {
            await using var escopo = host.Services.CreateAsyncScope();
            var uow = escopo.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var publicador = escopo.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            await uow.BeginTransactionAsync();
            await publicador.Publish(new MensagemDeTeste(marcador));

            if (confirmar)
                await uow.CommitAsync();
            else
                await uow.RollbackAsync();
        }

        private async Task<int> MensagensNoOutboxAsync(Guid marcador)
        {
            var opcoes = new DbContextOptionsBuilder<EsteiraContext>().UsarPostgres(infra.ConnectionString).Options;
            await using var contexto = new EsteiraContext(opcoes, null!);
            return await contexto.Set<OutboxMessage>().CountAsync(m => m.Body.Contains(marcador.ToString()));
        }

        [Fact, DisplayName("Deve entregar a mensagem publicada num command depois do commit")]
        public async Task Commit_DeveEntregar()
        {
            using var host = HostDeTeste.Criar(infra, HostDeTeste.AmbienteUnico(), RegistrarConsumidor);
            await host.StartAsync(TestContext.Current.CancellationToken);
            var marcador = Guid.NewGuid();

            await PublicarEmTransacaoAsync(host, marcador, confirmar: true);

            (await ConsumidorDeTeste.AguardarAsync(marcador, Entrega)).Should().BeTrue();
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        [Fact, DisplayName("Não deve gravar nem entregar a mensagem quando a transação desfaz")]
        public async Task Rollback_NaoDeveGravarNemEntregar()
        {
            using var host = HostDeTeste.Criar(infra, HostDeTeste.AmbienteUnico(), RegistrarConsumidor);
            await host.StartAsync(TestContext.Current.CancellationToken);
            var marcador = Guid.NewGuid();

            await PublicarEmTransacaoAsync(host, marcador, confirmar: false);

            (await MensagensNoOutboxAsync(marcador)).Should().Be(0);
            (await ConsumidorDeTeste.AguardarAsync(marcador, Silencio)).Should().BeFalse();
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        [Fact, DisplayName("Deve entregar depois que a API sobe a mensagem confirmada enquanto ela estava parada")]
        public async Task Reinicio_DeveEntregarPendente()
        {
            var ambiente = HostDeTeste.AmbienteUnico();
            var marcador = Guid.NewGuid();

            // Commit com o host parado: a mensagem fica só na tabela de outbox.
            using (var parado = HostDeTeste.Criar(infra, ambiente, RegistrarConsumidor))
                await PublicarEmTransacaoAsync(parado, marcador, confirmar: true);

            (await MensagensNoOutboxAsync(marcador)).Should().Be(1);
            ConsumidorDeTeste.Vezes(marcador).Should().Be(0);

            using var reiniciado = HostDeTeste.Criar(infra, ambiente, RegistrarConsumidor);
            await reiniciado.StartAsync(TestContext.Current.CancellationToken);

            (await ConsumidorDeTeste.AguardarAsync(marcador, Entrega)).Should().BeTrue();
            await reiniciado.StopAsync(TestContext.Current.CancellationToken);
        }

        [Fact, DisplayName("Deve processar uma vez só a mesma mensagem recebida duas vezes")]
        public async Task Duplicada_DeveProcessarUmaVez()
        {
            using var host = HostDeTeste.Criar(infra, HostDeTeste.AmbienteUnico(), RegistrarConsumidor);
            await host.StartAsync(TestContext.Current.CancellationToken);
            var bus = host.Services.GetRequiredService<IBus>();
            var marcador = Guid.NewGuid();
            var messageId = NewId.NextGuid();

            await bus.Publish(new MensagemDeTeste(marcador), c => c.MessageId = messageId, TestContext.Current.CancellationToken);
            await bus.Publish(new MensagemDeTeste(marcador), c => c.MessageId = messageId, TestContext.Current.CancellationToken);

            (await ConsumidorDeTeste.AguardarAsync(marcador, Entrega)).Should().BeTrue();
            await Task.Delay(Silencio, TestContext.Current.CancellationToken);
            ConsumidorDeTeste.Vezes(marcador).Should().Be(1);
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
