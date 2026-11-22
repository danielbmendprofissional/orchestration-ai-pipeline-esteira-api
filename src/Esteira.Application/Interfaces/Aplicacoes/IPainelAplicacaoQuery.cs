using Esteira.Application.Fatias;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    // Leitura das Aplicações para o Painel (EsteiraReadContext): quem o Painel pode mostrar e onde ler cada uma.
    public interface IPainelAplicacaoQuery
    {
        // MOR vê todas; os demais, só as Aplicações em que têm algum papel. Só Aplicações ativas.
        Task<IReadOnlyList<AplicacaoNoPainel>> ListarVisiveisAsync(string sub, bool ehMor, CancellationToken cancellationToken = default);
    }
}
