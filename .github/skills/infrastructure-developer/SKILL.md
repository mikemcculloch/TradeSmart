---
name: infrastructure-developer
description: Knowledge base for implementing Infrastructure layer projects in the MediRoutes Platform. Covers Pattern A (Repository/EF Core) for direct database access and Pattern B (Gateway/Proxy) for calling other services. Use when creating gateways, proxies, repositories, DbContexts, filter profiles, SDK client registration, or reviewing infrastructure code.
---

# Infrastructure Developer Skill

> Generic knowledge base for implementing Infrastructure layer projects in the MediRoutes Platform. Covers both infrastructure patterns: **Pattern A (Repository/EF Core)** for API services with direct database access and **Pattern B (Gateway/Proxy)** for BFF services that call other platform APIs or external services.

---

## 1. Two Infrastructure Patterns

### Pattern A — Repository / EF Core (API Services)
Used when the service **owns its own database**. Infrastructure implements EF Core DbContext, repositories, and filter profiles.

```
YourService.Infrastructure/
├── Data/
│   └── YourDbContext.cs           # EF Core DbContext (extends MediRBaseDataContext<T>)
├── Repositories/
│   └── YourEntityRepository.cs   # Implements IYourEntityRepository
├── Mappers/
│   └── YourFilterMappingProfile.cs  # FilterProfile for search predicate building
└── ServiceCollectionExtensions.cs
```

### Pattern B — Gateway / Proxy (BFF Services)
Used when the service **delegates all data access** to other platform APIs (Gateways) or external third-party services (Proxies).

```
YourService.Infrastructure/
├── Gateway/
│   └── YourApiGateway/
│       ├── YourApiGateway.cs       # Implements IYourApiGateway
│       ├── client/                 # Kiota-generated SDK client (auto-generated)
│       └── client-generate.ps1    # Script to regenerate SDK client
├── Proxy/
│   └── YourExternalProxy.cs       # Implements IYourExternalProxy
├── Mappers/
│   └── YourMappingProfile.cs      # SDK DTO → Domain entity mappings
└── ServiceCollectionExtensions.cs
```

---

## 2. Pattern A — DbContext

Extend `MediRBaseDataContext<T>` for multi-tenant data contexts.

```csharp
using MediR.Database;
using Microsoft.EntityFrameworkCore;

namespace YourService.Infrastructure.Data;

public sealed class YourDbContext : MediRBaseDataContext<YourDbContext>
{
    public YourDbContext(DbContextOptions<YourDbContext> options) : base(options)
    {
    }

    public DbSet<YourEntity> YourEntities => Set<YourEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(YourDbContext).Assembly);
    }
}
```

**Rules:**
- Always extend `MediRBaseDataContext<T>` — never `DbContext` directly
- Use `Set<T>()` for DbSet properties — never backing fields
- Apply entity configurations via `ApplyConfigurationsFromAssembly`

---

## 3. Pattern A — Repository

Extend `EFCoreRepository<TEntity, TKey, TDbContext>` from `MediR.Database`.

```csharp
using MediR.Database;
using MediR.Models;
using Microsoft.EntityFrameworkCore;
using YourService.Domain.Entities;
using YourService.Domain.Interfaces.Repositories;

namespace YourService.Infrastructure.Repositories;

public sealed class YourEntityRepository : EFCoreRepository<YourEntity, int, YourDbContext>, IYourEntityRepository
{
    public YourEntityRepository(YourDbContext context) : base(context)
    {
    }

    public async Task<BaseResponse<YourEntity>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.YourEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity == null)
        {
            return BaseResponse.CreateNotFoundResponse<BaseResponse<YourEntity>>();
        }

        return new BaseResponse<YourEntity> { Result = entity };
    }
}
```

**Rules:**
- Always `sealed`
- Always `AsNoTracking()` for read-only queries
- Return `BaseResponse.CreateNotFoundResponse<T>()` for missing entities — never throw
- Accept `CancellationToken` on all async methods
- Never expose `IQueryable` outside the repository

---

## 4. Pattern A — FilterProfile

