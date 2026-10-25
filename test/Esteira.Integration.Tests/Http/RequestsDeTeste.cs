using Esteira.Domain.Common.Resultados;
using FluentValidation;
using Esteira.Application.Interfaces.Transaction;
using MassTransit;
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

    // Critérios 4 e 5: requests com validator de verdade, para exercitar o pipeline de validação.
    public sealed record PedidoValidado(string Url) : IRequest<Result<string>>;

    public sealed record PedidoSemErrorCode(string Nome) : IRequest<Result<string>>;

    // Critério 6: o corpo passa pelo model binding; o handler só roda se o JSON for legível e tipado.
    public sealed record PedidoComCorpo(int Quantidade) : IRequest<Result<string>>;

    public sealed class PedidoValidadoValidator : AbstractValidator<PedidoValidado>
    {
        public PedidoValidadoValidator()
        {
            RuleFor(p => p.Url)
                .NotEmpty().WithMessage("A url é obrigatória.").WithErrorCode("url.obrigatoria");

            RuleFor(p => p.Url)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("A url informada é inválida.").WithErrorCode("url.invalida");
        }
    }

    public sealed class PedidoSemErrorCodeValidator : AbstractValidator<PedidoSemErrorCode>
    {
        public PedidoSemErrorCodeValidator()
        {
            RuleFor(p => p.Nome).NotEmpty().WithMessage("O nome é obrigatório.");
        }
    }

    public sealed class PedidoValidadoHandler : IRequestHandler<PedidoValidado, Result<string>>
    {
        // Zerado pelo teste: prova que a validação barrou o request antes do handler.
        public static int Execucoes { get; set; }

        public Task<Result<string>> Handle(PedidoValidado request, CancellationToken cancellationToken)
        {
            Execucoes++;
            return Task.FromResult(Result<string>.Sucesso("validado"));
        }
    }

    public sealed class PedidoSemErrorCodeHandler : IRequestHandler<PedidoSemErrorCode, Result<string>>
    {
        public static int Execucoes { get; set; }

        public Task<Result<string>> Handle(PedidoSemErrorCode request, CancellationToken cancellationToken)
        {
            Execucoes++;
            return Task.FromResult(Result<string>.Sucesso("validado"));
        }
    }

    public sealed class PedidoComCorpoHandler : IRequestHandler<PedidoComCorpo, Result<string>>
    {
        public static int Execucoes { get; set; }

        public Task<Result<string>> Handle(PedidoComCorpo request, CancellationToken cancellationToken)
        {
            Execucoes++;
            return Task.FromResult(Result<string>.Sucesso($"quantidade:{request.Quantidade}"));
        }
    }

    // Critério 9: exception não tratada. A mensagem é marcada para o teste provar que ela não vaza.
    public sealed record PedidoQueLanca : IRequest<Result<string>>;

    public sealed class PedidoQueLancaHandler : IRequestHandler<PedidoQueLanca, Result<string>>
    {
        public const string Mensagem = "detalhe-tecnico-que-nao-pode-vazar";

        public Task<Result<string>> Handle(PedidoQueLanca request, CancellationToken cancellationToken)
            => throw new InvalidOperationException(Mensagem);
    }

    // Critério 7: command transacional que publica mensagem (outbox) e devolve falha.
    public sealed record PedidoQuePublicaEFalha(Guid Marcador) : IRequest<Result>, ITransactionRequest;

    public sealed class PedidoQuePublicaEFalhaHandler(IPublishEndpoint publicador) : IRequestHandler<PedidoQuePublicaEFalha, Result>
    {
        public async Task<Result> Handle(PedidoQuePublicaEFalha request, CancellationToken cancellationToken)
        {
            await publicador.Publish(new Mensageria.MensagemDeTeste(request.Marcador), cancellationToken);
            return Result.Falha(new Erro("teste.falha", "Falhou.", TipoErro.Failure));
        }
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
