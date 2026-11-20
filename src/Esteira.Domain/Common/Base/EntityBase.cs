namespace Esteira.Domain.Common.Base
{
    // Id: chave interna (PK/FK/join), atribuída pelo banco, nunca sai da API.
    // PublicId: o que sai da API (URL, contrato, eventos). ADR-0014.
    public abstract class EntityBase
    {
        public long Id { get; internal set; }
        public Guid PublicId { get; private set; } = Guid.CreateVersion7();
    }
}
