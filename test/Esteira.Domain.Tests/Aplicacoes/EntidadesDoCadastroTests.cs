using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;

namespace Esteira.Domain.Tests.Aplicacoes
{
    // Dados da A1 (spec 01-cadastro-aplicacao.md, "Dados"): invariantes das entidades do cadastro.
    [Trait("Fatia", "A1")]
    public class EntidadesDoCadastroTests
    {
        [Fact, DisplayName("Deve normalizar a sigla da Aplicação em maiúscula e sem espaços")]
        public void Aplicacao_Criar_NormalizaSigla()
        {
            var r = AplicacaoEntity.Criar("Valuto", "  vlt ", 10, "valuto");

            r.IsSuccess.Should().BeTrue();
            r.Value.Sigla.Should().Be("VLT");
            r.Value.Ativa.Should().BeTrue();
            r.Value.Should().BeAssignableTo<EntityBase>();
            r.Value.PublicId.Version.Should().Be(7);
        }

        [Theory, DisplayName("Deve recusar Aplicação sem nome ou sem sigla")]
        [InlineData("", "VLT", "aplicacao.nome-obrigatorio")]
        [InlineData("Valuto", "  ", "aplicacao.sigla-obrigatoria")]
        public void Aplicacao_Criar_Obrigatorios(string nome, string sigla, string codigo)
        {
            var r = AplicacaoEntity.Criar(nome, sigla, 10, "valuto");

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be(codigo);
        }

        [Fact, DisplayName("Deve normalizar a sigla ao alterar a Aplicação")]
        public void Aplicacao_Alterar_NormalizaSigla()
        {
            var a = AplicacaoEntity.Criar("Valuto", "VLT", 10, "valuto").Value;

            var r = a.Alterar("Valuto 2", "vl2", false);

            r.IsSuccess.Should().BeTrue();
            (a.Nome, a.Sigla, a.Ativa).Should().Be(("Valuto 2", "VL2", false));
        }

        [Fact, DisplayName("Deve criar Projeto ativo com a stack informada")]
        public void Projeto_Criar()
        {
            var p = ProjetoEntity.Criar(1, 99, "valuto/api", "api", Stack.Dotnet);

            p.AplicacaoId.Should().Be(1);
            p.ProjetoGitLabId.Should().Be(99);
            p.Stack.Should().Be(Stack.Dotnet);
            p.Ativo.Should().BeTrue();
        }

        [Fact, DisplayName("Deve lançar ArgumentException para stack fora do enum")]
        public void Projeto_StackInvalida_Guard()
        {
            var act = () => ProjetoEntity.Criar(1, 99, "valuto/api", "api", (Stack)99);

            act.Should().Throw<ArgumentException>();
        }

        [Fact, DisplayName("Deve recusar dependência de um Projeto nele mesmo")]
        public void Dependencia_Propria_Recusada()
        {
            var r = DependenciaProjetoEntity.Criar(5, 5);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.dependencia-invalida");
        }

        [Fact, DisplayName("Deve criar dependência entre Projetos diferentes")]
        public void Dependencia_Valida()
        {
            var r = DependenciaProjetoEntity.Criar(5, 6);

            r.IsSuccess.Should().BeTrue();
            (r.Value.ProjetoId, r.Value.DependeDeProjetoId).Should().Be((5L, 6L));
        }

        [Fact, DisplayName("Deve criar Ambiente habilitado por padrão")]
        public void Ambiente_Criar_HabilitadoPorPadrao()
        {
            AmbienteProjetoEntity.Criar(1, AmbienteDeploy.Hml).Value.Habilitado.Should().BeTrue();
        }

        [Fact, DisplayName("Deve recusar desabilitar producao")]
        public void Ambiente_Producao_NaoDesabilita()
        {
            var amb = AmbienteProjetoEntity.Criar(1, AmbienteDeploy.Producao).Value;

            var r = amb.Alterar(false);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("ambiente.producao-obrigatorio");
            amb.Habilitado.Should().BeTrue();
        }

        [Fact, DisplayName("Deve recusar criar producao desabilitado")]
        public void Ambiente_Producao_NaoNasceDesabilitado()
        {
            AmbienteProjetoEntity.Criar(1, AmbienteDeploy.Producao, false).IsSuccess.Should().BeFalse();
        }

