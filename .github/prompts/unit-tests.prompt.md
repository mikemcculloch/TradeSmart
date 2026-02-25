---
description: This prompt is used to create unit tests for a .NET 8+ application using Onion Architecture, xUnit, Entity Framework Core InMemory, and a custom StepFlow framework. It provides detailed instructions on how to inspect existing context, request missing context, and follow established patterns for writing effective and maintainable tests.
name: unit-tests
agent: agent    
---
# Unit Testing Patterns Prompt

## Context
You are helping to create **unit tests** for a .NET 8+ application that follows Onion Architecture. The solution primarily uses:
- **xUnit** for tests
- **Shouldly** for assertions
- **Moq** for mocking external dependencies
- **Entity Framework Core InMemory** when testing repository/data-access behavior

## Goals
- Keep tests deterministic, isolated, and readable.
- Prefer testing behavior (outputs + side-effects) over implementation details.
- Mock only what is truly external to the unit under test.
- Do not change production code (e.g., add AutoMapper mappings/profiles) solely to satisfy a unit test; prefer configuring the mapper/test harness appropriately.

### Hard rule: tests must not change production code
- When the user asks for unit tests, make changes **only** in test projects/files by default.
- Do **not** modify non-test (production) code unless the user explicitly asks you to.
- If a production change seems necessary (e.g., a real bug uncovered by tests, nullability warnings treated as errors, incorrect behavior), you must:
	1) Stop,
	2) Explain the exact production change you believe is needed and why,
	3) Ask for explicit approval before making any production edits.

**Approval requirement:** No approval → no production edits.

## Workflow (Start With Current Context)
- First, inspect what’s already in the agent/editor context (current file, open tabs, selections, and attached files) to understand the unit under test and existing patterns.
- Only after that, widen the search to other folders/projects (e.g., related DTOs, profiles, helpers) as needed.

### If tests fail
- Prefer fixing the test (data setup, fixture, mocks) over changing production code.
- If the failure indicates a production bug, follow the “Hard rule” above and request approval.

## Missing Context (Stop And Ask)
- If the unit under test (or related DTOs/mappers/interfaces) is not available in the current context (no relevant open files, selections, attachments, or pasted code), do **not** guess.
- Instead, ask the user to provide one of:
	- Attach the relevant files, or
	- Open the relevant files in the editor, or
	- Paste the key code as text into the chat.
- Only proceed to writing tests once enough concrete code context is provided to compile and follow repo conventions.

## Coding Standards (Project Conventions)
- PascalCase for classes and methods.
- `_` prefix for private fields.
- Async methods must use the `Async` suffix.
- Arrange/Act/Assert structure.

## Test Naming
Always use descriptive method names that encode scenario and expectation:
- `Given[Precondition]_When[Action]_Then[ExpectedOutcome]Async`

Examples:
- `GivenMissingId_WhenGetByIdAsync_ThenReturnsBadRequestAsync`
- `GivenValidRequest_WhenHandleAsync_ThenReturnsResultAsync`

## Shouldly Assertions
Always use Shouldly for clarity and better failure messages:
- `result.ShouldBe(expected)`
- `result.ShouldBeOfType<T>()`
- `collection.ShouldBeEmpty()` / `collection.ShouldNotBeEmpty()`
- `value.ShouldNotBeNull()`

## Fixture Pattern
Always use fixtures for shared setup and helpers:
- Use `IClassFixture<TFixture>` for per-class shared setup.
- Put entity builders and reusable helpers in the fixture.
- **CRITICAL**: Test fixtures must be in separate files from test classes.
	- The test class and its fixture should each be in their own `.cs` file.
	- Helper DTOs/enums used only by that test suite should be declared as **nested types inside the fixture** (to avoid creating extra standalone helper files).
	- Do not create additional helper `.cs` files like `*TestDto.cs` / `*IncludeType.cs` unless explicitly requested.
	- Exception: if a helper type is shared across multiple test suites, discuss/confirm a shared location before adding new files.

Additional convention:
- Test objects (entities/DTOs) should be created in the class test fixture via builder/helper methods (not as ad-hoc private static methods in the test class).

Always inherit the fixture from `Infrastructure/MediR.Testing/BaseTestFixture.cs` to reuse:
- `GetServiceProvider()`
- `CreateMapper(...)`
- `CreateUserContext()`
- `CreateInMemoryDbContextOptions<TContext>()`

### Template

**ExampleTestFixture.cs**:
```csharp
using MediR.Testing;

namespace Example.Tests;

public sealed class ExampleTestFixture : BaseTestFixture
{
	public SomeEntity CreateSomeEntity(/* params */)
	{
		return new SomeEntity
		{
			// minimal valid defaults
		};
	}
}
```

**ExampleTests.cs**:
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
	public async Task DoThingAsync_HappyPath_ShouldSucceedAsync()
	{
		// Arrange

		// Act
		var result = await _service.DoThingAsync();

		// Assert
		result.ShouldNotBeNull();
	}
}
```

## Mocking Guidelines (Moq)
- Mock external systems: HTTP clients, SDK clients, queues, clocks, random generators, etc.
- Do not mock simple value objects or in-process domain logic.
- Verify interactions only when the interaction is part of the contract.

Typical patterns:
- `var mock = new Mock<ISomeDependency>();`
- `mock.Setup(x => x.CallAsync(It.IsAny<string>())).ReturnsAsync(value);`
- `mock.Verify(x => x.CallAsync("expected"), Times.Once);`

## EF Core InMemory Pattern
Use EF Core InMemory when the behavior depends on EF queries/changes:
```csharp
var options = fixture.CreateInMemoryDbContextOptions<MyDbContext>();
var userContext = fixture.CreateUserContext();

await using var db = new MyDbContext(userContext, options);

// Arrange: seed
await db.Entities.AddAsync(new MyEntity { /* ... */ });
await db.SaveChangesAsync();

db.ChangeTracker.Clear();

// Act
var result = await repository.GetAsync(/* ... */);

// Assert
result.ShouldNotBeNull();
```

Notes:
- Use a fresh InMemory database per test class or per test when necessary.
- Call `db.ChangeTracker.Clear()` before re-querying to avoid false positives from tracked entities.

## Controller Unit Testing Pattern (API)
- Build the controller with real mapper profiles when possible (via `BaseTestFixture.CreateMapper`).
- Use `MediR.Testing.Builders.ControllerBuilder` to create a realistic `HttpContext`.
- Validate action results:
  - `OkObjectResult`, `BadRequestObjectResult`, `NotFoundObjectResult`, `ObjectResult`

Example:
```csharp
var controller = new MyController(mapper, serviceMock.Object);
new ControllerBuilder(controller).WithContext().Build();

var result = await controller.GetAsync(id);
result.Result.ShouldBeOfType<OkObjectResult>();
```

## What To Cover
Minimum coverage expectations for most units:
- Guard clauses / invalid input
- Happy path
- Dependency error propagation (service returns errors)
- Side effects (DB changes, returned DTO mapping, etc.)

## Keep Tests Maintainable
- Use builders/helpers to avoid repeating object setup.
- Prefer asserting on meaningful fields instead of entire object graphs.
- Avoid fragile assertions that depend on ordering unless ordering is part of the requirement.
