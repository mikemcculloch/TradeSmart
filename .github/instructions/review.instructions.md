---
description: 'Code Review Guidelines for .NET 8+ MediRoutes Platform with Onion Architecture'
applyTo: "**/*.cs"
---

# .NET Development Guidelines for MediRoutes Platform

> These instructions target .NET 8+ projects following Onion Architecture principles. Rules are prioritized for automated review tools and GitHub Copilot. C# code should be written to maximize readability, maintainability, and correctness while minimizing complexity and coupling. Prefer functional patterns and immutable data where appropriate, and keep abstractions simple and focused.

# Role Definition:

- C# Language Expert
- Software Architect
- Code Quality Specialist

## Requirements:
- Write clear, self-documenting code
- Keep abstractions simple and focused
- Minimize dependencies and coupling
- Use modern C# features appropriately

## Critical Violations (🚨 Block PR)

### Security & Safety
- ❌ **NEVER** hardcode connection strings, API keys, or secrets in code. Azure Config should be used and recommended.
- ❌ **NEVER** use string concatenation for SQL queries  
- ❌ **NEVER** log sensitive data (passwords, tokens, PII, connection strings)
- ❌ **NEVER** expose domain entities directly in API responses
- ❌ **NEVER** use `ConfigurationManager.AppSettings` or direct config access. Always use exposed extension methods from the Domain project

### Architecture Violations
```csharp
// ❌ FORBIDDEN: Domain depending on Infrastructure
namespace MyApp.Domain
{
    using MyApp.Infrastructure; // VIOLATION
}

// ❌ FORBIDDEN: Controllers with business logic
public class OrderController : ControllerBase
{
    public IActionResult CreateOrder()
    {
        // Business logic here - VIOLATION
        if (order.Total > 1000) // Business rule in controller
    }
}

// ❌ FORBIDDEN: Returning domain entities from controllers
public IActionResult Get() => Ok(domainEntity); // VIOLATION

// ✅ REQUIRED: Return DTOs from controllers  
public IActionResult Get() => Ok(_mapper.Map<EntityDto>(domainEntity));
```

## High Priority Violations (⚠️ Must Fix)

### Controller Requirements
```csharp
// ✅ REQUIRED: All controllers must inherit MediRSecureBaseController
public class SearchController : MediRSecureBaseController // CORRECT

// ❌ VIOLATION: Generic controller base
public class SearchController : ControllerBase // WRONG

// ✅ REQUIRED: Comprehensive response type attributes
[HttpGet]
[ProducesResponseType(typeof(SearchDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<SearchDto>> SearchAsync() // CORRECT

// ❌ VIOLATION: Missing ProducesResponseType attributes
[HttpGet]
public async Task<ActionResult<SearchDto>> SearchAsync() // INCOMPLETE
```

### Repository Requirements
```csharp
// ✅ REQUIRED: Repositories must inherit EFCoreRepository
public class SearchRepository : EFCoreRepository<SearchContext, SearchEntity, Search> // CORRECT

// ❌ VIOLATION: Direct DbContext usage in domain/app services
public class SearchService 
{
    private readonly DbContext _context; // WRONG - should use repository
}
```

### Domain Service Response Pattern
```csharp
// ✅ REQUIRED: Domain services return BaseResponse<T>
public async Task<BaseResponse<Search>> CreateSearchAsync(Search search)
{
    if (!IsValid(search))
        return BaseResponse.CreateErrorResponse<BaseResponse<Search>>("Invalid search");
    
    return new BaseResponse<Search> { Result = search };
}

// ❌ VIOLATION: Throwing exceptions for business rule violations
public async Task<Search> CreateSearchAsync(Search search)
{
    if (!IsValid(search))
        throw new ArgumentException("Invalid search"); // WRONG
}
```

## Medium Priority (Code Quality)

### Naming Conventions
```csharp
// ✅ CORRECT Naming
public class SearchService : ISearchService           // PascalCase classes/interfaces
{
    private readonly ILogger _logger;                 // _camelCase private fields
    private const int MAX_RESULTS = 100;              // UPPER_SNAKE_CASE constants
    
    public async Task<SearchResult> SearchAsync()     // Async suffix required
}

// ❌ VIOLATIONS
public class searchService : isearchService           // Wrong casing
{
    private readonly ILogger logger;                  // Missing underscore
    private const int maxResults = 100;               // Wrong constant casing
    
    public async Task<SearchResult> Search()          // Missing Async suffix
}
```