Used with `SearchAsync` to build EF-compatible predicates from filter DTOs.

```csharp
using MediR.Database.Search;
using System.Linq.Expressions;
using YourService.Domain.Entities;
using YourService.Infrastructure.Data;

namespace YourService.Infrastructure.Mappers;

public sealed class YourEntityFilterMappingProfile : FilterProfile<YourEntity, YourDbContext>
{
    public YourEntityFilterMappingProfile()
    {
        // Exact match
        CreateFilter<string>(
            nameof(YourEntityFilter.Name),
            (value) => (entity) => entity.Name == value
        );

        // Contains / partial match
        CreateFilter<string>(
            nameof(YourEntityFilter.Code),
            (value) => (entity) => entity.Code.Contains(value)
        );

        // Enum filter
        CreateFilter<YourStatusEnum>(
            nameof(YourEntityFilter.Status),
            (value) => (entity) => entity.Status == value
        );
    }
}
```

**Rules:**
- One `FilterProfile` per searchable entity
- Filters are registered as named predicates — name must match the property name on the filter DTO exactly
- Registered automatically via `AddMultiTenantDBContext` or explicit registration

---

## 5. Pattern B — Gateway

Gateways call internal MediRoutes platform APIs via Kiota-generated SDK clients.

```csharp
using AutoMapper;
using MediR.Http;
using MediR.Models;
using Microsoft.Extensions.Logging;
using YourService.Domain.Entities;
using YourService.Domain.Interfaces.Gateways;
using YourService.Infrastructure.Gateway.YourApi.Client;

namespace YourService.Infrastructure.Gateway.YourApi;

public sealed class YourApiGateway : IYourApiGateway
{
    private readonly YourApiSdkClient _client;
    private readonly ILogger<YourApiGateway> _logger;
    private readonly IMapper _mapper;

    public YourApiGateway(YourApiSdkClient client, ILogger<YourApiGateway> logger, IMapper mapper)
    {
        _client = client;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<BaseResponse<YourEntity>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = new BaseResponse<YourEntity>();

        var result = await _client.Api.YourEntities[id].GetAsync(cancellationToken: cancellationToken)
            .TryAsync((ex) =>
            {
                var convertedResponse = GatewayExceptionConverter.Convert<ProblemDetails, MediRoutesProblemDetails>(ex);
                _logger.LogError("Error fetching YourEntity {Id}: {ErrorDetail}", id, convertedResponse?.Detail ?? "Unknown error");
                response = convertedResponse!.CreateErrorResponse<BaseResponse<YourEntity>>();
                response.Result = default;
            });

        if (!response.HasErrors)
        {
            response.Result = _mapper.Map<YourEntity>(result);
        }

        return response;
    }
}
```

**Error handling rules (gateway):**
1. Initialize `var response = new BaseResponse<T>()` before the `TryAsync` call
2. Use `.TryAsync()` — never try/catch directly
3. In the error callback: `GatewayExceptionConverter.Convert<ProblemDetails, MediRoutesProblemDetails>(ex)`
4. Use structured logging — `_logger.LogError("Message {Param}: {Detail}", param, detail)` — **never string interpolation**
5. Set `response.Result = default` (or `[]` for lists, `false` for booleans) in the error callback
6. After `TryAsync`, check `!response.HasErrors` before mapping the result

---

## 6. Pattern B — Proxy

Proxies call external third-party services outside the MediRoutes platform.

```csharp
using MediR.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using YourService.Domain;
using YourService.Domain.Interfaces.Proxies;

namespace YourService.Infrastructure.Proxy;

public sealed class YourExternalProxy : IYourExternalProxy
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<YourExternalProxy> _logger;

    // Required for strategy-pattern dispatch (AddCollectionOfServices<IYourProxy>)
    public YourProviderType Name => YourProviderType.SomeProvider;

    public YourExternalProxy(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<YourExternalProxy> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BaseResponse<YourResult>> GetDataAsync(string param)
    {
        var client = _httpClientFactory.CreateClient(Constants.YOUR_HTTP_CLIENT_NAME);
        var url = $"{_configuration.GetYourApiUrl()}/endpoint?key={_configuration.GetYourApiKey()}&param={param}";

        var httpResponse = await client.GetAsync(url);

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogError("External API call failed {StatusCode} for param {Param}", httpResponse.StatusCode, param);
            return new BaseResponse<YourResult>
            {
                Error = new Error(Constants.ErrorCodes.EXTERNAL_API_ERROR, "External API call failed")
            };
        }

        var json = await httpResponse.Content.ReadAsStringAsync();
        var data = JsonConvert.DeserializeObject<dynamic>(json);

        return new BaseResponse<YourResult> { Result = MapToResult(data) };
    }
}
```

