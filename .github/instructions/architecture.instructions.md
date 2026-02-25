---
applyTo: "**/*.cs"
---

# MediRoutes Platform — Architecture & Coding Standards

## Solution Summary

This solution implements a modular, service-oriented architecture using .NET 8+ and C# 12.0. It follows **Onion Architecture** principles to ensure separation of concerns, maintainability, and testability. Services communicate via SDKs. Configuration is managed centrally, supporting local and cloud-based (Azure) sources.

## Solution Structure

- **API** — Web API services, each a standalone API with its own onion architecture in its own folder.
- **Infrastructure** — Shared infrastructure projects that standardize common functionality (logging, configuration, HTTP, auth, cache, testing). All API projects reference these.
- **Shared** — Projects that bridge legacy NuGet packages and new onion architecture code.
- **Demo** — Sample application demonstrating best practices. Refer to this when implementing new features.

## Onion Architecture Layers

| Layer | Project Pattern | Responsibility |
|---|---|---|
| API | `*.Api` | Controllers, DTOs, AutoMapper profiles, endpoint wiring |
| Domain | `*.Domain` | Business logic, domain entities, service interfaces, gateway/proxy interfaces, config extensions |
| Infrastructure | `*.Infrastructure` | Gateway/proxy implementations, repositories, SDK clients, DI registration |
| Tests | `*.Tests` | Unit and integration tests per layer |

**Dependency rule:** Domain has zero knowledge of Infrastructure or web concerns. Infrastructure implements Domain interfaces.

```
Api → Infrastructure → Domain → MediR.* shared libraries
```

## Key Architectural Concepts

### BaseResponse Pattern
All domain services, application services, gateways, and proxies return `BaseResponse<T>` from `Infrastructure\MediR.Models\BaseResponse.cs`.

- **Never throw exceptions for business rule violations** — return error responses.
- `BaseResponse.CreateErrorResponse<T>()` — general errors
- `CreateNotFoundResponse<T>()` — 404 scenarios
- `CreateBadRequestError()` — validation failures
- Callers check `HasErrors`, `HasNotFoundErrors()`, or `HasBadRequestErrors()` to handle error flow.

