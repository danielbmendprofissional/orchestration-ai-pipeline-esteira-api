namespace Esteira.Application.Interfaces.Cache
{
    public interface IInvalidaCache
    {
        IEnumerable<string> PrefixosCache { get; }
    }
}