### File & Namespace Organization
```csharp
// ✅ REQUIRED: File-scoped namespaces (.NET 8+)
namespace Search.Domain.Entities;

public class SearchOptions { }

// ❌ VIOLATION: Traditional namespace blocks
namespace Search.Domain.Entities
{
    public class SearchOptions { }
}
```

### Modern C# Features (.NET 8+)
```csharp
// ✅ PREFERRED: Modern C# patterns
public sealed record SearchDto(
    int Id,
    required string Query,
    EntityType Type
)
{
    public List<string> Tags { get; init; } = [];
}

// ✅ PREFERRED: Standard constructors. Due to the fact that primary constructors expose the fields as public properties.
public class SearchService
{
    public SearchService(ILogger<SearchService> logger, ISearchRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }
    public async Task<SearchResult> SearchAsync(string query) =>
        await _repository.SearchAsync(query);
}

// ✅ NOT PREFERRED: Primary constructors
public class SearchService(ILogger<SearchService> logger, ISearchRepository repository)
{
    public async Task<SearchResult> SearchAsync(string query) =>
        await repository.SearchAsync(query);
}
```

## Async/Performance Requirements

### Async Patterns
```csharp
// ✅ REQUIRED: Proper async patterns. 
// ✅ REQUIRED: Methods must be async all the way. 
// ✅ REQUIRED: Method names must have Async suffix.
public async Task<SearchResult> SearchAsync(CancellationToken cancellationToken = default)
{
    return await _repository.SearchAsync(query).ConfigureAwait(false);
}

// ❌ VIOLATIONS: Blocking async calls
public SearchResult Search()
{
    return SearchAsync().Result;           // WRONG - blocking
    return SearchAsync().GetAwaiter().GetResult(); // WRONG - blocking
}
```

### Memory & Performance
```csharp
// ✅ PREFERRED: Efficient collections (.NET 8+)
private static readonly FrozenDictionary<string, EntityType> _typeMap = 
    new Dictionary<string, EntityType>().ToFrozenDictionary();

// ✅ REQUIRED: IDisposable handling
public async Task ProcessAsync()
{
    using var scope = _serviceProvider.CreateScope();
    await ProcessWithScopeAsync(scope);
} // Proper disposal
```

## Architecture-Specific Rules

### Onion Architecture Layers

#### Domain Layer (`*.Domain` projects)
```csharp
// ✅ ALLOWED: Domain dependencies only
using System.*;
using MediR.Models;              // Base models
using MyApp.Domain.Entities;     // Same domain

// ❌ FORBIDDEN: External dependencies
using Microsoft.EntityFrameworkCore;  // Infrastructure concern
using Microsoft.AspNetCore.*;         // Web concern
using MyApp.Infrastructure.*;          // Layer violation
```

#### Application Services (`*.ApplicationServices` projects)
```csharp
// ✅ REQUIRED: Application service pattern
// ✅ REQUIRED: Application services return BaseResponse<T>
public class SearchApplicationService : ISearchApplicationService
{
    public async Task<BaseResponse<SearchDto>> SearchAsync(SearchRequestDto request)
    {
        // 1. Map DTO to domain
        var domainRequest = _mapper.Map<SearchRequest>(request);
        
        // 2. Call domain service
        var domainResult = await _searchDomainService.SearchAsync(domainRequest);
        
        // 3. Handle domain response
        if (domainResult.HasErrors)
            return _mapper.Map<BaseResponse<SearchDto>>(domainResult);
        
        // 4. Return mapped result
        return new BaseResponse<SearchDto> 
        { 
            Result = _mapper.Map<SearchDto>(domainResult.Result) 
        };
    }
}
```

#### Infrastructure Layer (`*.Infrastructure` projects)
```csharp
// ✅ REQUIRED: Proxy pattern for internal cross platform services
// ✅ REQUIRED: Proxies return BaseResponse<T>
public class AzureSearchProxy : ISearchProxy
{
    private readonly SearchServiceClient _searchClient;
    
    public async Task<BaseResponse<SearchResult>> SearchAsync(string query)
    {
        try 
        {
            var azureResult = await _searchClient.Documents.SearchAsync(query);
            var mapped = _mapper.Map<SearchResult>(azureResult); // Transform to domain
            return new BaseResponse<SearchResult> { Result = mapped };
        }
        catch (CloudException ex)
        {
            _logger.LogError(ex, "Azure search failed for query: {Query}", query);
            return BaseResponse.CreateErrorResponse<SearchResult>("Search service unavailable", ex);
        }
    }
}
```