**Rules:**
- Use `IHttpClientFactory` with named clients — never `new HttpClient()`
- All configuration via domain `ConfigurationExtensions` — never raw `IConfiguration` keys inline
- Check `IsSuccessStatusCode` — never assume success
- Use `Newtonsoft.Json` — never `System.Text.Json`
- Named HTTP client constant belongs in domain `Constants.cs`

---

## 7. Pattern B — SDK Client Generation (Kiota)

Each gateway subfolder contains a PowerShell script to (re)generate the Kiota SDK client.

```powershell
# client-generate.ps1
$solutionRoot = (& git rev-parse --show-toplevel).Trim()
& (Join-Path $solutionRoot "kiota-client-generate.ps1") `
    -SolutionRoot $PSScriptRoot `
    -OpenApiUrl "http://localhost:<port>/swagger/v1/swagger.json" `
    -ClassName "<Name>SdkClient" `
    -NamespaceName "<Name>.Sdk"
```

**Registration in DI:**
```csharp
services.RegisterSdk<YourApiSdkClient>(new SdkOptions
{
    BaseUrl = configuration.GetYourApiBaseUrl()
});
```

---

## 8. AutoMapper Profiles

### Pattern A — Domain Entity Mapping (Repository-based)
Typically no infrastructure-layer mapping needed — repositories return domain entities directly from EF Core.

### Pattern B — SDK DTO → Domain Entity Mapping (Gateway-based)
Each gateway area has its own `AutoMapper.Profile` in `Mappers/`.

```csharp
using AutoMapper;
using YourService.Domain.Entities;
using YourService.Infrastructure.Gateway.YourApi.Client.Models;

namespace YourService.Infrastructure.Mappers;

public sealed class YourEntityMappingProfile : Profile
{
    public YourEntityMappingProfile()
    {
        CreateMap<YourEntityDto, YourEntity>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id ?? 0))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src =>
                src.CreatedAtOffset.HasValue
                    ? src.CreatedAtOffset.Value.DateTime
                    : DateTime.MinValue));
    }
}
```

**Mapping conventions:**
- `DateTimeOffset` → `DateTime`: always null-safe
- Nullable SDK fields → non-nullable domain fields: provide explicit defaults (`?? 0`, `?? string.Empty`, `DateTime.MinValue`)
- Enum mapping: explicit cast or parse — never convention-based
- `UntypedNode` / `UntypedArray` (Kiota): requires explicit `switch`/conversion logic
- All profiles `sealed`

---

## 9. ServiceCollectionExtensions

### Pattern A (Repository/EF Core)

```csharp
using MediR.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YourService.Domain.Interfaces.Repositories;
using YourService.Infrastructure.Data;
using YourService.Infrastructure.Mappers;
using YourService.Infrastructure.Repositories;

namespace YourService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMultiTenantDBContext<YourDbContext>(configuration);

        services.AddScoped<IYourEntityRepository, YourEntityRepository>();

        services.AddAutoMapper(typeof(YourFilterMappingProfile));

        return services;
    }
}
```

### Pattern B (Gateway/Proxy)

