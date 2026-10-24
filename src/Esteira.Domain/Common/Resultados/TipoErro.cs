namespace Esteira.Domain.Common.Resultados
{
    // ADR-0003: quem sabe o que aconteceu com a regra é o domínio/handler, que devolve Result em vez de
    // lançar. O tipo do erro é traduzido pra status HTTP num ponto só (Api/Erros).
    public enum TipoErro
    {
        Validation,
        NotFound,
        Conflict,
        Forbidden,
        Unauthorized,
        Failure
    }
}
