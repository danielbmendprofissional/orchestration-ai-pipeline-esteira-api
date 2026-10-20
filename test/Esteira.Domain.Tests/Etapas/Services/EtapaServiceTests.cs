using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.Etapas.Services;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;
using System.ComponentModel;

namespace Esteira.Domain.Tests.Etapas.Services
{
    public class EtapaServiceTests
    {
        private readonly EtapaService _service = new();

        [Fact, DisplayName("Deve lançar NotFoundException quando a etapa for nula")]
        public void ValidarStatusEtapa_EtapaNula_DeveLancarNotFound()
        {
            var act = () => _service.ValidarStatusEtapa(null, StatusEnum.ATIVO);

            act.Should().Throw<NotFoundException>().WithMessage("Etapa não encontrada");
        }

        [Fact, DisplayName("Deve lançar exceção quando a etapa já estiver no status informado")]
        public void ValidarStatusEtapa_MesmoStatus_DeveLancarExcecao()
        {
            var etapa = EtapaEntity.Criar("DEV", "Desenvolvimento", 1, true);

            var act = () => _service.ValidarStatusEtapa(etapa, StatusEnum.ATIVO);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Não deve lançar exceção quando o status da etapa for diferente")]
        public void ValidarStatusEtapa_StatusDiferente_NaoDeveLancarExcecao()
        {
            var etapa = EtapaEntity.Criar("DEV", "Desenvolvimento", 1, true);

            var act = () => _service.ValidarStatusEtapa(etapa, StatusEnum.CANCELADO);

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve aceitar etapas existentes e ativas ignorando maiúsculas e minúsculas")]
        public void ValidarEtapasExistentesEAtivas_EtapasValidas_NaoDeveLancarExcecao()
        {
            var act = () => _service.ValidarEtapasExistentesEAtivas(FluxoTestesSuporte.Catalogo, new List<string> { "dev", "QA" });

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve lançar exceção com a sigla quando a etapa não existir")]
        public void ValidarEtapasExistentesEAtivas_EtapaInexistente_DeveLancarExcecao()
        {
            var act = () => _service.ValidarEtapasExistentesEAtivas(FluxoTestesSuporte.Catalogo, new List<string> { "DEV", "XYZ" });

            act.Should().Throw<DomainException>().WithMessage("Etapas inválidas ou inativas: XYZ");
        }

        [Fact, DisplayName("Deve lançar exceção com a descrição quando a etapa estiver inativa")]
        public void ValidarEtapasExistentesEAtivas_EtapaInativa_DeveLancarExcecao()
        {
            var catalogo = new List<EtapaEntity> { EtapaEntity.Criar("DEV", "Desenvolvimento", 1, true) };
            catalogo[0].AtualizarStatusEtapa(StatusEnum.CANCELADO);

            var act = () => _service.ValidarEtapasExistentesEAtivas(catalogo, new List<string> { "DEV" });

            act.Should().Throw<DomainException>().WithMessage("Etapas inválidas ou inativas: Desenvolvimento");
        }

        [Fact, DisplayName("Deve lançar exceção de domínio quando as listas forem nulas")]
        public void ValidarEtapas_ListasNulas_DeveLancarExcecao()
        {
            var act1 = () => _service.ValidarEtapasExistentesEAtivas(null, new List<string>());
            var act2 = () => _service.ValidarEtapasExistentesEAtivas(FluxoTestesSuporte.Catalogo, null);
            var act3 = () => _service.ValidarEtapasObrigatorias(null, new List<string>());
            var act4 = () => _service.ValidarEtapasObrigatorias(FluxoTestesSuporte.Catalogo, null);

            act1.Should().Throw<DomainException>();
            act2.Should().Throw<DomainException>();
            act3.Should().Throw<DomainException>();
            act4.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve aceitar quando todas as etapas obrigatórias forem informadas")]
        public void ValidarEtapasObrigatorias_TodasInformadas_NaoDeveLancarExcecao()
        {
            var act = () => _service.ValidarEtapasObrigatorias(FluxoTestesSuporte.Catalogo, new List<string> { "ABER", "DEV", "HML", "PRD" });

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve aceitar etapas obrigatórias informadas em minúsculas")]
        public void ValidarEtapasObrigatorias_Minusculas_NaoDeveLancarExcecao()
        {
            var act = () => _service.ValidarEtapasObrigatorias(FluxoTestesSuporte.Catalogo, new List<string> { "aber", "dev", "Hml", "prd" });

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve lançar exceção listando as descrições das etapas obrigatórias faltantes")]
        public void ValidarEtapasObrigatorias_Faltantes_DeveLancarExcecao()
        {
            var act = () => _service.ValidarEtapasObrigatorias(FluxoTestesSuporte.Catalogo, new List<string> { "ABER", "HML" });

            act.Should().Throw<DomainException>().WithMessage("As etapas obrigatórias não foram informadas: Desenvolvimento, Produção");
        }
    }
}
