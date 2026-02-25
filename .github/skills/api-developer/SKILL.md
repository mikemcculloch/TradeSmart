---
name: api-developer
description: Knowledge base for implementing API layer projects in the MediRoutes Platform. Covers project structure, bootstrap sequence, controllers, DTOs, AutoMapper profiles, search patterns, and hydrators. Use when creating endpoints, adding controllers, mapping DTOs, wiring up Program.cs, or reviewing API layer code.
---
# API Developer — Skill

> Generic knowledge for implementing and evolving `*.Api` projects in the MediRoutes Platform. Applies to all APIs (Routes, Violations, Scheduling, Search, BFF, etc.). Load this skill before generating or reviewing any API layer code.

---

## 1. Project Structure

A standard `*.Api` project contains:

```
YourName.Api/
├── Controllers/          ← One controller per resource area
├── Dto/                  ← Request and response DTOs, grouped by domain area
│   ├── SomeFeature/
│   └── Shared/
├── Mappers/              ← AutoMapper profiles (Api DTOs ↔ Domain entities)
├── Shared/
│   └── Hydrators/        ← Optional: IHydrator<T> implementations for post-mapping enrichment
├── ServiceCollectionExtensions.cs
└── Program.cs
```

---

## 2. Program.cs — Bootstrap Sequence

Every API follows this exact sequence. Do not reorder or skip steps.

```csharp
const string APP_NAME = "YourApiName";
var builder = WebApplication.CreateBuilder(args);

// 1. Load configuration (two-pass: local + Azure App Config)
var config = builder.Services.AddMediRoutesConfiguration<Program>(builder.Configuration, new ConfigurationOptions
{
    AppName = APP_NAME
});
var isDevelopment = builder.Environment.IsDevelopment();

// 2. Logging (Application Insights)
builder.AddMediRoutesLogging(APP_NAME, (opts) =>
{
    opts.ApplicationInsightsOptions.IncludeApplicationInsightsSink = true;
    opts.ApplicationInsightsOptions.ConnectionString = config.GetApplicationInsightsConnection();
});

// 3. User profile services
builder.Services.AddUserProfileServices(new()
{
    EFormanConnectionString = config.GetEFormanConnectionString(),
    SqlServerCompatibilityLevel = config.GetSqlServerCompatibilityLevel()
});

// 4. Cache providers
builder.Services.AddInMemoryCacheProvider();
builder.Services.AddRedisCacheProvider(cacheOptions: new CacheOptions
{
    RedisCacheOptions = new RedisCacheOptions { ConnectionString = config.GetRedisConnectionString() }
});

// 5. CORS — only when API is consumed by browser clients
var corsPolicyOptions = new CorsPolicyOptions
{
    PolicyName = "Default",
    Origins = config.GetOrigins(),
    IncludeAuthorization = true
};
builder.Services.AddCorsPolicy(corsPolicyOptions);

// 6. Swagger and app setup
var swaggerOptions = new SwaggerOptions
{
    SwaggerServiceInfo = new SwaggerServiceInfo { Title = "Your Api Title", Description = "Description" }
};
var defaultOptions = new AddDefaultAppOptions();
config.Bind("DefaultAppOptions", defaultOptions);
defaultOptions.AuthorizationOptions = new AuthorizationOptions
{
    Issuer = config.GetWebAppAuthIssuer(),
    Audience = config.GetJwtAudience(),
    SigningKey = config.GetAuthorizationSigningKey()
};
defaultOptions.SwaggerOptions = swaggerOptions;
defaultOptions.IncludeSwagger = isDevelopment;

// If Infrastructure has its own AutoMapper profiles, register them here:
// defaultOptions.AdditionalAutoMapperTypesToRegister = [ typeof(YourInfrastructure.Mappers.MappingProfile) ];

var mvcBuilder = builder.Services.AddDefaultAppSetup<Program>(builder, defaultOptions);
mvcBuilder.AddMediRoutesLoggingController();

// 7. Register services — always in this order
builder.Services.RegisterApiServices(builder.Configuration);
builder.Services.RegisterDomainServices(builder.Configuration);
builder.Services.RegisterInfrastructureServices(builder.Configuration);
builder.Services.ValidateDIRegistrations();

// 8. Middleware
var app = builder.Build();
app.UseDefaultAppSetup(new UseDefaultAppOptions
{
    SwaggerOptions = swaggerOptions,
#if !DEBUG
    UseHttps = false,
#endif
    UseCors = true,
    CorsPolicyName = corsPolicyOptions.PolicyName,
    RegisterAdditionalMiddlewareCallback = a => app.UseUserProfileServices()
});
app.UseMediRoutesConfigReloading(app.Configuration as IConfigurationRoot);
app.UseMediRoutesLogging();
app.AddStatusEndpoint<Program>();

await app.RunAsync();
```

