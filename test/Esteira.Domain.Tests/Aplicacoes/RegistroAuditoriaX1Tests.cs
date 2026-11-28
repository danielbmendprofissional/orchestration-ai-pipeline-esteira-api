using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Domain.Tests.Aplicacoes
{
    // X1.1 (critério 1/11): RegistroAuditoria ganha Aplicação, origem, categoria e campos de leitura; continua só inserção.
    [Trait("Fatia", "X1")]
    public class RegistroAuditoriaX1Tests
    {
        private static readonly DateTime Quando = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact, DisplayName("Deve criar o registro com Aplicação, origem, nome, categoria, verbo, objeto, motivo e autoaprovação")]
        public void Criar_ComCamposNovos()
        {
            var r = RegistroAuditoriaEntity.Criar(Quando, "sub-1", "AcaoBot", Guid.NewGuid(), AcaoAuditoria.Criou, null, "{}",
                CategoriaAuditoria.Aprovacao, OrigemAuditoria.Bot, aplicacaoId: 7, usuarioNome: "bot da Esteira",
                verbo: "aprovou a Fatia V4 (MR)", objetoCodigo: "V4", objetoNome: "Fatia V4", motivo: "ok", autoaprovacao: true);

            (r.Categoria, r.Origem, r.AplicacaoId, r.UsuarioNome, r.Verbo, r.ObjetoCodigo, r.ObjetoNome, r.Motivo, r.Autoaprovacao)
                .Should().Be((CategoriaAuditoria.Aprovacao, OrigemAuditoria.Bot, 7L, "bot da Esteira", "aprovou a Fatia V4 (MR)", "V4", "Fatia V4", "ok", true));
        }

        [Fact, DisplayName("Deve usar padrões (tela, cadastro, sem Aplicação, sem autoaprovação) quando os campos novos não são informados")]
        public void Criar_Padroes()
        {
            var r = RegistroAuditoriaEntity.Criar(Quando, "sub-1", "Aplicacao", Guid.NewGuid(), AcaoAuditoria.Criou, null, null);

            (r.Categoria, r.Origem, r.AplicacaoId, r.Autoaprovacao).Should().Be((CategoriaAuditoria.Cadastro, OrigemAuditoria.Tela, (long?)null, false));
        }

        [Fact, DisplayName("Deve recusar categoria e origem inválidas")]
        public void Criar_EnumInvalido()
        {
            var cat = () => RegistroAuditoriaEntity.Criar(Quando, "s", "A", Guid.NewGuid(), AcaoAuditoria.Criou, null, null, categoria: (CategoriaAuditoria)99);
            var ori = () => RegistroAuditoriaEntity.Criar(Quando, "s", "A", Guid.NewGuid(), AcaoAuditoria.Criou, null, null, origem: (OrigemAuditoria)99);

            cat.Should().Throw<ArgumentException>();
            ori.Should().Throw<ArgumentException>();
        }
    }
}
