using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Auditoria;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Aplicacoes;
using NSubstitute;

namespace Esteira.Application.Tests.Auditoria
{
    // X1.3, critérios 2, 3 e 4: a consulta única de escopo da trilha.
    [Trait("Fatia", "X1")]
    public class EscopoDeAuditoriaTests
    {
        private static RegistroAuditoriaEntity Registro(long? aplicacaoId)
            => RegistroAuditoriaEntity.Criar(DateTime.UtcNow, "x", "Projeto", Guid.NewGuid(), AcaoAuditoria.Criou, null, "{}", aplicacaoId: aplicacaoId);

        private static EscopoDeAuditoria Escopo(bool mor, string sub = "sub-1")
        {
            var usuario = Substitute.For<IUsuarioAtual>();
            usuario.Id.Returns(sub);
            usuario.EhMor.Returns(mor);
            return new EscopoDeAuditoria(usuario);
        }

        private static readonly IQueryable<PapelAplicacaoEntity> Papeis = new[]
        {
            PapelAplicacaoEntity.Criar(1, "sub-1", PapelAplicacao.Membro),
            PapelAplicacaoEntity.Criar(2, "outra", PapelAplicacao.Membro)
        }.AsQueryable();

        [Fact, DisplayName("3: MOR vê todos os registros, inclusive os sem Aplicação")]
        public void Mor_VeTudo()
        {
            var registros = new[] { Registro(1), Registro(2), Registro(null) }.AsQueryable();

            Escopo(mor: true).Restringir(registros, Papeis).Should().HaveCount(3);
        }

        [Fact, DisplayName("2 e 4: pessoa com papel vê só as Aplicações dela, nunca registro sem Aplicação")]
        public void ComPapel_VeSoAsSuas()
        {
            var meu = Registro(1);
            var registros = new[] { meu, Registro(2), Registro(3), Registro(null) }.AsQueryable();

            Escopo(mor: false).Restringir(registros, Papeis).Should().ContainSingle().Which.Should().BeSameAs(meu);
        }

        [Fact, DisplayName("4: pessoa sem papel nenhum recebe lista vazia")]
        public void SemPapel_Vazio()
        {
            var registros = new[] { Registro(1), Registro(2), Registro(null) }.AsQueryable();

            Escopo(mor: false, sub: "sem-papel").Restringir(registros, Papeis).Should().BeEmpty();
        }
    }
}
