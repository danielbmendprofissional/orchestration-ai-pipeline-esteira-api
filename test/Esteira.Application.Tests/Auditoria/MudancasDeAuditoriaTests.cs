using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Auditoria;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Tests.Auditoria
{
    // X1.4, critérios 8 e 9: mudanças antes/depois montadas na leitura, segredo mascarado.
    [Trait("Fatia", "X1")]
    public class MudancasDeAuditoriaTests
    {
        [Fact, DisplayName("8: Criou lista os campos do depois, com antes ausente (—)")]
        public void Criou()
            => MudancasDeAuditoria.Montar(AcaoAuditoria.Criou, null, """{"nome":"A","replicas":2}""")
                .Should().Equal(new MudancaDto("nome", "—", "A"), new MudancaDto("replicas", "—", "2"));

        [Fact, DisplayName("8: Removeu lista os campos do antes, com depois ausente (—)")]
        public void Removeu()
            => MudancasDeAuditoria.Montar(AcaoAuditoria.Removeu, """{"nome":"A"}""", null)
                .Should().Equal(new MudancaDto("nome", "A", "—"));

        [Fact, DisplayName("8: Alterou lista só o que mudou, com objetos aninhados em caminho com ponto e campo novo/ausente como —")]
        public void Alterou_SoOQueMudou_Aninhado()
            => MudancasDeAuditoria.Montar(AcaoAuditoria.Alterou,
                """{"nome":"A","destino":{"replicas":1,"url":"x"},"velho":"v"}""",
                """{"nome":"A","destino":{"replicas":2,"url":"x"},"novo":true}""")
                .Should().BeEquivalentTo(new[]
                {
                    new MudancaDto("destino.replicas", "1", "2"),
                    new MudancaDto("velho", "v", "—"),
                    new MudancaDto("novo", "—", "true")
                });

        [Theory, DisplayName("9: campo com nome de segredo sai mascarado em antes e depois, sem vazar o valor")]
        [InlineData("password")]
        [InlineData("DB_Senha")]
        [InlineData("clientSecret")]
        [InlineData("apiToken")]
        [InlineData("chaveKey")]
        [InlineData("credential")]
        public void Segredo_Mascarado(string campo)
        {
            var mudancas = MudancasDeAuditoria.Montar(AcaoAuditoria.Alterou,
                $$"""{"{{campo}}":"velho-segredo"}""", $$"""{"{{campo}}":"novo-segredo"}""");

            mudancas.Should().ContainSingle().Which.Should().Be(new MudancaDto(campo, "••••", "••••"));
        }

        [Fact, DisplayName("9: segredo dentro de lista (array) também é mascarado, sem vazar o valor")]
        public void Segredo_EmLista()
        {
            var m = MudancasDeAuditoria.Montar(AcaoAuditoria.Criou, null, """{"destinos":[{"nome":"d","password":"p4ss"}]}""");

            m.Should().Contain(new MudancaDto("destinos[0].password", "—", "••••"));
            m.Should().NotContain(x => x.Depois.Contains("p4ss"));
        }

        [Fact, DisplayName("9: segredo aninhado também é mascarado; segredo criado mostra antes — e depois ••••")]
        public void Segredo_Aninhado()
            => MudancasDeAuditoria.Montar(AcaoAuditoria.Criou, null, """{"destino":{"token":"abc","url":"x"}}""")
                .Should().Equal(new MudancaDto("destino.token", "—", "••••"), new MudancaDto("destino.url", "—", "x"));
    }
}