        [Fact, DisplayName("Deve permitir desabilitar qa")]
        public void Ambiente_Qa_Desabilita()
        {
            var amb = AmbienteProjetoEntity.Criar(1, AmbienteDeploy.Qa).Value;

            amb.Alterar(false).IsSuccess.Should().BeTrue();
            amb.Habilitado.Should().BeFalse();
        }

        [Fact, DisplayName("Deve recusar Destino sem nome")]
        public void Destino_SemNome()
        {
            var r = DestinoEntity.Criar(1, AmbienteDeploy.Hml, " ", new Dictionary<string, string>(), 0);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("destino.nome-obrigatorio");
        }

        [Fact, DisplayName("Deve guardar a configuração chave-valor do Destino")]
        public void Destino_Configuracao()
        {
            var d = DestinoEntity.Criar(1, AmbienteDeploy.Hml, "k8s-hml", new Dictionary<string, string> { ["KUBE_CONTEXT"] = "hml" }, 2).Value;

            d.Configuracao.Should().ContainKey("KUBE_CONTEXT").WhoseValue.Should().Be("hml");
            d.Ordem.Should().Be(2);
        }

        [Fact, DisplayName("Deve criar política com limites null (sem limite) e modo")]
        public void Politica_ComNulos()
        {
            var r = PoliticaSeveridadeEntity.Criar(1, AmbientePolitica.Qa, 0, 0, null, null, ModoPolitica.Bloqueia);

            r.IsSuccess.Should().BeTrue();
            (r.Value.Critica, r.Value.Alta, r.Value.Media, r.Value.Baixa).Should().Be((0, 0, (int?)null, (int?)null));
            r.Value.Modo.Should().Be(ModoPolitica.Bloqueia);
        }

        [Fact, DisplayName("Deve recusar política com limite negativo (politica.limite-negativo)")]
        public void Politica_Negativo()
        {
            var r = PoliticaSeveridadeEntity.Criar(1, AmbientePolitica.Qa, 0, -1, null, null, ModoPolitica.Avisa);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("politica.limite-negativo");
        }

        [Fact, DisplayName("Deve recusar usuário vazio no Papel")]
        public void Papel_UsuarioVazio_Guard()
        {
            var act = () => PapelAplicacaoEntity.Criar(1, " ", PapelAplicacao.Analista);

            act.Should().Throw<ArgumentException>();
        }

        [Fact, DisplayName("Deve criar Papel com sub e papel")]
        public void Papel_Criar()
        {
            var p = PapelAplicacaoEntity.Criar(1, "sub-1", PapelAplicacao.AprovadorTecnico);

            (p.AplicacaoId, p.Usuario, p.Papel).Should().Be((1L, "sub-1", PapelAplicacao.AprovadorTecnico));
        }

        [Fact, DisplayName("Deve atualizar nome, e-mail e último login da Pessoa")]
        public void Pessoa_Atualizar()
        {
            var t1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var t2 = t1.AddDays(1);
            var p = PessoaEntity.Criar("sub-1", "Ana", "ana@x.com", t1);

            p.RegistrarLogin("Ana B", "ana.b@x.com", t2);

            (p.Sub, p.Nome, p.Email, p.UltimoLoginEm).Should().Be(("sub-1", "Ana B", "ana.b@x.com", t2));
        }

        [Fact, DisplayName("Deve criar RegistroAuditoria com quem, quando, ação e antes/depois")]
        public void Auditoria_Criar()
        {
            var quando = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var id = Guid.CreateVersion7();

            var r = RegistroAuditoriaEntity.Criar(quando, "sub-1", "Aplicacao", id, AcaoAuditoria.Alterou, "{\"a\":1}", "{\"a\":2}");

            (r.Quando, r.Usuario, r.Entidade, r.EntidadePublicId, r.Acao, r.Antes, r.Depois)
                .Should().Be((quando, "sub-1", "Aplicacao", id, AcaoAuditoria.Alterou, "{\"a\":1}", "{\"a\":2}"));
        }

        [Fact, DisplayName("Deve recusar RegistroAuditoria sem usuário")]
        public void Auditoria_SemUsuario_Guard()
        {
            var act = () => RegistroAuditoriaEntity.Criar(DateTime.UtcNow, "", "Aplicacao", Guid.NewGuid(), AcaoAuditoria.Criou, null, null);

            act.Should().Throw<ArgumentException>();
        }
    }
}
