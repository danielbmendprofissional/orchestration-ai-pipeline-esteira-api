using System.Security.Claims;
using AwesomeAssertions;
using Esteira.Api.Autenticacao;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Esteira.Integration.Tests.Sessao
{
    // Critério 6 da B3: IUsuarioAtual sai dos claims que o esquema `Sessao` monta a partir do token guardado.
    public class UsuarioAtualTests
    {
        private static UsuarioAtual Com(params Claim[] claims)
        {
            var contexto = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Sessao")) };
            return new UsuarioAtual(new HttpContextAccessor { HttpContext = contexto }, Options.Create(new AutenticacaoSettings()));
        }

        [Fact]
        public void Expoe_id_usuario_nome_email_e_perfis_dos_claims()
        {
            var eu = Com(
                new Claim("sub", "abc-123"),
                new Claim("preferred_username", "dev"),
                new Claim("name", "Dev Teste"),
                new Claim("email", "dev@esteira.local"),
                new Claim(UsuarioAtual.ClaimPerfil, "mor"),
                new Claim(UsuarioAtual.ClaimPerfil, "outro"));

            eu.Id.Should().Be("abc-123");
            eu.Usuario.Should().Be("dev");
            eu.Nome.Should().Be("Dev Teste");
            eu.Email.Should().Be("dev@esteira.local");
            eu.Perfis.Should().BeEquivalentTo("mor", "outro");
            eu.EhMor.Should().BeTrue();
        }

        [Fact]
        public void Sem_o_perfil_mor_nao_e_MOR()
        {
            var eu = Com(new Claim("sub", "x"), new Claim(UsuarioAtual.ClaimPerfil, "membro"));

            eu.EhMor.Should().BeFalse();
        }

        [Fact]
        public void Sem_sessao_nada_e_MOR_e_a_lista_de_perfis_e_vazia()
        {
            var eu = new UsuarioAtual(new HttpContextAccessor(), Options.Create(new AutenticacaoSettings()));

            eu.Id.Should().BeNull();
            eu.Perfis.Should().BeEmpty();
            eu.EhMor.Should().BeFalse();
        }

        [Fact]
        public void Nome_cai_para_o_usuario_quando_o_token_nao_traz_name()
        {
            var eu = Com(new Claim("preferred_username", "dev"));

            eu.Nome.Should().Be("dev");
        }
    }
}
