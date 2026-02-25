---
name: domain-developer
description: Knowledge base for implementing Domain layer projects in the MediRoutes Platform. Covers project structure, domain services, entities, repositories, gateway/proxy interfaces, and configuration extensions. Use when creating entities, domain services, interfaces, config extensions, or reviewing domain layer code.
---

# Domain Developer — Skill

> Generic knowledge for implementing and evolving `*.Domain` projects in the MediRoutes Platform. Applies to all APIs (Routes, Violations, Scheduling, Search, BFF, etc.). Load this skill before generating or reviewing any Domain layer code.

---

## 1. Project Structure

A standard `*.Domain` project contains:

```
YourName.Domain/
├── Entities/                        ← Domain entities, value objects, enums, filter models
│   ├── SomeFeature/
│   └── Shared/
├── Interfaces/
│   ├── Services/                    ← IYourNameService contracts
│   ├── Gateways/                    ← IYourNameApiGateway contracts (BFF/gateway-style APIs)
│   ├── Proxies/                     ← IYourNameProxy contracts (external third-party services)
│   └── Repositories/                ← IYourNameRepository contracts (APIs with direct DB access)
├── Services/                        ← Domain service implementations
├── Extensions/
│   ├── ConfigurationExtensions.cs   ← All config key access lives here
│   └── ServiceCollectionExtensions.cs
├── Mappers/                         ← Domain-layer AutoMapper profile (entity ↔ entity)
└── Constants.cs
```

---

## 2. Two Domain Patterns

The domain layer operates in one of two modes depending on how the API accesses data. Identify which pattern applies before generating code.

### Pattern A — Repository-Based (API owns a database)

Used when the API has direct database access via EF Core. Domain entities map to DB tables via `BaseEntity`.

- Entities inherit `BaseEntity` from `MediR.Database.Models`
- Entities use `sealed class` with `{ get; set; }` (required by EF Core change tracking)
- Repository interfaces extend `IBaseEFCoreRepository<TEntity>` — they inherit all CRUD; add only custom query methods
- No domain services required for simple CRUD — controller calls repository directly via interface
- Domain `ServiceCollectionExtensions` registers `AddAutoMapper` for domain mappers

### Pattern B — Gateway/Proxy-Based (API calls other services, no direct DB)

Used when the API aggregates data from other platform APIs (gateways) or external services (proxies). No direct DB access.

- Entities are pure domain models — use `sealed record` with `{ get; init; }`
- Use `{ get; set; }` only when post-construction mutation is required (e.g., hydration) — comment why
- Domain services contain business logic and orchestration (aggregation, rules, derived values)
- Gateway interfaces defined here, implemented in Infrastructure
- Proxy interfaces defined here, implemented in Infrastructure (strategy pattern with `Name` property)
- Domain `ServiceCollectionExtensions` registers each `IService → Service` as `AddScoped`

---

## 3. Entities

### Repository-based entity (inherits BaseEntity, EF-compatible)

```csharp
using MediR.Database.Models;

namespace YourName.Domain.Entities;

public sealed class YourEntity : BaseEntity
{
    public int YourEntityId { get; set; }
    public string Name { get; set; }
    public double? OptionalValue { get; set; }
    public Guid? RowId { get; set; }
    public int SvClientId { get; set; }
}
```

### Search filter entity (for repository SearchAsync)

```csharp
namespace YourName.Domain.Entities;

public sealed class YourEntitySearchFilter
{
    public int? YourEntityId { get; set; }
    public string? Status { get; set; }
}
```

### Gateway-based entity (pure domain, immutable)

```csharp
namespace YourName.Domain.Entities.SomeFeature;

public sealed record YourEntity
{
    /// <summary>The unique identifier.</summary>
    public int Id { get; init; }

    /// <summary>The name.</summary>
    public string Name { get; init; }

    /// <summary>Set during post-construction hydration — see HydratorIncludeTypes.SOME_TYPE.</summary>
    public RelatedEntity? Related { get; set; }   // { get; set; } documented when mutable
}
```

### Enums

```csharp
namespace YourName.Domain.Entities.SomeFeature;

public enum StatusType
{
    Unknown = 0,
    Active = 1,
    Inactive = 2
}
```

Use explicit integer values when enums are serialized across API boundaries.

---

## 4. Interfaces

### Repository interface (Pattern A)

```csharp
using MediR.Database.Interfaces.Repository;
using YourName.Domain.Entities;

namespace YourName.Domain.Interfaces.Repositories;

/// <summary>Repository interface for managing YourEntity entities.</summary>
public interface IYourEntityRepository : IBaseEFCoreRepository<YourEntity>
{
    // Only add methods not covered by IBaseEFCoreRepository
    // e.g.: Task<YourEntity?> GetByExternalIdAsync(string externalId);
}
```

### Gateway interface (Pattern B — internal platform API)

All methods return `Task<BaseResponse<T>>`. XML doc comments required.