---

## 3. ServiceCollectionExtensions.cs

Every Api project must expose `RegisterApiServices`. Register only API-layer concerns here.

```csharp
namespace YourName.Api;

public static class ServiceCollectionExtensions
{
    public static void RegisterApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register IHydrator<T> implementations when project uses hydrators
        services.AddCollectionOfServicesFromGenericInterface(
            typeof(IHydrator<>),
            typeof(SomeHydrator).Assembly,
            ServiceLifetime.Scoped);
    }
}
```

If the API layer has no registrations, keep the method with an empty body.

---

## 4. Controllers

### Rules
- Always inherit `MediRSecureBaseController`
- Mark class as `sealed`
- File-scoped namespace
- All endpoints must have complete `[ProducesResponseType]` attributes
- XML doc comments (`<summary>`, `<param>`, `<returns>`) on every endpoint
- Validate all input before calling any service — return `ValidationProblem(ModelState)` on failure
- Never put business logic in a controller
- Always call `BuildActionResultFromErrors()` for domain/service error responses
- Use `CancellationToken cancellationToken` on write operations (POST, PUT, PATCH, DELETE)

### Standard Pattern — via Domain Service

```csharp
[Route("api/[controller]")]
public sealed class RoutesController : MediRSecureBaseController
{
    private readonly IMapper _mapper;
    private readonly ILogger<RoutesController> _logger;
    private readonly IRoutesService _routesService;

    public RoutesController(ILogger<RoutesController> logger, IRoutesService routesService, IMapper mapper)
    {
        _logger = logger;
        _routesService = routesService;
        _mapper = mapper;
    }

    /// <summary>Gets routes for a given work date.</summary>
    /// <param name="workDate">The work date to retrieve routes for.</param>
    /// <returns>List of routes.</returns>
    [HttpGet("list")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<RouteSummaryDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyCollection<RouteSummaryDto>>> GetRouteSummaryAsync(
        [FromQuery] DateTimeOffset workDate)
    {
        if (workDate == default)
        {
            ModelState.AddModelError(nameof(workDate), "Work date is required.");
            return ValidationProblem(ModelState);
        }

        var response = await _routesService.GetRoutesAsync(workDate);
        if (response.HasErrors)
        {
            return BuildActionResultFromErrors(response);
        }

        return Ok(_mapper.Map<IReadOnlyCollection<RouteSummaryDto>>(response.Result));
    }

    /// <summary>Cancels one or more trips.</summary>
    /// <param name="tripGuids">The trip GUIDs to cancel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated route with cancelled trips.</returns>
    [HttpPatch("trips/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RouteTripsResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RouteTripsResponseDto>> CancelTripsAsync(
        [FromQuery(Name = "tripGuid")] List<Guid> tripGuids,
        CancellationToken cancellationToken)
    {
        if (tripGuids is null || tripGuids.Count == 0 || tripGuids.Any(g => g == Guid.Empty))
        {
            ModelState.AddModelError(nameof(tripGuids), "At least one valid trip GUID is required.");
            return ValidationProblem(ModelState);
        }

        var response = await _service.CancelTripsAsync(tripGuids, cancellationToken);
        if (response.HasErrors)
        {
            return BuildActionResultFromErrors(response);
        }

        return Ok(_mapper.Map<RouteTripsResponseDto>(response.Result));
    }
}
```