```csharp
// ✅ REQUIRED: Gateway pattern for internal cross platform services
// ✅ REQUIRED: Gateways return BaseResponse<T>
public class RouteApiGateway : IRouteApiGateway
{
    private readonly RouteApiClient _routeClient;

    public async Task<BaseResponse<Route>> SearchAsync(string id)
    {
        try 
        {
            var routeResult = await _routeClient.GetAsync(id);
            var mapped = _mapper.Map<Route>(routeResult); // Transform to domain
            return new BaseResponse<Route> { Result = mapped };
        }
        catch (CloudException ex)
        {
            _logger.LogError(ex, "Unable to retrieve route with ID: {Id}", id);
            return BaseResponse.CreateErrorResponse<Route>("Route service unavailable", ex);
        }
    }
}
```

### AutoMapper Configurations
```csharp
// ✅ REQUIRED: Explicit mapping profiles
public class SearchMappingProfile : Profile
{
    public SearchMappingProfile()
    {
        CreateMap<SearchEntity, SearchDto>()
            .ForMember(dest => dest.TypeName, opt => opt.MapFrom(src => src.Type.ToString()))
            .ReverseMap()
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()); // Don't map back
    }
}

// ❌ VIOLATION: Auto-mapping without explicit configuration
// Mapper.Map<SearchDto>(entity); // Without profile - UNSAFE
```

## Testing Requirements

### Unit Test Standards

```csharp
// ✅ REQUIRED: Use IClassFixture and the MediR.Testing\MediR.Testing.csproj
public class SearchServiceTests : IClassFixture<BaseTestFixture>
{
    public SearchServiceTests(BaseTestFixture fixture)
    {
        _fixture = fixture;
        _mockSearchRepository = new Mock<ISearchRepository>();
         (_loggerBuilder, _logger) = _fixture.GetLogBuilder<AzureSearchProxy>();
        
        // Initialize service under test with mocked dependencies
        _searchService = new SearchService(
            _logger.Object,
            _mockSearchRepository.Object
        );
    }
}

```

```csharp
// ✅ REQUIRED: Descriptive test names using Given_When_Then pattern
[Fact]
public async Task SearchAsync_WithValidQuery_ReturnsExpectedResults()
{
    // Arrange
    var query = "test search";
    var expectedCount = 5;
    
    // Act  
    var result = await _searchService.SearchAsync(query);
    
    // Assert
    result.Should().NotBeNull();
    result.Results.Should().HaveCount(expectedCount);
}

// ❌ VIOLATION: Poor test naming
[Fact] 
public async Task Test1() // Non-descriptive name
```

### Test Coverage Requirements
- ✅ **REQUIRED**: Minimum 80% code coverage for all business logic
- ✅ **REQUIRED**: All domain services must have comprehensive unit tests
- ✅ **REQUIRED**: All controllers must have integration tests

## Error Handling Patterns

### Service Layer Error Handling
```csharp
// ✅ REQUIRED: Structured error handling with BaseResponse
public async Task<BaseResponse<SearchResult>> SearchAsync(string query)
{
    try
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BaseResponse.CreateBadRequestError(new Dictionary<string, string[]> { { nameof(query), new[] { "query can not be empty" } } });
        }
        
        var result = await _searchRepository.SearchAsync(query);
        return new BaseResponse<SearchResult> { Result = result };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Search failed for query: {Query}", query);
        return BaseResponse.CreateErrorResponse<BaseResponse<SearchResult>>(new[] { new Error { Message = "Service error" } });
    }
}

// ❌ VIOLATION: Unhandled exceptions bubbling up
public async Task<SearchResult> SearchAsync(string query)
{
    // This can throw - WRONG
    return await _externalService.SearchAsync(query);
}
```

## Logging Standards

### Structured Logging
```csharp
// ✅ REQUIRED: Structured logging with parameters
_logger.LogInformation("Search completed for query {Query} with {ResultCount} results", 
    query, results.Count);

// ✅ REQUIRED: Use LogWithGuard when log level is lower then Warning
_logger.LogWithGuard(LogLevel.Debug, "Detailed debug info for query {query} with parameters {parameters}", query, string.Join(",", parameters));

// ✅ REQUIRED: Appropriate log levels
_logger.LogDebug("Processing search request");          // Verbose info
_logger.LogInformation("Search completed successfully"); // Important events  
_logger.LogWarning("Search returned no results");       // Unexpected but not error
_logger.LogError(ex, "Search service unavailable");     // Actual errors

// ❌ VIOLATIONS
_logger.LogInformation($"Search for {query}");          // String interpolation
_logger.LogError("Something went wrong");               // No context
Console.WriteLine("Debug info");                        // Console usage
```

