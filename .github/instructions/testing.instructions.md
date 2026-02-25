---
applyTo: "**/*Tests.cs,**/*Fixture.cs,**/*Tests/**/*.cs"
---

# MediRoutes Platform — Testing Standards

## Frameworks
- **xUnit** — test runner
- **Shouldly** — assertions (never FluentAssertions)
- **Moq** — mocking
- **EF Core InMemory** — in-process database testing

## Test Project Placement

| Production Layer | Test Project |
|---|---|
| API controllers/mappers | `*.Api.Tests/` |
| Domain services | `*.Domain.Tests/` |
| Infrastructure gateways/repos | `*.Infrastructure.Tests/` |
| Application services | `*.ApplicationServices.Tests/` |

## BaseTestFixture — Always Read First
Before writing tests, **read `Infrastructure/MediR.Testing/BaseTestFixture.cs`**. It provides factory methods for:
- AutoMapper, UserContext, InMemory/Mock DbContexts
- MemoryCache, DistributedCache
- Mock/real IConfiguration
- HttpClient with mocked handlers
- HttpContextAccessor, ClaimsPrincipal
- LoggerBuilder with verification
- ServiceProvider, ServiceCollection

All test fixtures must inherit from `BaseTestFixture`.

## Fixture Pattern

**Hard Rules:**
- Fixture class and test class must be in **separate files**.
- Helper DTOs/enums used only by one test suite belong as **nested types inside the fixture** — no extra standalone helper files.
- Test objects should be created via builder/helper methods in the fixture — not as ad-hoc private static methods in the test class.

**ExampleTestFixture.cs:**
```csharp
using MediR.Testing;

namespace Example.Tests;

public sealed class ExampleTestFixture : BaseTestFixture
{
    public SomeEntity CreateSomeEntity()
    {
        return new SomeEntity { /* minimal valid defaults */ };
    }
}
```

**ExampleTests.cs:**
```csharp
namespace Example.Tests;

public sealed class ExampleTests : IClassFixture<ExampleTestFixture>
{
    private readonly ExampleTestFixture _fixture;
    private readonly SomeService _service;

    public ExampleTests(ExampleTestFixture fixture)
    {
        _fixture = fixture;
        _service = new SomeService(/* deps */);
    }

    [Fact]
    public async Task GivenValidInput_WhenDoThingAsync_ThenShouldSucceedAsync()
    {
        // Arrange

        // Act
        var result = await _service.DoThingAsync();

        // Assert
        result.ShouldNotBeNull();
    }
}
```

## Test Naming
Always: `Given[Precondition]_When[Action]_Then[ExpectedOutcome]Async`

Examples:
- `GivenMissingId_WhenGetByIdAsync_ThenReturnsBadRequestAsync`
- `GivenValidRequest_WhenHandleAsync_ThenReturnsResultAsync`

## Shouldly Assertions
- `result.ShouldBe(expected)`
- `result.ShouldBeOfType<T>()`
- `collection.ShouldBeEmpty()` / `collection.ShouldNotBeEmpty()`
- `value.ShouldNotBeNull()`

Read `MediR.Testing/Extensions/ShouldlyExtensions.cs` for custom extensions before writing assertion logic.

## Mocking (Moq)
- Mock external systems only: HTTP clients, SDK clients, queues, clocks.
- Do not mock simple value objects or in-process domain logic.
- Verify interactions only when the interaction is part of the contract.

```csharp
var mock = new Mock<ISomeDependency>();
mock.Setup(x => x.CallAsync(It.IsAny<string>())).ReturnsAsync(value);
mock.Verify(x => x.CallAsync("expected"), Times.Once);
```

### Mock IConfiguration
```csharp
var config = fixture.GetConfiguration();
config.AddItemToConfiguration("MySection:MyKey", "some-value");
```

## EF Core InMemory Pattern
```csharp
var options = fixture.CreateInMemoryDbContextOptions<MyDbContext>();
var userContext = fixture.CreateUserContext();

// Seed with one context
await using (var seedDb = new MyDbContext(userContext, options))
{
    await seedDb.Entities.AddAsync(new MyEntity { /* ... */ });
    await seedDb.SaveChangesAsync();
}

// Act with a fresh context — never reuse seed context
await using var db = new MyDbContext(userContext, options);
var result = await repository.GetAsync(/* ... */);
result.ShouldNotBeNull();
```

Use InMemory when: EF query translation, navigation properties, or database-generated values matter.
Use Mock DbContext (`SetupDbSet`) when: simple Add/Remove/query behavior without real EF is sufficient.

## Controller Testing
- Build controller with real mapper profiles via `BaseTestFixture.CreateMapper`.
- Use `MediR.Testing.Builders.ControllerBuilder` to create realistic `HttpContext`.

```csharp
var controller = new MyController(mapper, serviceMock.Object);
var builderResult = new ControllerBuilder(controller)
    .WithContext()
    .WithUserAuthenticated()
    .Build();
var result = await controller.GetAsync(id);
result.Result.ShouldBeOfType<OkObjectResult>();
```

## Logger Verification
```csharp
var (logBuilder, mockLogger) = fixture.GetLogBuilder<MyService>();
var service = new MyService(mockLogger.Object);
await service.DoWorkAsync();
logBuilder.Verify(Times.Once(), v => v
    .HasLogLevel(LogLevel.Error)
    .ErrorMessageContains("failed"));
```

## HttpClient Testing
```csharp
var (httpClient, mockHandler) = new HttpClientBuilder()
    .WithRequest(HttpMethod.Get, "https://api.example.com/data")
    .WithResponse(HttpStatusCode.OK, new { Id = 1, Name = "Test" })
    .Build();
var service = new MyProxy(httpClient);
var result = await service.GetDataAsync();
result.ShouldNotBeNull();
```

## Minimum Coverage Expectations
- Guard clauses / invalid input
- Happy path
- Dependency error propagation (service returns errors)
- Side effects (DB changes, DTO mapping, etc.)

## Hard Rules
- **Never modify production code to satisfy a test** without explicit user approval.
- If a production change is needed, stop, explain what and why, and get approval first.
- Test class must be `sealed` (leaf class).
- Arrange / Act / Assert structure always.
