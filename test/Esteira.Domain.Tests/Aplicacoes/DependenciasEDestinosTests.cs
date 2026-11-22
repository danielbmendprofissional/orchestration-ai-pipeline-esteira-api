using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Domain.Tests.Aplicacoes
{
    // A1.4: regras do grafo de dependência entre Projetos (critério 15) e da configuração do Destino (critério 10).
    [Trait("Fatia", "A1")]
    public class DependenciasEDestinosTests
    {
        private static readonly long[] Projetos = [1, 2, 3, 4];

        private static IEnumerable<(long, long)> Arestas(params (long, long)[] arestas) => arestas;

        [Fact, DisplayName("Critério 15: aceita dependência entre Projetos da mesma Aplicação sem ciclo")]
        public void Dependencia_Valida()
        {
            var r = GrafoDeDependencias.Validar(1, [2, 3], Projetos, Arestas((2, 4)));

            r.IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("Critério 15: lista vazia é válida (remove todas as dependências)")]
        public void Dependencia_Vazia()
        {
            GrafoDeDependencias.Validar(1, [], Projetos, Arestas((1, 2))).IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("Critério 15: recusa dependência de um Projeto nele mesmo")]
        public void Dependencia_Propria()
        {
            var r = GrafoDeDependencias.Validar(1, [1], Projetos, Arestas());

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.dependencia-invalida");
        }

        [Fact, DisplayName("Critério 15: recusa dependência de Projeto que não é da Aplicação")]
        public void Dependencia_OutraAplicacao()
        {
            var r = GrafoDeDependencias.Validar(1, [99], Projetos, Arestas());

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.dependencia-invalida");
        }

        [Fact, DisplayName("Critério 15: recusa ciclo direto (A depende de B, B já depende de A)")]
        public void Dependencia_CicloDireto()
        {
            var r = GrafoDeDependencias.Validar(1, [2], Projetos, Arestas((2, 1)));

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.dependencia-invalida");
        }

        [Fact, DisplayName("Critério 15: recusa ciclo indireto (A->B, B->C, C->A)")]
        public void Dependencia_CicloIndireto()
        {
            var r = GrafoDeDependencias.Validar(1, [2], Projetos, Arestas((2, 3), (3, 1)));

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.dependencia-invalida");
        }

        [Fact, DisplayName("Critério 15: ao substituir, as dependências antigas do próprio Projeto não contam")]
        public void Dependencia_SubstituiaAntigas()
        {
            // 1 dependia de 2; agora passa a depender só de 3. 3 depende de 2: sem ciclo.
            var r = GrafoDeDependencias.Validar(1, [3], Projetos, Arestas((1, 2), (3, 2)));

            r.IsSuccess.Should().BeTrue();
        }

        [Theory, DisplayName("Critério 10: recusa chave com cara de segredo (sem diferenciar maiúsculas) e orienta a usar variável do GitLab")]
        [InlineData("DB_PASSWORD")]
        [InlineData("senha")]
        [InlineData("Api_Secret")]
        [InlineData("GITHUB_TOKEN")]
        [InlineData("ssh_key")]
        [InlineData("AwsCredential")]
        public void Destino_Segredo_Criar(string chave)
        {
            var r = DestinoEntity.Criar(1, AmbienteDeploy.Hml, "k8s", new Dictionary<string, string> { [chave] = "valor-super-secreto" }, 0);

            r.IsSuccess.Should().BeFalse();
            var erro = r.Errors.Should().ContainSingle().Subject;
            erro.Code.Should().Be("destino.configuracao-com-segredo");
            erro.Message.Should().Contain("variável").And.Contain("GitLab");
            erro.Message.Should().NotContain("valor-super-secreto");
        }

        [Fact, DisplayName("Critério 10: aceita chaves sem cara de segredo")]
        public void Destino_SemSegredo()
        {
            var cfg = new Dictionary<string, string> { ["KUBE_CONTEXT"] = "hml", ["NAMESPACE"] = "valuto", ["REPLICAS"] = "2" };

            DestinoEntity.Criar(1, AmbienteDeploy.Hml, "k8s", cfg, 0).IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("Critério 10: ao alterar o Destino a configuração também é conferida e nada muda na recusa")]
        public void Destino_Alterar()
        {
            var d = DestinoEntity.Criar(1, AmbienteDeploy.Hml, "k8s", new Dictionary<string, string> { ["A"] = "1" }, 0).Value;

            var ruim = d.Alterar("novo", new Dictionary<string, string> { ["TOKEN"] = "x" }, 3);
            var bom = d.Alterar(" outro ", new Dictionary<string, string> { ["B"] = "2" }, 4);

            ruim.Errors.Should().ContainSingle().Which.Code.Should().Be("destino.configuracao-com-segredo");
            bom.IsSuccess.Should().BeTrue();
            (d.Nome, d.Ordem).Should().Be(("outro", 4));
            d.Configuracao.Should().BeEquivalentTo(new Dictionary<string, string> { ["B"] = "2" });
        }

        [Fact, DisplayName("Alterar Destino sem nome recusa (destino.nome-obrigatorio)")]
        public void Destino_Alterar_SemNome()
        {
            var d = DestinoEntity.Criar(1, AmbienteDeploy.Hml, "k8s", new Dictionary<string, string>(), 0).Value;

            d.Alterar(" ", new Dictionary<string, string>(), 0).Errors.Should().ContainSingle().Which.Code.Should().Be("destino.nome-obrigatorio");
            d.Nome.Should().Be("k8s");
        }
    }
}
