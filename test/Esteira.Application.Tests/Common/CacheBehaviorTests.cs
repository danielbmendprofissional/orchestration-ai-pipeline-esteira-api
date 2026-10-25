using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Behavios;
using Esteira.Application.Interfaces.Cache;
using Esteira.Domain.Common.Resultados;
using Esteira.Infra.Cache;
using Microsoft.Extensions.Caching.Memory;

namespace Esteira.Application.Tests.Common
{
    public class CacheBehaviorTests
    {
        public sealed class ConsultaCacheada : ICacheableQuery
        {
            public string ChaveCache => "teste:consulta";
            public TimeSpan DuracaoCache => TimeSpan.FromMinutes(1);
        }

        public sealed class ComandoQueInvalida : IInvalidaCache
        {
            public IEnumerable<string> PrefixosCache => new[] { "teste:" };
        }

        public sealed class Consulta { }

        private readonly MemoryCacheService _cache = new(new MemoryCache(new MemoryCacheOptions()));

        [Fact, DisplayName("Deve executar a consulta cacheada uma única vez e reaproveitar o resultado")]
        public async Task ConsultaCacheada_DeveExecutarUmaVez()
        {
            var execucoes = 0;
            var behavior = new CacheBehavior<ConsultaCacheada, int>(_cache);

            var primeira = await behavior.Handle(new ConsultaCacheada(), _ => Task.FromResult(++execucoes), CancellationToken.None);
            var segunda = await behavior.Handle(new ConsultaCacheada(), _ => Task.FromResult(++execucoes), CancellationToken.None);

            primeira.Should().Be(1);
            segunda.Should().Be(1);
            execucoes.Should().Be(1);
        }

        [Fact, DisplayName("Deve invalidar o cache pelo prefixo depois de executar o comando")]
        public async Task ComandoQueInvalida_DeveRemoverPorPrefixo()
        {
            var execucoes = 0;
            var consulta = new CacheBehavior<ConsultaCacheada, int>(_cache);
            await consulta.Handle(new ConsultaCacheada(), _ => Task.FromResult(++execucoes), CancellationToken.None);

            await new CacheBehavior<ComandoQueInvalida, string>(_cache).Handle(new ComandoQueInvalida(), _ => Task.FromResult("ok"), CancellationToken.None);
            var depois = await consulta.Handle(new ConsultaCacheada(), _ => Task.FromResult(++execucoes), CancellationToken.None);

            depois.Should().Be(2);
        }

        [Fact, DisplayName("Não deve invalidar o cache quando o comando falhar")]
        public async Task ComandoComFalha_NaoDeveInvalidar()
        {
            var consulta = new CacheBehavior<ConsultaCacheada, int>(_cache);
            await consulta.Handle(new ConsultaCacheada(), _ => Task.FromResult(1), CancellationToken.None);

            var act = () => new CacheBehavior<ComandoQueInvalida, string>(_cache)
                .Handle(new ComandoQueInvalida(), _ => throw new InvalidOperationException(), CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();

            (await consulta.Handle(new ConsultaCacheada(), _ => Task.FromResult(2), CancellationToken.None)).Should().Be(1);
        }

        [Fact, DisplayName("Não deve usar cache em requisições sem cache configurado")]
        public async Task RequisicaoSemCache_DeveSempreExecutar()
        {
            var execucoes = 0;
            var behavior = new CacheBehavior<Consulta, int>(_cache);

            await behavior.Handle(new Consulta(), _ => Task.FromResult(++execucoes), CancellationToken.None);
            await behavior.Handle(new Consulta(), _ => Task.FromResult(++execucoes), CancellationToken.None);

            execucoes.Should().Be(2);
        }

        [Fact, DisplayName("Deve remover somente as chaves do prefixo informado")]
        public async Task RemoverPorPrefixo_DeveManterOutrasChaves()
        {
            await _cache.ObterOuCriarAsync("etapas:a", () => Task.FromResult(1), TimeSpan.FromMinutes(1));
            await _cache.ObterOuCriarAsync("tarefas:a", () => Task.FromResult(1), TimeSpan.FromMinutes(1));

            _cache.RemoverPorPrefixo("etapas:");

            (await _cache.ObterOuCriarAsync("etapas:a", () => Task.FromResult(2), TimeSpan.FromMinutes(1))).Should().Be(2);
            (await _cache.ObterOuCriarAsync("tarefas:a", () => Task.FromResult(2), TimeSpan.FromMinutes(1))).Should().Be(1);
        }

        [Fact, DisplayName("Não deve guardar no cache a consulta que devolve Result de falha")]
        public async Task ConsultaComResultDeFalha_NaoDeveGuardar()
        {
            var execucoes = 0;
            var behavior = new CacheBehavior<ConsultaCacheada, Result<int>>(_cache);
            var falha = Result<int>.Falha(new Erro("teste.falha", "Falhou.", TipoErro.Failure));

            await behavior.Handle(new ConsultaCacheada(), _ => { execucoes++; return Task.FromResult(falha); }, CancellationToken.None);
            var segunda = await behavior.Handle(new ConsultaCacheada(), _ => { execucoes++; return Task.FromResult(Result<int>.Sucesso(7)); }, CancellationToken.None);

            execucoes.Should().Be(2);
            segunda.Value.Should().Be(7);
        }

        [Fact, DisplayName("Deve guardar no cache a consulta que devolve Result de sucesso")]
        public async Task ConsultaComResultDeSucesso_DeveGuardar()
        {
            var execucoes = 0;
            var behavior = new CacheBehavior<ConsultaCacheada, Result<int>>(_cache);

            await behavior.Handle(new ConsultaCacheada(), _ => { execucoes++; return Task.FromResult(Result<int>.Sucesso(1)); }, CancellationToken.None);
            await behavior.Handle(new ConsultaCacheada(), _ => { execucoes++; return Task.FromResult(Result<int>.Sucesso(1)); }, CancellationToken.None);

            execucoes.Should().Be(1);
        }
    }
}
