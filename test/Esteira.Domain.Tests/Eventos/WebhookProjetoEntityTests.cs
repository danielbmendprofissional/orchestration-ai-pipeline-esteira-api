using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Eventos;

namespace Esteira.Domain.Tests.Eventos
{
    // T1 (spec 01-tempo-real, "Dados"; critério 9): transições de WebhookProjeto.
    [Trait("Fatia", "T1")]
    public class WebhookProjetoEntityTests
    {
        private static readonly DateTime T0 = new(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime T1 = T0.AddMinutes(1);

        private static WebhookProjetoEntity Em(EstadoWebhookProjeto estado)
        {
            var w = WebhookProjetoEntity.Criar(11, T0);
            switch (estado)
            {
                case EstadoWebhookProjeto.Registrado: w.Registrar(99, T0); break;
                case EstadoWebhookProjeto.Falhou: w.Falhar("webhook.gitlab-indisponivel", T0); break;
            }
            return w;
        }

        [Fact, DisplayName("Deve nascer pendente, com PublicId v7, sem hook nem erro")]
        public void Criar_NascePendente()
        {
            var w = WebhookProjetoEntity.Criar(11, T0);

            w.Should().BeAssignableTo<EntityBase>();
            w.PublicId.Version.Should().Be(7);
            (w.ProjetoId, w.Estado, w.GitLabHookId, w.CodigoErro, w.AtualizadaEm)
                .Should().Be((11L, EstadoWebhookProjeto.Pendente, (long?)null, (string?)null, T0));
        }

        [Fact, DisplayName("Deve ir de pendente para registrado gravando o GitLabHookId")]
        public void Registrar_DePendente()
        {
            var w = Em(EstadoWebhookProjeto.Pendente);

            var r = w.Registrar(99, T1);

            r.IsSuccess.Should().BeTrue();
            (w.Estado, w.GitLabHookId, w.CodigoErro, w.AtualizadaEm).Should().Be((EstadoWebhookProjeto.Registrado, (long?)99, (string?)null, T1));
        }

        [Fact, DisplayName("Deve ir de pendente para falhou gravando o CodigoErro")]
        public void Falhar_DePendente()
        {
            var w = Em(EstadoWebhookProjeto.Pendente);

            var r = w.Falhar("webhook.sem-permissao", T1);

            r.IsSuccess.Should().BeTrue();
            (w.Estado, w.CodigoErro, w.AtualizadaEm).Should().Be((EstadoWebhookProjeto.Falhou, "webhook.sem-permissao", T1));
        }

        [Fact, DisplayName("Deve voltar de falhou para pendente limpando o CodigoErro")]
        public void Reexecutar_DeFalhou()
        {
            var w = Em(EstadoWebhookProjeto.Falhou);

            var r = w.Reexecutar(T1);

            r.IsSuccess.Should().BeTrue();
            (w.Estado, w.CodigoErro, w.AtualizadaEm).Should().Be((EstadoWebhookProjeto.Pendente, (string?)null, T1));
        }

        [Fact, DisplayName("Deve limpar o CodigoErro ao registrar depois de uma reexecução")]
        public void Registrar_AposReexecucao_LimpaErro()
        {
            var w = Em(EstadoWebhookProjeto.Falhou);
            w.Reexecutar(T0);

            w.Registrar(5, T1);

            (w.Estado, w.CodigoErro, w.GitLabHookId).Should().Be((EstadoWebhookProjeto.Registrado, (string?)null, (long?)5));
        }

        public static TheoryData<EstadoWebhookProjeto, string> Invalidas => new()
        {
            { EstadoWebhookProjeto.Pendente, "reexecutar" },
            { EstadoWebhookProjeto.Registrado, "registrar" },
            { EstadoWebhookProjeto.Registrado, "falhar" },
            { EstadoWebhookProjeto.Registrado, "reexecutar" },
            { EstadoWebhookProjeto.Falhou, "registrar" },
            { EstadoWebhookProjeto.Falhou, "falhar" },
        };

        [Theory, MemberData(nameof(Invalidas)), DisplayName("Deve recusar qualquer outra transição com webhook.transicao-invalida, sem alterar a linha")]
        public void TransicaoInvalida(EstadoWebhookProjeto origem, string acao)
        {
            var w = Em(origem);
            var antes = (w.Estado, w.GitLabHookId, w.CodigoErro, w.AtualizadaEm);

            var r = acao switch
            {
                "registrar" => w.Registrar(1, T1),
                "falhar" => w.Falhar("webhook.gitlab-indisponivel", T1),
                _ => w.Reexecutar(T1)
            };

            r.IsSuccess.Should().BeFalse();
            r.Errors[0].Code.Should().Be("webhook.transicao-invalida");
            (w.Estado, w.GitLabHookId, w.CodigoErro, w.AtualizadaEm).Should().Be(antes);
        }
    }
}
