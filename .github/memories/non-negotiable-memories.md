# Non-Negotiable Memories

These rules have zero exceptions. Any violation is a hard failure.

| # | Rule |
|---|---|
| 1 | **Never execute a plan without explicit user approval.** Present the plan, stop, wait for response. |
| 2 | **Always use Newtonsoft.Json.** Never use System.Text.Json anywhere in the solution. |
| 3 | **Always check and update affected unit tests** when making code changes — without being asked. |
| 4 | **Always use `MediR.Testing` helpers** (BaseTestFixture, Mocks/Builders) over raw mocking. |
| 5 | **Never return domain entities from controllers.** Always map to DTOs via AutoMapper. |
| 6 | **Never throw exceptions for business rule violations.** Return `BaseResponse` error responses. |
| 7 | **Never use string interpolation in log messages.** Use structured logging templates with parameters. |
| 8 | **Never access `IConfiguration` directly in services.** Use `ConfigurationExtensions` methods from the Domain project. |
| 9 | **Domain layer must have zero knowledge of Infrastructure or web concerns.** |
| 10 | **All async methods must have the `Async` suffix.** |
| 11 | **Curly braces are required on all control statements** — no one-liner exceptions. |