### Infrastructure Patterns
- **Gateway** — wraps internal cross-platform service calls via Kiota-generated SDKs. Lives in `Infrastructure\Gateway\`. Returns `BaseResponse<T>`.
- **Proxy** — wraps external third-party services (e.g., Azure Maps, Azure Search). Lives in `Infrastructure\Proxy\`. Returns `BaseResponse<T>`.
- Both catch external exceptions, log errors, and never let exceptions bubble up unhandled.
- Every Infrastructure project exposes `ServiceCollectionExtensions.cs` with a `Register*Services` extension method.
- Use `AddCollectionOfServices<TInterface>` for strategy-pattern registrations (multiple implementations of same interface).

### SDK-Based Communication
Services interact via Kiota-generated SDK clients. Gateways wrap SDK clients, transforming external DTOs into domain entities.

### Centralized Configuration
All configuration is loaded in a two-pass process, supporting local development and Azure App Configuration. Configuration values are always exposed via `ConfigurationExtensions.cs` in the Domain project — never accessed directly via `IConfiguration` in services.

---

## Coding Standards

### Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Classes, Methods, Enums, Public Members, Namespaces | PascalCase | `MyService`, `GetDataAsync` |
| Interfaces | `I` prefix + PascalCase | `ICountryService` |
| Abstract classes | `Base` prefix + PascalCase | `BaseRepository` |
| Private members | `_` prefix + camelCase | `_countryService` |
| Constants | UPPER_SNAKE_CASE | `MAX_RETRY_COUNT` |
| Async methods | `Async` suffix | `GetDataAsync` |

### File Naming
- Filenames and directories: PascalCase.
- Main class name matches filename.

### Code Organization (member order)
1. Constants
2. Private Members
3. Protected Members
4. Private Readonly Members
5. Properties
6. Public Static Properties
7. Protected Properties
8. Constructor
9. Public Methods
10. Protected Methods
11. Private Methods

### Formatting
- One statement per line.
- One assignment per statement.
- Tab indentation.
- Space after control keywords and commas.
- Curly braces required for **all** control statements — no one-liner exceptions.
- Use `new MyClass()` or `new()` for object instantiation.
- Use file-scoped namespaces: `namespace MyApp.Domain.Entities;`
- **Prefer standard constructors over primary constructors** — primary constructors expose fields as public properties.

### Logging
- Use structured logging with message template parameters. **Never** use string interpolation in log messages.
  - ✅ `_logger.LogInformation("Search completed for {Query} with {Count} results", query, count);`
  - ❌ `_logger.LogInformation($"Search completed for {query}");`
- Use `LogWithGuard` for log levels below Warning (Debug, Trace, Information).
- **Never** use `Console.WriteLine`.

### Error Handling
- Infrastructure layer (Proxies, Gateways) must catch external exceptions and return `BaseResponse` error responses.
- Controllers must **never** return domain entities directly — always map to DTOs via AutoMapper.
- Use `BuildActionResultFromErrors()` in controllers to convert `BaseResponse` errors to HTTP status codes.

### Async
- All methods returning `Task` or `Task<T>` **must** have the `Async` suffix.
- Use `.ConfigureAwait(false)` on awaited calls in Domain and Infrastructure layers (not needed in Controllers).
- All async methods should accept `CancellationToken cancellationToken = default` and forward it downstream.
- **Never** block async calls with `.Result` or `.GetAwaiter().GetResult()`.

---

## Layer-Specific Rules

### API Layer (`*.Api` projects)
- All controllers inherit from `MediRSecureBaseController`.
- All endpoints must have complete `ProducesResponseType` attributes (200, 400, 401, 403, 404, 500 as applicable).
- Validate `ModelState` and return `ValidationProblem(ModelState)` when invalid.
- Map DTOs → domain models via AutoMapper. Never pass DTOs into domain services directly.
- Call `BuildActionResultFromErrors()` for all domain error responses.
- No business logic in controllers.

```csharp
// ✅ REQUIRED pattern
[HttpGet("{id:int}")]
[ProducesResponseType(typeof(MyDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<MyDto>> GetByIdAsync([FromRoute] int id)
{
    if (id == 0)
    {
        ModelState.AddModelError(nameof(id), "ID must be greater than 0.");
        return ValidationProblem(ModelState);
    }
    var result = await _service.GetByIdAsync(id);
    if (result.HasErrors)
        return BuildActionResultFromErrors(result);
    return Ok(_mapper.Map<MyDto>(result.Result));
}
```

### Domain Layer (`*.Domain` projects)
- No infrastructure dependencies (`Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore.*`, `*.Infrastructure.*` — all forbidden).
- All service methods return `BaseResponse<T>`.
- Never throw exceptions for business rule violations.
- Configuration accessed only via domain `ConfigurationExtensions` methods.
- Gateway/proxy interfaces defined here, implemented in Infrastructure.

```csharp
// ✅ REQUIRED: file-scoped namespace
namespace MyApp.Domain.Services;

// ✅ REQUIRED: standard constructor, sealed, BaseResponse<T>
public sealed class MyService : IMyService
{
    private readonly IMyGateway _gateway;
    private readonly ILogger<MyService> _logger;

    public MyService(IMyGateway gateway, ILogger<MyService> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<BaseResponse<MyEntity>> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _gateway.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.HasErrors)
            return result;
        return new BaseResponse<MyEntity> { Result = result.Result };
    }
}
```

### Infrastructure Layer (`*.Infrastructure` projects)
- Repositories inherit from `EFCoreRepository` in `Infrastructure\MediR.Database\Repository\EFCoreRepository.cs`.
- DbContexts inherit from `MediRBaseDataContext` in `Infrastructure\MediR.Database\MediRBaseDataContext.cs`.
- Use the `TryAsync()` extension for SDK gateway calls with `GatewayExceptionConverter` for error handling.
- Gateways: 1 gateway class per upstream API, wrapping 1 Kiota SDK client, implementing 1 domain interface.
- All gateways/proxies return `BaseResponse<T>` — never raw SDK types or exceptions.

```csharp
// ✅ REQUIRED: Gateway pattern
public async Task<BaseResponse<MyEntity>> GetAsync(int id, CancellationToken cancellationToken = default)
{
    var response = new BaseResponse<MyEntity>();
    var result = await _client.Api.Resource.GetAsync(config => { config.QueryParameters.Id = id; }, cancellationToken)
        .TryAsync((ex) =>
        {
            var converted = GatewayExceptionConverter.Convert<ProblemDetails, MediRoutesProblemDetails>(ex);
            _logger.LogError("Failed to get entity {Id}: {Detail}", id, converted?.Detail ?? "Unknown error");
            response = converted!.CreateErrorResponse<BaseResponse<MyEntity>>();
            response.Result = default;
        });

    if (!response.HasErrors)
        response.Result = _mapper.Map<MyEntity>(result);

    return response;
}
```

---

## Configuration Pattern

```csharp
// ✅ REQUIRED: Configuration extensions in Domain projects
public static class ConfigurationExtensions
{
    public static string GetMyServiceEndpoint(this IConfiguration configuration) =>
        configuration["MyService:Endpoint"] ??
        throw new InvalidOperationException("MyService:Endpoint not configured");
}

// ❌ FORBIDDEN: Direct config access in services
var endpoint = config["MyService:Endpoint"]; // WRONG
```

---

## Dependency Injection

- Every Infrastructure project exposes a `Register*Services(IServiceCollection)` extension method.
- Scoped: domain services, gateways, proxies, repositories.
- Singleton: caches, shared stateless utilities.
- Use `AddCollectionOfServices<TInterface>` for strategy-pattern multi-implementations.

---

## Common Mistakes (Hard Failures)

- ❌ Missing `Async` suffix on async methods
- ❌ Constants not in UPPER_SNAKE_CASE
- ❌ Private members missing `_` prefix
- ❌ Curly braces omitted on single-line control statements
- ❌ String interpolation in log messages
- ❌ `Console.WriteLine` anywhere
- ❌ Returning domain entities from controllers
- ❌ Throwing exceptions for business rule violations
- ❌ Direct `IConfiguration` access in services
- ❌ Domain referencing Infrastructure or web packages
- ❌ Using System.Text.Json (always Newtonsoft.Json)