```csharp
using MediR.Models;
using YourName.Domain.Entities.SomeFeature;

namespace YourName.Domain.Interfaces.Gateways;

public interface IYourApiGateway
{
    /// <summary>Retrieves an entity by its unique identifier.</summary>
    /// <param name="id">The unique identifier.</param>
    /// <returns>A <see cref="BaseResponse{TResult}"/> containing the entity or error details.</returns>
    Task<BaseResponse<YourEntity>> GetByIdAsync(int id);

    /// <summary>Retrieves entities for a given date.</summary>
    /// <param name="date">The date to filter by.</param>
    /// <returns>A <see cref="BaseResponse{TResult}"/> containing the list of entities or error details.</returns>
    Task<BaseResponse<List<YourEntity>>> GetByDateAsync(DateTimeOffset date);

    /// <summary>Performs a write operation with cancellation support.</summary>
    /// <param name="ids">The identifiers to act upon.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="BaseResponse{TResult}"/> containing the updated entity or error details.</returns>
    Task<BaseResponse<YourEntity>> UpdateAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);
}
```

### Proxy interface (Pattern B — external third-party service, strategy pattern)

```csharp
using MediR.Models;
using YourName.Domain.Entities.SomeFeature;

namespace YourName.Domain.Interfaces.Proxies;

public interface IYourProxy
{
    ProviderType Name { get; }   // Strategy dispatch property

    Task<BaseResponse<YourResult>> GetAsync(YourRequest request);
}
```

### Service interface

```csharp
using MediR.Models;
using YourName.Domain.Entities.SomeFeature;

namespace YourName.Domain.Interfaces.Services;

public interface IYourService
{
    Task<BaseResponse<YourEntity>> GetByIdAsync(int id);
    Task<BaseResponse<List<YourEntity>>> GetByDateAsync(DateTimeOffset date);
}
```

---

## 5. Domain Services (Pattern B only)

Create a domain service only when there is real business logic: input validation, orchestrating multiple gateway calls, computing derived values, or enforcing business rules. Do not create a service that only forwards to a single gateway call.

```csharp
using MediR.Models;
using Microsoft.Extensions.Logging;
using YourName.Domain.Entities.SomeFeature;
using YourName.Domain.Interfaces.Gateways;
using YourName.Domain.Interfaces.Services;

namespace YourName.Domain.Services;

public sealed class YourService : IYourService
{
    private readonly IYourApiGateway _gateway;
    private readonly ILogger<YourService> _logger;

    public YourService(IYourApiGateway gateway, ILogger<YourService> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<BaseResponse<YourEntity>> GetByIdAsync(int id)
    {
        if (id <= 0)
        {
            return BaseResponse.CreateErrorResponse<BaseResponse<YourEntity>>(
                new Error(Constants.ErrorCodes.INVALID_INPUT, "ID must be greater than 0"));
        }

        return await _gateway.GetByIdAsync(id).ConfigureAwait(false);
    }

    public async Task<BaseResponse<List<YourEntity>>> GetByDateAsync(DateTimeOffset date)
    {
        if (date == default)
        {
            return BaseResponse.CreateErrorResponse<BaseResponse<List<YourEntity>>>(
                new Error(Constants.ErrorCodes.INVALID_INPUT, "Date is required"));
        }

        var result = await _gateway.GetByDateAsync(date).ConfigureAwait(false);
        if (result.HasErrors)
        {
            return result;
        }

        // Example: parallel hydration from a second gateway
        // var extra = await _otherGateway.GetExtraAsync().ConfigureAwait(false);

        return result;
    }
}
```

**Rules:**
- Always `sealed` unless inheritance is required
- Standard constructor (not primary constructor)
- Always store `_logger` — never accept logger without assigning it to a field
- Return `BaseResponse<T>` from all methods
- Use `ConfigureAwait(false)` on all awaited calls
- Use `BaseResponse.CreateErrorResponse<T>()` for business rule violations — never throw
- Use structured logging — never string interpolation

---

## 6. ConfigurationExtensions.cs

Place in `Microsoft.Extensions.Configuration` namespace using `#pragma warning disable IDE0130`. Each method wraps one config key.

```csharp
#pragma warning disable IDE0130

namespace Microsoft.Extensions.Configuration;

public static class ConfigurationExtensions
{
    // SDK/service URLs — nullable return, default to null
    public static string? GetYourServiceApiUrl(this IConfiguration configuration, string? defaultValue = default)
    {
        return configuration.GetValue("YourService:BaseUrl", defaultValue);
    }

    // Required secrets — non-nullable, default to null (caller validates at startup)
    public static string GetAuthorizationSigningKey(this IConfiguration configuration, string defaultValue = default)
    {
        return configuration.GetValue("AuthSecretKey", defaultValue);
    }

    // Connection strings
    public static string GetRedisConnectionString(this IConfiguration configuration, string defaultValue = default)
    {
        return configuration.GetValue("RedisConnectionString", defaultValue);
    }

    // Numeric settings with sensible defaults
    public static int GetCacheTimeoutMinutes(this IConfiguration configuration, int? defaultValue = default)
    {
        return configuration.GetValue("CacheTimeoutMinutes", defaultValue ?? 5);
    }
}
```