### Pass-Through Pattern — Direct Gateway or Repository

When an endpoint is a simple pass-through (validate → single call → map → return), call the gateway or repository directly. **Do not create a domain service method that only forwards.**

```csharp
// Direct repository — when this API owns its own data store
var result = await _repository.SearchAsync<MyFilter, MyEntity>(filter, options: options);

// Direct gateway — when delegating to another platform service with no added logic
var response = await _apiGateway.CancelTripsAsync(tripGuids, cancellationToken);
```

---

## 5. DTOs

### Rules
- `sealed record` with `{ get; init; }` for all response and request DTOs
- `{ get; set; }` only when post-construction mutation is explicitly required — document why
- Group DTOs in subfolders by domain area: `Dto/Routes/`, `Dto/Searching/`, `Dto/Shared/`
- Add `[SelectableField]` attribute when the DTO participates in field selection (see section 7)
- Nullable properties on filter/request DTOs for optional fields

```csharp
// Response DTO
public sealed record ViolationResponseDto
{
    [SelectableField(sourcePath: nameof(Violation.ViolationId))]
    public int ViolationId { get; init; }

    [SelectableField(sourcePath: nameof(Violation.ViolationType))]
    public string ViolationType { get; init; }

    public double MaxOnBoardTimeMinutes { get; init; }
}

// Filter/request DTO
public sealed record SearchFilterDto
{
    public int? ViolationId { get; init; }
    public string? Status { get; init; }
}
```

---

## 6. AutoMapper Profiles

### Rules
- One `sealed class` per domain area inheriting `Profile`
- Explicit `CreateMap` for every mapping — no convention-based auto-mapping
- `ReverseMap()` only when both directions are actually needed
- Complex transformations (grouping, aggregation, enum-to-string) go in `ForMember` with resolver lambdas
- API layer profiles map **domain entities → API DTOs** (and DTOs → domain for incoming requests)
- Infrastructure mapper profiles register separately via `AdditionalAutoMapperTypesToRegister` in `Program.cs`

```csharp
namespace YourName.Api.Mappers;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Simple symmetric mapping
        CreateMap<ViolationResponseDto, Violation>().ReverseMap();
        CreateMap<SearchFilterDto, ViolationSearchFilter>().ReverseMap();

        // Complex mapping with grouping
        CreateMap<SearchResponse, SearchResponseDto>()
            .ForMember(dest => dest.Result, opt => opt.MapFrom((src, dest, destMember, context) =>
                src.Result != null
                    ? src.Result
                        .GroupBy(r => r.EntityType)
                        .Select(g => new SearchResultGroupDto
                        {
                            EntityType = g.Key,
                            Results = context.Mapper.Map<IEnumerable<SearchResultDto>>(g.OrderByDescending(r => r.Score)),
                            Count = g.Count()
                        }).OrderBy(o => o.EntityType)
                    : Enumerable.Empty<SearchResultGroupDto>()));
    }
}
```

---

## 7. SearchRequest / SearchResponse Pattern

Used for paginated, filterable, field-selectable endpoints backed by `EFCoreRepository`.