## Configuration Management

### Configuration Extensions Pattern
```csharp
// ✅ REQUIRED: Configuration extensions in Domain projects
public static class ConfigurationExtensions
{
    public static string GetAzureSearchEndpoint(this IConfiguration configuration) =>
        configuration["AzureSearch:Endpoint"] ?? 
        throw new InvalidOperationException("AzureSearch:Endpoint not configured");
    
    public static TimeSpan GetSearchTimeout(this IConfiguration configuration) =>
        TimeSpan.FromSeconds(configuration.GetValue<int>("Search:TimeoutSeconds", 30));
}

// ❌ VIOLATION: Direct configuration access in services
public class SearchService
{
    public SearchService(IConfiguration config)
    {
        var endpoint = config["AzureSearch:Endpoint"]; // WRONG - use extensions
    }
}
```

## Dependency Injection Patterns

### Service Registration
```csharp
// ✅ REQUIRED: Proper service lifetimes
services.AddScoped<ISearchService, SearchService>();     // Per request
services.AddSingleton<ISearchCache, SearchCache>();      // Application lifetime
services.AddTransient<ISearchValidator, SearchValidator>(); // Per use

// ✅ REQUIRED: Validation in DEBUG builds
#if DEBUG
services.ValidateDIRegistrations(); // Custom extension method
#endif
```

## Code Review Checklist

Before submitting PR, verify:

### Critical Items
- [ ] No hardcoded secrets or connection strings: **Target 100%**
- [ ] All controllers inherit from `MediRSecureBaseController`: **Target 100%**
- [ ] All async methods have `Async` suffix: **Target 100%**
- [ ] No domain entities exposed in API responses: **Target 100%**
- [ ] All repositories inherit from `EFCoreRepository<,,>`: **Target 100%**

### Architecture Compliance  
- [ ] Domain layer has no infrastructure dependencies
- [ ] Domain services return `BaseResponse<T>`: **Target 100%**
- [ ] Application services coordinate and map DTOs
- [ ] Infrastructure uses Proxy pattern for external services
- [ ] Infrastructure uses Gateway pattern for internal platform services

### Quality Standards
- [ ] Private fields use `_camelCase` naming: **Target 100%**
- [ ] Constants use `UPPER_SNAKE_CASE`: **Target 100%**
- [ ] File-scoped namespaces used: **Target 100%**
- [ ] Comprehensive `ProducesResponseType` attributes: **Target 100%**
- [ ] Structured logging with parameters: **Target 100%**

### Testing
- [ ] Unit tests for all business logic: **Target 100%**
- [ ] Test coverage ≥ 80%
- [ ] Descriptive test method names
- [ ] Integration tests for controllers


## 📋 Specific Review Checklist by Layer

### **API Layer Reviews**

#### Controllers
- [ ] **Inheritance**: All inherit from `MediRSecureBaseController`
- [ ] **Response Types**: Complete `ProducesResponseType` attributes on ALL endpoints
- [ ] **DTO Usage**: Never return domain entities directly
- [ ] **Input Validation**: Proper ModelState validation before processing
- [ ] **Error Handling**: Use `BuildActionResultFromErrors()` consistently

```csharp
// ✅ CORRECT Pattern Found in DriversController
[HttpGet("{driverId:int}")]
[ProducesResponseType(typeof(DriverResponseDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<DriverResponseDto>> GetDriverByIdAsync([FromRoute] int driverId)
{
    if (driverId == 0)
    {
        ModelState.AddModelError(nameof(driverId), "Driver ID is required and must be greater than 0.");
        return ValidationProblem(ModelState);
    }

    var driver = await _driverService.GetDriverByIdAsync(driverId);
    if (driver.HasErrors)
    {
        return BuildActionResultFromErrors(driver);
    }

    var driverResponseDto = _mapper.Map<DriverResponseDto>(driver.Result);
    return Ok(driverResponseDto);
}
```

### **Domain Layer Reviews**

