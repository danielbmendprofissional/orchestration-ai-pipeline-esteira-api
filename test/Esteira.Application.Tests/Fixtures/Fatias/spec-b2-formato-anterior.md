# B2 — Result pattern + Problem Details

## Contexto necessário
- esteira-api/CLAUDE.md (camadas, padrões, testes)
- esteira-api/docs/PENDENCIAS.md
- docs/adr/0003-erros.md (a decisão; esta spec não repete o formato, só o aplica)
- docs/adr/0014-identificadores.md (nenhum `long` em mensagem de erro)
- specs/feature/base/01-esqueleto-do-modelo.md (precisa estar feita: é sobre o esqueleto que esta fatia trabalha)

## Ideia
Hoje erro de negócio é exception: os VOs lançam `DomainException` pelo `Guard`, o `ValidationBehavior` lança `ValidationException` e o `ExceptionMiddleware` devolve `{ message }` com o status que a exception carrega. Esta fatia aplica a ADR-0003 no esqueleto, antes de existir o primeiro handler de negócio: **todo handler e toda fábrica de VO devolve `Result`**, a API traduz `Result` em status + `{ errors: [...] }`, e só o que é inesperado vira **Problem Details** (RFC 9457) com `traceId`, sem detalhe técnico.