**Rules:**
- One method per config key — never access `IConfiguration` directly in services
- Services call these methods; they never call `config["key"]` themselves
- Add a new method here whenever a new config value is needed by any layer

---

## 7. Constants.cs

```csharp
namespace YourName.Domain;

public static class Constants
{
    // Sentinel values
    public static readonly DateTime NULL_DATE = new(1900, 1, 1);

    // Named HTTP clients used by Infrastructure proxies
    public const string EXTERNAL_SERVICE_HTTP_CLIENT_NAME = "ExternalServiceHttpClient";

    public static class ErrorCodes
    {
        // Reserve a numeric range per domain — document the range
        // e.g. 4300–4399 for Violations, 12500–12799 for BFF
        public const int INVALID_INPUT = 4300;
        public const int NOT_FOUND = 4301;
    }

    public static class HydratorIncludeTypes
    {
        // String tokens used to select which hydrators run post-mapping
        public const string VEHICLE = "Vehicle";
        public const string SOME_TYPE = "SomeType";
    }
}
```

**Rules:**
- Constants: UPPER_SNAKE_CASE
- Error codes: document the numeric range as a comment
- `static readonly` for non-primitive types (DateTime), `const` for strings and numbers

---

## 8. ServiceCollectionExtensions.cs

### Pattern A (repository-based) — register AutoMapper

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YourName.Domain.Mappers;

namespace YourName.Domain.Extensions;

public static class ServiceCollectionExtensions
{
    public static void RegisterDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAutoMapper(typeof(MappingProfile).Assembly);
    }
}
```

### Pattern B (gateway-based) — register domain services

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YourName.Domain.Interfaces.Services;
using YourName.Domain.Services;

namespace YourName.Domain;

public static class ServiceCollectionExtensions
{
    public static void RegisterDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IYourService, YourService>();
        services.AddScoped<IOtherService, OtherService>();
    }
}
```

All domain services register as `AddScoped`.

---

## 9. Domain AutoMapper Profile (Pattern A)

When the Domain project contains entity-to-entity or entity-to-filter mappings:

```csharp
using AutoMapper;

namespace YourName.Domain.Mappers;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Add maps when domain owns transformations between its own types
        // e.g. entity → search filter, or DB entity → domain entity
    }
}
```

If there are no domain-level mappings needed, keep the file with an empty constructor — it must exist so `AddAutoMapper(typeof(MappingProfile).Assembly)` has an anchor.

---

## 10. New Code Checklist

### Adding a new entity
1. Place in `Entities/<Area>/`
2. Pattern A: inherit `BaseEntity`, `sealed class`, `{ get; set; }`
3. Pattern B: `sealed record`, `{ get; init; }`, XML doc comments on all public properties
4. Add enum values with explicit integers when serialized across API boundaries

### Adding a new repository interface (Pattern A)
1. Create in `Interfaces/Repositories/I<Name>Repository.cs`
2. Extend `IBaseEFCoreRepository<TEntity>`
3. Only add methods not already on `IBaseEFCoreRepository`
4. Implementation goes in `*.Infrastructure`

### Adding a new gateway interface (Pattern B)
1. Create in `Interfaces/Gateways/I<Name>ApiGateway.cs`
2. All methods return `Task<BaseResponse<T>>`
3. XML doc comments with `<summary>`, `<param>`, `<returns>` on every method
4. Implementation goes in `*.Infrastructure`

### Adding a new domain service (Pattern B)
1. Create interface in `Interfaces/Services/I<Name>Service.cs`
2. Create implementation in `Services/<Name>Service.cs`
3. `sealed` class, standard constructor, store `_logger`
4. Return `BaseResponse<T>`, use `ConfigureAwait(false)`, validate inputs
5. Register in `ServiceCollectionExtensions.RegisterDomainServices` as `AddScoped`

### Adding a new config value
1. Add extension method to `Extensions/ConfigurationExtensions.cs`
2. One method per key — wrap `configuration.GetValue<T>(key, default)`
3. Never access `IConfiguration` directly in services or Infrastructure

---

## Related Resources

| Resource | Path | Load When |
|----------|------|-----------|
| API Skill | `skills/api-developer/SKILL.md` | Creating/modifying controllers, DTOs, or API mappings |
| Infrastructure Skill | `skills/infrastructure-developer/SKILL.md` | Implementing domain interfaces (gateways, proxies, repositories) |
| Architecture Instructions | `instructions/architecture.instructions.md` | Always — coding standards and naming conventions |
| Testing Instructions | `instructions/testing.instructions.md` | Writing or reviewing tests |
| SQL Metadata Skill | `skills/sql-metadata/SKILL.md` | Need to inspect database schema before generating entities |
