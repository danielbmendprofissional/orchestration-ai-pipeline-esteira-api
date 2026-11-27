namespace Esteira.Application.Interfaces.UnitOfWork
{
    // Conflito de concorrência otimista (token xmin) vindo do banco, traduzido pela Data: outra requisição gravou o
    // mesmo agregado entre a leitura e a gravação. O handler a converte em Result.
    public sealed class ConcorrenciaOtimistaException(Exception? inner = null)
        : Exception("Conflito de concorrência otimista.", inner)
    {
    }
}