```csharp
using MediR.Http.Sdk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly.Contrib.WaitAndRetry;
using YourService.Domain;
using YourService.Domain.Extensions;
using YourService.Domain.Interfaces.Gateways;
using YourService.Domain.Interfaces.Proxies;
using YourService.Infrastructure.Gateway.YourApi;
using YourService.Infrastructure.Gateway.YourApi.Client;
using YourService.Infrastructure.Mappers;
using YourService.Infrastructure.Proxy;

namespace YourService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Gateways
        services.AddScoped<IYourApiGateway, YourApiGateway>();

        // SDK clients (Kiota)
        services.RegisterSdk<YourApiSdkClient>(new SdkOptions
        {
            BaseUrl = configuration.GetYourApiBaseUrl()
        });

        // Proxies with strategy pattern
        services.AddCollectionOfServices<IYourProxy>(typeof(ServiceCollectionExtensions).Assembly);

        // Named HTTP clients with resilience
        services.AddHttpClient(Constants.YOUR_HTTP_CLIENT_NAME)
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(
                    medianFirstRetryDelay: TimeSpan.FromMilliseconds(10),
                    retryCount: 3)));

        // AutoMapper
        services.AddAutoMapper(typeof(YourEntityMappingProfile));

        return services;
    }
}
```

---

## 10. New Code Checklist

### Adding a Gateway (Pattern B)
- [ ] Create `Gateway/<ApiName>/` folder with `<ApiName>Gateway.cs` and `client-generate.ps1`
- [ ] Run `client-generate.ps1` to produce Kiota `client/` subfolder
- [ ] Implement domain interface `IYourApiGateway` from `YourService.Domain`
- [ ] Use `TryAsync()` + `GatewayExceptionConverter` for every SDK call
- [ ] Use structured logging — no string interpolation in `LogError`
- [ ] Add `AutoMapper.Profile` in `Mappers/` for SDK DTO → domain entity
- [ ] Register `AddScoped<IYourApiGateway, YourApiGateway>()` in `ServiceCollectionExtensions`
- [ ] Register `RegisterSdk<YourApiSdkClient>(SdkOptions)` in `ServiceCollectionExtensions`
- [ ] Define interface in `YourService.Domain/Interfaces/Gateways/`
- [ ] Add config extension in domain `ConfigurationExtensions.cs`

### Adding a Proxy (Pattern B)
- [ ] Create `Proxy/<ProviderName>Proxy.cs` implementing domain interface
- [ ] Implement `Name` property if using strategy dispatch
- [ ] Use `IHttpClientFactory` with a named client constant from domain `Constants.cs`
- [ ] Register named HTTP client with Polly resilience in `ServiceCollectionExtensions`
- [ ] Register via `AddCollectionOfServices<IProxy>()` (strategy) or `AddScoped<>()` (single)
- [ ] Add config extension methods in domain project for new config keys

### Adding a Repository (Pattern A)
- [ ] Extend `EFCoreRepository<TEntity, TKey, TDbContext>`
- [ ] Implement domain interface `IYourEntityRepository`
- [ ] Use `AsNoTracking()` for all read queries
- [ ] Return `BaseResponse.CreateNotFoundResponse<T>()` for missing entities
- [ ] Accept `CancellationToken` on all async methods
- [ ] Register `AddScoped<IYourEntityRepository, YourEntityRepository>()` in `ServiceCollectionExtensions`

### Adding a FilterProfile (Pattern A)
- [ ] Extend `FilterProfile<TEntity, TDbContext>`
- [ ] Create one filter per filterable property using `CreateFilter<TValue>(name, predicate)`
- [ ] Property name must exactly match the filter DTO property name
- [ ] Register via `AddAutoMapper` or explicit registration in `ServiceCollectionExtensions`

---

## Related Resources

| Resource | Path | Load When |
|----------|------|-----------|
| API Skill | `skills/api-developer/SKILL.md` | Creating/modifying controllers, DTOs, or API mappings |
| Domain Skill | `skills/domain-developer/SKILL.md` | Creating/modifying domain entities, services, or interfaces |
| Architecture Instructions | `instructions/architecture.instructions.md` | Always — coding standards and naming conventions |
| Testing Instructions | `instructions/testing.instructions.md` | Writing or reviewing tests |
| SQL Metadata Skill | `skills/sql-metadata/SKILL.md` | Need to inspect database schema before generating entities |
