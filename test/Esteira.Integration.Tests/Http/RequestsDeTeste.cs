using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Integration.Tests.Http
{
    // Requests só de teste: cada um devolve o Result que o teste de HTTP precisa ver na resposta.
    // Vivem no assembly de teste, nunca no assembly da Api (ApiDeTeste registra esse assembly via ApplicationPart).
    public sealed record PedidoComValor : IRequest<Result<string>>;

    public sealed record PedidoSemValor : IRequest<Result>;

    public sealed record PedidoComFalha(TipoErro Tipo) : IRequest<Result<string>>;

    public sealed record PedidoComDuasFalhas : IRequest<Result<string>>;

    public sealed class PedidoComValorHandler : IRequestHandler<PedidoComValor, Result<string>>
    {
        public Task<Result<string>> Handle(PedidoComValor request, CancellationToken cancellationToken)
            => Task.FromResult(Result<string>.Sucesso("valor de teste"));
    }

    public sealed class PedidoSemValorHandler : IRequestHandler<PedidoSemValor, Result>
    {
        public Task<Result> Handle(PedidoSemValor request, CancellationToken cancellationToken)
            => Task.FromResult(Result.Sucesso());
    }

    public sealed class PedidoComFalhaHandler : IRequestHandler<PedidoComFalha, Result<string>>
    {
        public Task<Result<string>> Handle(PedidoComFalha request, CancellationToken cancellationToken)
            => Task.FromResult(Result<string>.Falha(ErrosDeTeste.ErroDo(request.Tipo)));
    }

    public sealed class PedidoComDuasFalhasHandler : IRequestHandler<PedidoComDuasFalhas, Result<string>>
    {
        public Task<Result<string>> Handle(PedidoComDuasFalhas request, CancellationToken cancellationToken)
            => Task.FromResult(Result<string>.Falha(
                new Erro("url.invalida", "A url informada é inválida.", TipoErro.Validation),
                new Erro("projeto.nao.encontrado", "Projeto não encontrado.", TipoErro.NotFound)));
    }

    internal static class ErrosDeTeste
    {
        public static Erro ErroDo(TipoErro tipo) => tipo switch
        {
            TipoErro.Validation => new Erro("url.invalida", "A url informada é inválida.", TipoErro.Validation),
            TipoErro.NotFound => new Erro("projeto.nao.encontrado", "Projeto não encontrado.", TipoErro.NotFound),
            TipoErro.Conflict => new Erro("ambiente.ocupado", "Ambiente já está ocupado.", TipoErro.Conflict),
            TipoErro.Forbidden => new Erro("acesso.negado", "Você não tem permissão para esta ação.", TipoErro.Forbidden),
            TipoErro.Unauthorized => new Erro("sessao.expirada", "Sua sessão expirou.", TipoErro.Unauthorized),
            TipoErro.Failure => new Erro("falha.inesperada", "Não foi possível concluir a operação.", TipoErro.Failure),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null)
        };
    }
}
