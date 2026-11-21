using Esteira.Application.Aplicacoes;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    // Escrita do Projeto e do que pende dele: Ambientes, Destinos e dependências (EsteiraContext).
    public interface IProjetoRepositorio
    {
        Task<ProjetoEntity?> ObterPorPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);

        // Projeto do GitLab já cadastrado em qualquer Aplicação.
        Task<bool> ProjetoGitLabCadastradoAsync(long projetoGitLabId, CancellationToken cancellationToken = default);

        // Dos ids pedidos, os que já estão cadastrados (qualquer Aplicação).
        Task<IReadOnlySet<long>> ProjetosGitLabCadastradosAsync(IReadOnlyCollection<long> projetoGitLabIds, CancellationToken cancellationToken = default);

        Task AdicionarAsync(ProjetoEntity projeto, CancellationToken cancellationToken = default);

        Task AdicionarAmbienteAsync(AmbienteProjetoEntity ambiente, CancellationToken cancellationToken = default);

        Task<AmbienteProjetoEntity?> ObterAmbienteAsync(long projetoId, AmbienteDeploy ambiente, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ProjetoEntity>> ProjetosDaAplicacaoAsync(long aplicacaoId, CancellationToken cancellationToken = default);

        // Todas as arestas da Aplicação (rastreadas: o handler remove as que saem).
        Task<IReadOnlyList<DependenciaProjetoEntity>> DependenciasDaAplicacaoAsync(long aplicacaoId, CancellationToken cancellationToken = default);

        Task AdicionarDependenciaAsync(DependenciaProjetoEntity dependencia, CancellationToken cancellationToken = default);

        void RemoverDependencia(DependenciaProjetoEntity dependencia);

        Task<DestinoEntity?> ObterDestinoAsync(long projetoId, AmbienteDeploy ambiente, Guid destinoPublicId, CancellationToken cancellationToken = default);

        // `exceto`: Id do próprio Destino ao alterar.
        Task<bool> DestinoNomeExisteAsync(long projetoId, AmbienteDeploy ambiente, string nome, long? exceto, CancellationToken cancellationToken = default);

        Task AdicionarDestinoAsync(DestinoEntity destino, CancellationToken cancellationToken = default);

        void RemoverDestino(DestinoEntity destino);

        // O Projeto como a API o devolve (Ambientes, Destinos, dependências), lido do contexto de escrita
        // porque a resposta sai antes do commit da transação do comando.
        Task<ProjetoResumoDto> MontarDtoAsync(ProjetoEntity projeto, CancellationToken cancellationToken = default);
    }
}