```csharp
[HttpGet("search")]
[ProducesResponseType(typeof(SearchResponseDto<ViolationResponseDto>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<SearchResponseDto<ViolationResponseDto>>> SearchAsync(
    [FromQuery] SearchRequest<SearchFilterDto> searchRequest)
{
    var filter = _mapper.Map<ViolationSearchFilter>(searchRequest.Filters ?? new SearchFilterDto());
    var options = searchRequest.ToQueryOptions<SearchFilterDto, ViolationResponseDto, Violation>(_selectableFieldsRegistry);

    if (options.HasNoMatchingSelectionFields)
    {
        ModelState.AddModelError(nameof(searchRequest.Select), "None of the requested select fields are recognized.");
        return ValidationProblem(ModelState);
    }

    var result = await _repository.SearchAsync<ViolationSearchFilter, Violation>(filter, options: options);
    if (result == null)
    {
        return NotFound("No results found.");
    }

    if (result.HasProjection)
    {
        return Ok(new SearchResponseDto<ViolationResponseDto>
        {
            ProjectedItems = JsonConvert.SerializeObject(result.ProjectedItems),
            TotalCount = result.TotalCount,
            CurrentPage = result.CurrentPage,
            TotalPages = result.TotalPages
        });
    }

    return Ok(new SearchResponseDto<ViolationResponseDto>
    {
        Items = _mapper.Map<IReadOnlyCollection<ViolationResponseDto>>(result.Items).ToList(),
        TotalCount = result.TotalCount,
        CurrentPage = result.CurrentPage,
        TotalPages = result.TotalPages
    });
}
```

Mark DTO properties with `[SelectableField]` to opt into partial field selection:
```csharp
[SelectableField(sourcePath: nameof(DomainEntity.PropertyName))]
public string PropertyName { get; init; }
```

`ISelectableFieldsRegistry` is registered automatically by `AddDefaultAppSetup` — no manual registration needed.

---

## 8. Hydrator Pattern

Used for optional post-mapping enrichment selected by include-type string tokens.

### Applying hydration in a controller

```csharp
private readonly IEnumerable<IHydrator<List<MyResponseDto>>> _hydrators;

// After mapping:
var result = _mapper.Map<List<MyResponseDto>>(response.Result) ?? [];
if (result.Any())
{
    result = await _hydrators.HydrateAsync(result, [Constants.HydratorIncludeTypes.MY_TYPE])
        .ConfigureAwait(false) ?? result;
}
return Ok(result);
```

### Implementing a Hydrator

```csharp
namespace YourName.Api.Shared.Hydrators;

public sealed class MyHydrator : IHydrator<List<MyResponseDto>>
{
    public string Name => Constants.HydratorIncludeTypes.MY_TYPE;

    private readonly ISomeGateway _gateway;

    public MyHydrator(ISomeGateway gateway)
    {
        _gateway = gateway;
    }

    public async Task<List<MyResponseDto>?> HydrateAsync(List<MyResponseDto> items, string[] includeTypes)
    {
        if (!includeTypes.Contains(Name))
        {
            return items;
        }

        // enrich items from gateway
        return items;
    }
}
```

Register all hydrators via `AddCollectionOfServicesFromGenericInterface` in `RegisterApiServices`.

---

## 9. New Endpoint Checklist

1. **DTO** — Create in `Dto/<Area>/`. Use `sealed record` + `{ get; init; }`. Add `[SelectableField]` if using SearchRequest pattern.
2. **Mapping** — Add `CreateMap<>` entries in the appropriate `MappingProfile`.
3. **Controller action** — Inherit `MediRSecureBaseController`, `sealed`, all `ProducesResponseType`, XML docs, explicit input validation.
4. **Domain service** — Create/update only when there is real business logic (multiple gateway calls, derived values, business rules). Never for pure pass-through.
5. **DI** — Register any new services, hydrators, or registries in `ServiceCollectionExtensions.RegisterApiServices`.
6. **Tests** — Create or update tests in `*.Api.Tests/` for the new action and any new service methods.

---

## Related Resources

| Resource | Path | Load When |
|----------|------|-----------|
| Domain Skill | `skills/domain-developer/SKILL.md` | Creating/modifying domain entities, services, or interfaces |
| Infrastructure Skill | `skills/infrastructure-developer/SKILL.md` | Creating/modifying gateways, proxies, or repositories |
| Architecture Instructions | `instructions/architecture.instructions.md` | Always — coding standards and naming conventions |
| Testing Instructions | `instructions/testing.instructions.md` | Writing or reviewing tests |
| SQL Metadata Skill | `skills/sql-metadata/SKILL.md` | Need to inspect database schema before generating entities |
