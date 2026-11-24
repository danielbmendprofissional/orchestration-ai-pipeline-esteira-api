using AwesomeAssertions;
using Esteira.Application.Interfaces.Integracoes;

namespace Esteira.Application.Tests.Fatias
{
    // A P2 dizia "a porta do GitLab não tem nenhum método de escrita" (P2.5, critério 17) e a spec da P2b é o último
    // lugar onde isso vale: a escrita é do executor da P2b. Este teste guarda a superfície nova — a leitura do Painel
    // continua e a escrita da Ação do bot existe — para que nenhum dos dois lados suma por acidente.
    [Trait("Fatia", "P2b")]
    public class IGitLabLeituraEEscritaDaAcaoTests
    {
        [Fact]
        public void IGitLab_tem_a_leitura_e_a_escrita_da_acao_do_bot()
        {
            var nomes = typeof(IGitLab).GetMethods().Select(m => m.Name).ToList();

            string[] esperados =
            [
                // leitura (Painel, e a nota que o executor procura pelo marcador)
                "ObterMergeRequestAsync",
                "ObterPipelineAsync",
                "ListarNotasMergeRequestAsync",
                // escrita da Ação do bot
                "ComentarMergeRequestAsync",
                "FecharMergeRequestAsync",
                "ReabrirMergeRequestAsync",
                "MergearMergeRequestAsync",
                "CriarTriggerTokenAsync",
                "ApagarTriggerTokenAsync",
                "CriarPipelinePorTriggerAsync",
            ];

            nomes.Should().Contain(esperados);
        }
    }
}