#### Business Logic Encapsulation
- [ ] **Pure Domain Logic**: No infrastructure concerns (HTTP, database, etc.)
- [ ] **BaseResponse Usage**: All services return `BaseResponse<T>`
- [ ] **Exception Handling**: Business rule violations return errors, don't throw exceptions
- [ ] **Configuration Access**: Only via Domain-specific extension methods

```csharp
// ✅ CORRECT: Domain service pattern
public async Task<BaseResponse<Route>> CreateRouteAsync(Route route)
{
    if (!IsValidRoute(route))
    {
        return BaseResponse.CreateErrorResponse<BaseResponse<Route>>("Invalid route data");
    }
    
    // Business logic here
    return new BaseResponse<Route> { Result = processedRoute };
}

// ❌ WRONG: Throwing exceptions for business rules
public async Task<Route> CreateRouteAsync(Route route)
{
    if (!IsValidRoute(route))
        throw new ArgumentException("Invalid route"); // BUSINESS RULE VIOLATION
}
```

#### Configuration Extensions Pattern
- [ ] **Centralized Access**: Configuration extensions in Domain projects only
- [ ] **Typed Access**: Strong typing with validation
- [ ] **Error Handling**: Meaningful errors for missing config

```csharp
// ✅ FOUND GOOD PATTERN: Domain configuration extensions
public static string GetEFormanConnectionString(this IConfiguration configuration) =>
    configuration.GetConnectionString("EForman") ?? 
    throw new InvalidOperationException("EForman connection string not configured");
```

### **Infrastructure Layer Reviews**

#### Repository Patterns
- [ ] **Base Inheritance**: All inherit from `EFCoreRepository<TContext, TDbEntity, TDomainEntity>`
- [ ] **Mapping**: AutoMapper for entity-to-domain transformations
- [ ] **Error Handling**: Comprehensive logging and graceful failures

```csharp
// ✅ CORRECT Pattern Found
public class RouteRepository : EFCoreRepository<RouteMultiTenantContext, Databases.Entities.Route, DomainRoute>, IRouteRepository
{
    public RouteRepository(RouteMultiTenantContext dbContext, 
        ILogger<EFCoreRepository<RouteMultiTenantContext, Databases.Entities.Route, DomainRoute>> logger, 
        IMapper mapper, 
        IFilterProfile filterProfile, 
        IMemoryCache memoryCache) 
        : base(dbContext, logger, mapper, filterProfile, memoryCache)
    {
    }
}
```

### **Testing Reviews**

#### Test Structure & Patterns
- [ ] **BaseTestFixture Usage**: All test classes use `IClassFixture<BaseTestFixture>`
- [ ] **Descriptive Naming**: Tests use Given_When_Then pattern
- [ ] **Comprehensive Coverage**: Domain logic ≥80% coverage
- [ ] **Proper Mocking**: Repository and external service mocking

```csharp
// ✅ GOOD PATTERN FOUND in RouteRepositoryTests
public class RouteRepositoryTests : IClassFixture<BaseTestFixture>, IDisposable
{
    [Fact]
    public async Task GetByIdAsync_WithExistingRoute_ReturnsRoute()
    {
        // Arrange
        var domainRoute = CreateDomainRoute();
        var created = await _repository.CreateAsync(domainRoute);

        // Act
        var result = await _repository.GetByIdAsync(created!.RouteId);

        // Assert
        result.Should().NotBeNull();
        result!.RouteId.Should().Be(created.RouteId);
    }
}
```


### **Naming Convention Enforcement**
```csharp
// ✅ CORRECT Examples Found
private readonly ILogger _logger;                 // _camelCase private fields
private const int MAX_RESULTS = 100;              // UPPER_SNAKE_CASE constants
public async Task<SearchResult> SearchAsync()     // Async suffix required

// ❌ VIOLATIONS to Watch For
private readonly ILogger logger;                  // Missing underscore
private const int maxResults = 100;               // Wrong constant casing
public async Task<SearchResult> Search()          // Missing Async suffix
```

### **Async/Performance Patterns**
```csharp
// ✅ ENFORCE: ConfigureAwait in library code
return await _repository.SearchAsync(query).ConfigureAwait(false);

// ✅ ENFORCE: CancellationToken support
public async Task<SearchResult> SearchAsync(CancellationToken cancellationToken = default)

// ❌ BLOCK: Blocking async calls
return SearchAsync().Result;           // VIOLATION
return SearchAsync().GetAwaiter().GetResult(); // VIOLATION
```
