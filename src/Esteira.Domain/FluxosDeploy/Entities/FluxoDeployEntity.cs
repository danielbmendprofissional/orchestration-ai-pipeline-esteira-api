using Esteira.Domain.AprovadoresTecnicos.Entities;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.FluxosDeploy.Events;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Domain.FluxosDeploy.Entities
{
    public sealed class FluxoDeployEntity : AggregateRoot
    {
        public StatusEnum Status { get; private set; }
        public DateTime Inicio { get; private set; }
        public DateTime? Fim { get; private set; }
        public string Sistema { get; private set; }
        public string Usuario { get; private set; }
        public string Versao { get; private set; }

        public FluxoDeployVersaoTarefasEntity? VersaoTarefas { get; private set; }
        public FluxoDeployGmudEntity? Gmud { get; private set; }

        private readonly List<FluxoDeployBdCrsEntity> _bds = new();
        public IReadOnlyCollection<FluxoDeployBdCrsEntity> Bds => _bds;

        private readonly List<FluxoDeployAprovadorTecnicoEntity> _aprovadoresTecnicos = new();
        public IReadOnlyCollection<FluxoDeployAprovadorTecnicoEntity> AprovadoresTecnicos => _aprovadoresTecnicos;

        private readonly List<FluxoDeployAprovadorNegocioEntity> _aprovadoresNegocio = new();

        public IReadOnlyCollection<FluxoDeployAprovadorNegocioEntity> AprovadoresNegocio => _aprovadoresNegocio;

        private readonly List<FluxoDeployEtapaEntity> _etapas = new();
        public IReadOnlyCollection<FluxoDeployEtapaEntity> Etapas => _etapas;

        private FluxoDeployEntity() { }

        internal FluxoDeployEntity(string sistema, string usuario, VersaoFluxoValueObject versao, StatusEnum status, DateTime inicio)
        {
            Guard.AgainstNullOrWhiteSpace(sistema, nameof(sistema));
            Guard.AgainstNullOrWhiteSpace(usuario, nameof(usuario));
            Guard.AgainstNull(versao, nameof(versao));

            Sistema = sistema;
            Usuario = usuario;
            Versao = versao.ToString();
            Status = status;
            Inicio = inicio;
        }

        public static FluxoDeployEntity Criar(string sistema, string usuario, string versaoAnterior, StatusEnum status = StatusEnum.EXECUTANDO, DateTime? agora = null)
        {
            var versaoAtual = string.IsNullOrEmpty(versaoAnterior) ? null : VersaoFluxoValueObject.Criar(versaoAnterior);

            return new FluxoDeployEntity(
                sistema,
                usuario,
                VersaoFluxoValueObject.Proxima(versaoAtual),
                status,
                agora ?? DateTime.Now);
        }

        public void Atualizar(string versao, string sistema, string usuario)
        {
            GarantirEmExecucao();

            Guard.AgainstNullOrWhiteSpace(sistema, nameof(sistema));
            Guard.AgainstNullOrWhiteSpace(usuario, nameof(usuario));

            Sistema = sistema;
            Versao = VersaoFluxoValueObject.Criar(versao).ToString();
            Usuario = usuario;
        }

        public void Concluir(DateTime? agora = null)
        {
            GarantirEmExecucao();

            Status = StatusEnum.CONCLUIDO;
            Fim = agora ?? DateTime.Now;

            AdicionarEvento(new FluxoDeployConcluidoEvent(Id, Fim.Value));
        }

        public void Cancelar(DateTime? agora = null)
        {
            Guard.Against<DomainException>(
                Status == StatusEnum.CANCELADO,
                $"Fluxo deploy '{Id}' já está cancelado");

            Guard.Against<DomainException>(
                Status == StatusEnum.CONCLUIDO,
                $"Fluxo deploy '{Id}' já está concluído e não pode ser cancelado");

            Status = StatusEnum.CANCELADO;
            Fim = agora ?? DateTime.Now;

            foreach (var etapa in _etapas)
                etapa.Cancelar(limparOrdem: false);

            AdicionarEvento(new FluxoDeployCanceladoEvent(Id, Fim.Value));
        }

        #region VersaoTarefas
        public void CriarVersaoTarefas(
            int idProjeto,
            int idVersao)
        {
            GarantirEmExecucao();

            if (VersaoTarefas is null)
            {
                VersaoTarefas =
                    new FluxoDeployVersaoTarefasEntity(
                        idProjeto,
                        idVersao);
            }
            else
            {
                if (VersaoTarefas.IdProjeto != idProjeto || VersaoTarefas.IdVersao != idVersao)
                    _aprovadoresNegocio.Clear();

                VersaoTarefas.Atualizar(
                    idProjeto,
                    idVersao);
            }
        }
        #endregion

        #region Gmud
        public void CriarGmud(
            string urlGmud,
            string justificativa,
            string usuario,
            DateTime? agora = null)
        {
            GarantirEmExecucao();

            if (Gmud is null)
            {
                Gmud = new FluxoDeployGmudEntity(
                    urlGmud,
                    justificativa,
                    usuario,
                    agora);
            }
            else
            {
                Gmud.Atualizar(
                    urlGmud,
                    justificativa,
                    usuario);
            }
        }
        #endregion

        #region AprovadorTecnico
        public void AdicionarAprovadorTecnico(
            AprovadorTecnicoEntity aprovador,
            IEnumerable<AprovadorTecnicoEntity> integrantesEquipe,
            bool aprovado,
            string justificativa,
            DateTime? data = null)
        {
            GarantirEmExecucao();

            Guard.AgainstNull(aprovador, nameof(aprovador));
            Guard.AgainstNull(integrantesEquipe, nameof(integrantesEquipe));

            Guard.Against<DomainException>(
                aprovador.Status != StatusEnum.ATIVO,
                "Aprovador técnico inativo.");

            Guard.Against<DomainException>(
                !EtapaEstaExecutando(EtapaEnum.AET.GetDescription()),
                "A aprovação técnica só pode ser registrada com a etapa de aprovação técnica em execução.");

            Guard.Against<DomainException>(
                string.Equals(aprovador.Usuario, Usuario, StringComparison.OrdinalIgnoreCase),
                "O criador do fluxo não pode registrar aprovação técnica no próprio fluxo.");

            Guard.Against<DomainException>(
                !aprovado && string.IsNullOrWhiteSpace(justificativa),
                "Justificativa é obrigatória quando a aprovação for negada.");

            var decisaoAnterior = _aprovadoresTecnicos
                .FirstOrDefault(x => x.IdAprovadorTecnico == aprovador.Id);

            if (decisaoAnterior is not null)
            {
                decisaoAnterior.AlterarVeredito(aprovado, justificativa, data);
                return;
            }

            var idsEquipe = integrantesEquipe
                .Where(x => x.Equipe == aprovador.Equipe)
                .Select(x => x.Id)
                .ToHashSet();

            Guard.Against<DomainException>(
                _aprovadoresTecnicos.Any(x =>
                    idsEquipe.Contains(x.IdAprovadorTecnico)),
                "A equipe já realizou uma decisão para este fluxo.");

            _aprovadoresTecnicos.Add(
                new FluxoDeployAprovadorTecnicoEntity(
                    aprovador.Id,
                    aprovado,
                    justificativa,
                    data));
        }
        #endregion

        #region Bd
        public void AdicionarBd(
            string crs,
            string url)
        {
            Guard.Against<DomainException>(
                _bds.Any(x => x.Crs == crs),
                "CRS já adicionada ao fluxo.");

            _bds.Add(
                new FluxoDeployBdCrsEntity(
                    crs,
                    url));
        }

        public void RemoverBd(
            int idCrs)
        {
            var bd = _bds.FirstOrDefault(x => x.Id == idCrs);
            Guard.Against<DomainException>(
                bd is null,
                "CRS não encontrada.");

            _bds.Remove(bd);
        }
        #endregion

        #region AprovadorNegocio
        public void AdicionarAprovadorNegocio(
            int idTarefa,
            string url)
        {
            Guard.Against<DomainException>(
                _aprovadoresNegocio.Any(x => x.IdTarefa == idTarefa),
                "Tarefa já associada ao fluxo.");

            _aprovadoresNegocio.Add(
                new FluxoDeployAprovadorNegocioEntity(
                    idTarefa,
                    url));
        }

        public void RemoverAprovadorNegocio(
            int idTarefa)
        {
            var aprovacao = _aprovadoresNegocio
                .FirstOrDefault(x => x.IdTarefa == idTarefa);

            Guard.Against<DomainException>(
                aprovacao is null,
                "Tarefa não encontrada.");

            _aprovadoresNegocio.Remove(aprovacao);
        }
        #endregion

        #region Etapas
        public void AdicionarEtapa(
            string sglEtapa,
            int ordem)
        {
            GarantirEmExecucao();

            Guard.AgainstNullOrWhiteSpace(sglEtapa, nameof(sglEtapa));

            Guard.Against<DomainException>(
                ordem <= 0,
                "A ordem de uma etapa deve ser maior que 0.");

            Guard.Against<DomainException>(
                _etapas.Any(x => x.SglEtapa.Equals(sglEtapa, StringComparison.OrdinalIgnoreCase)),
                $"Etapa {sglEtapa} já adicionada ao fluxo.");

            Guard.Against<DomainException>(
                _etapas.Any(x => x.Ordem == ordem),
                $"Já existe uma etapa na ordem {ordem}.");

            Guard.Against<DomainException>(
                _etapas.Any(x => x.Status == StatusEnum.CONCLUIDO),
                "Não é possível adicionar etapas após o início do fluxo. Utilize a reconfiguração de etapas.");

            _etapas.Add(
                new FluxoDeployEtapaEntity(
                    sglEtapa,
                    ordem));

            foreach (var etapa in _etapas.Where(x => x.Status == StatusEnum.EXECUTANDO))
                etapa.Reiniciar();

            DefinirEtapaExecutando();
        }

        public void AtualizarFluxoDeployEtapas(
            IEnumerable<EtapaEntity> etapasSistema,
            IEnumerable<string> etapasSelecionadas)
        {
            GarantirEmExecucao();

            Guard.AgainstNull(etapasSistema, nameof(etapasSistema));
            Guard.AgainstNull(etapasSelecionadas, nameof(etapasSelecionadas));

            var etapasAtivas = etapasSistema
                .Where(x => x.Status == StatusEnum.ATIVO)
                .ToList();

            ValidarRemocaoEtapasConcluidas(etapasSelecionadas);

            ValidarEtapasObrigatorias(
                etapasAtivas,
                etapasSelecionadas);

            CancelarEtapasRemovidas(etapasSelecionadas);

            ReorganizarEtapas(
                etapasAtivas,
                etapasSelecionadas);

            DefinirEtapaExecutando();
        }

        public FluxoDeployEtapaEntity EtapaAtual()
            => _etapas.OrderBy(a => a.Ordem).FirstOrDefault(a => a.Status == StatusEnum.EXECUTANDO);

        public FluxoDeployEtapaEntity ProximaEtapaNaoIniciada()
            => _etapas.OrderBy(a => a.Ordem).FirstOrDefault(a => a.Status == StatusEnum.NAO_INICIADO);

        public bool EtapaEstaExecutando(string sigla)
            => _etapas.Any(e => e.SglEtapa.Equals(sigla, StringComparison.OrdinalIgnoreCase) && e.Status == StatusEnum.EXECUTANDO);

        public bool EtapaEstaConcluida(string sigla)
            => _etapas.Any(e => e.SglEtapa.Equals(sigla, StringComparison.OrdinalIgnoreCase) && e.Status == StatusEnum.CONCLUIDO);

        public bool AlgumaEtapaExecutando(IEnumerable<string> siglas)
        {
            Guard.AgainstNull(siglas, nameof(siglas));

            return _etapas.Any(e => siglas.Contains(e.SglEtapa, StringComparer.OrdinalIgnoreCase) && e.Status == StatusEnum.EXECUTANDO);
        }

        public StatusEnum StatusEtapa(string sigla)
            => _etapas.Where(e => e.SglEtapa.Equals(sigla, StringComparison.OrdinalIgnoreCase)).Select(e => e.Status).FirstOrDefault();

        public void AvancarEtapa()
        {
            GarantirEmExecucao();

            var etapaAtual = EtapaAtual();
            var etapaSeguinte = ProximaEtapaNaoIniciada();

            Guard.Against<DomainException>(
                etapaAtual is null,
                $"Não há etapa em EXECUÇÃO para o fluxo de deploy {Id}");

            etapaAtual.Concluir();
            etapaSeguinte?.Iniciar();
        }

        public void AtualizarEtapaEspecifica(string sglEtapa)
        {
            GarantirEmExecucao();

            var etapaSelecionada = _etapas
                .FirstOrDefault(x => x.SglEtapa.Equals(sglEtapa, StringComparison.OrdinalIgnoreCase));

            Guard.Against<DomainException>(etapaSelecionada is null,
                "Etapa não está contida no fluxo de deploy");

            var etapaExecutando = _etapas.FirstOrDefault(a => a.Status == StatusEnum.EXECUTANDO);

            Guard.Against<DomainException>(
                etapaExecutando is null,
                $"Não há etapa em EXECUÇÃO para o fluxo de deploy {Id}");

            Guard.Against<DomainException>(etapaExecutando.Ordem < etapaSelecionada.Ordem,
                "Não é possível selecionar uma etapa futura!");

            foreach (var etapa in _etapas.Where(x =>
                         x.Ordem > etapaSelecionada.Ordem &&
                         x.Status != StatusEnum.CANCELADO))
            {
                etapa.Reiniciar();
            }
            etapaSelecionada.Iniciar();
        }

            #region AtualizarFluxoDeployEtapas
        private void ValidarRemocaoEtapasConcluidas(
        IEnumerable<string> etapasSelecionadas)
        {
            var etapasConcluidasNaoInformadas = _etapas
                .Where(x => x.Status == StatusEnum.CONCLUIDO)
                .Where(x => !etapasSelecionadas.Contains(x.SglEtapa, StringComparer.OrdinalIgnoreCase))
                .ToList();

            Guard.Against<DomainException>(etapasConcluidasNaoInformadas.Any(),
                "Não é permitido remover etapas já concluídas.");
        }

        private static void ValidarEtapasObrigatorias(
                IEnumerable<EtapaEntity> etapasAtivas,
                IEnumerable<string> etapasSelecionadas)
        {
            var obrigatoriasAusentes = etapasAtivas
                .Where(x => x.Obrigatorio)
                .Select(x => x.Sigla)
                .Except(etapasSelecionadas, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Guard.Against<DomainException>(obrigatoriasAusentes.Any(),
                $"As etapas obrigatórias não foram informadas: {string.Join(", ", obrigatoriasAusentes)}");
        }

        private void CancelarEtapasRemovidas(
            IEnumerable<string> etapasSelecionadas)
        {
            foreach (var etapa in _etapas)
            {
                if (!etapasSelecionadas.Contains(etapa.SglEtapa, StringComparer.OrdinalIgnoreCase))
                {
                    etapa.Cancelar();
                }
            }
        }

        private void ReorganizarEtapas(
            IEnumerable<EtapaEntity> etapasAtivas,
            IEnumerable<string> etapasSelecionadas)
        {
            var ordem = 1;

            foreach (var etapaSistema in etapasAtivas.OrderBy(x => x.Ordem))
            {
                if (!etapasSelecionadas.Contains(etapaSistema.Sigla, StringComparer.OrdinalIgnoreCase))
                    continue;

                var etapaFluxo = _etapas
                    .FirstOrDefault(x => x.SglEtapa.Equals(etapaSistema.Sigla, StringComparison.OrdinalIgnoreCase));

                if (etapaFluxo is null)
                {
                    etapaFluxo = new FluxoDeployEtapaEntity(
                        etapaSistema.Sigla,
                        ordem);

                    _etapas.Add(etapaFluxo);
                }
                else
                {
                    etapaFluxo.AtualizarOrdem(ordem);
                    etapaFluxo.Reiniciar();
                }

                ordem++;
            }
        }

        private void DefinirEtapaExecutando()
        {
            var etapaExecutando = _etapas
                .FirstOrDefault(x => x.Status == StatusEnum.EXECUTANDO);

            if (etapaExecutando != null)
                return;

            var proximaEtapa = _etapas
                .Where(x => x.Status == StatusEnum.NAO_INICIADO)
                .OrderBy(x => x.Ordem)
                .FirstOrDefault();

            proximaEtapa?.Iniciar();
        }
        #endregion

        #endregion

        private void GarantirEmExecucao()
            => Guard.Against<DomainException>(
                Status != StatusEnum.EXECUTANDO,
                $"Fluxo deploy '{Id}' não está em execução");
    }

}
