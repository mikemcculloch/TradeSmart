---
description: This prompt is used to create unit tests for StepFlow Process steps in a .NET 8+ application using Onion Architecture, xUnit, Entity Framework Core InMemory, and a custom StepFlow framework. It provides detailed instructions on how to inspect existing context, request missing context, and follow established patterns for writing effective and maintainable tests.
name: stepflow-tests
agent: agent    
---
# StepFlow Testing Patterns Prompt

## Context
You are helping to create unit tests for **StepFlow Process** steps in a .NET 8+ application using Onion Architecture. The solution uses xUnit, Entity Framework Core InMemory, and a custom StepFlow framework.

## Workflow (Start With Current Context)
- First, inspect what’s already in the agent/editor context (current file, open tabs, selections, and attached files) to understand the unit under test and existing patterns.
- Only after that, widen the search to other folders/projects (e.g., related DTOs, profiles, helpers) as needed.

## Missing Context (Stop And Ask)
- If the step under test (or required extensions/constants/entities) is not available in the current context (no relevant open files, selections, attachments, or pasted code), do **not** guess.
- Instead, prompt the user to provide one of:
	- Attach the relevant files, or
	- Open the relevant files in the editor, or
	- Paste the key code as text into the chat.
- Only proceed to writing tests once enough concrete code context is provided to compile and follow repo conventions.

## Key Requirements

### Always Use Real StepFlowContext
- Use the real `StepFlowContext` (not a mock)
- Keep tests deterministic and isolated using EF Core InMemory per test class
- Assert `NodeResult.State` values: `Succeeded`, `Skipped`, `SucceededWithErrors`, `Failed`

### Required Dependencies
The following infrastructure already exists in the solution:
- `Infrastructure/MediR.Testing/BaseTestFixture.cs` - Provides `CreateInMemoryDbContextOptions<TContext>()` and `GetServiceProvider()`
- `Infrastructure/MediR.Testing.WorkFlows/Helpers/StepFlowContextHelper.cs` - Provides `mockServiceProvider.CreateStepFlowContext()` extension
- `Infrastructure/MediR.WorkFlow.StepFlows/Entities/StepFlowContext.cs` - Backed by `StepFlowProperties` dictionary
- `Infrastructure/MediR.WorkFlow.StepFlows/Steps/BaseStepFlowStep.cs` - Prefer `SafeExecuteAsync()` for exception testing

### Fixture Pattern
When creating StepFlow tests, prefer a **single fixture approach**:
- Create a test fixture class that inherits from `BaseTestFixture`
- Include entity builder helpers in the fixture
- Inject the fixture into the test class via `IClassFixture<T>`

### Required Test Structure
Always include this field in StepFlow test classes:
```csharp
private readonly StepFlowContext _stepFlowContext;
```

## Template to Follow

```csharp
public sealed class [StepName]TestFixture : BaseTestFixture
{
	public [Entity] Create[Entity]([parameters])
	{
		// Entity builder helpers live here
		return new [Entity]();
	}
}

public class [StepName]Tests : IClassFixture<[StepName]TestFixture>
{
	private readonly StepFlowContext _stepFlowContext;
	private readonly [StepName] _step;

	public [StepName]Tests([StepName]TestFixture fixture)
	{
		var serviceProvider = fixture.GetServiceProvider();
		_stepFlowContext = serviceProvider.CreateStepFlowContext();

		_step = new [StepName](/* real or mocked dependencies */);
	}

	[Fact]
	public async Task ExecuteAsync_HappyPath_ShouldSucceedAsync()
	{
		// Arrange
		_stepFlowContext.SetProperty("[Key]", "[Value]");

		// Act
		var result = await _step.ExecuteAsync(_stepFlowContext);

		// Assert
		result.State.ShouldBe(NodeState.Succeeded);
	}
}
```

## StepFlowContext Usage

### Setting Values
- Prefer domain-specific extension methods when available: `context.GetTripGuid()`
- For raw properties: `_stepFlowContext.SetProperty(SomeConstants.SOME_KEY, value);`

### Reading Values
- Use existing domain extension methods when available
- Access raw properties via the StepFlowProperties dictionary

## EF Core InMemory Pattern

For repository-backed steps:
```csharp
var options = fixture.CreateInMemoryDbContextOptions<[Context]>();
var userContext = fixture.CreateUserContext();
userContext.SetSvClientId([clientId]);
var db = new [Context](userContext, options);
```

**Important**: Call `db.ChangeTracker.Clear()` before re-querying to avoid false positives from tracked entities.

## Exception Testing

For validating exception handling:
```csharp
var result = await _step.SafeExecuteAsync(_stepFlowContext);
result.State.ShouldBe(NodeState.SucceededWithErrors);
_stepFlowContext.Exceptions.ShouldNotBeEmpty();
```

## Test Case Checklist

Create tests for these scenarios:
- **Missing required context values** → `Skipped` or `Failed` (depending on step)
- **Valid context but no matching data in DB** → `Skipped`
- **Happy path** → `Succeeded` with persisted side effects
- **Exception path** → Use `SafeExecuteAsync`, expect `SucceededWithErrors` and recorded errors

## Coding Standards

Follow the solution's .NET coding standards:
- PascalCase for classes, methods, properties
- `_` prefix for private members
- `Async` suffix for async methods
- Proper using statements and namespace organization

## Instructions

When creating StepFlow tests:
1. Create both the test fixture and test class
2. Include entity builder methods in the fixture
3. Use real StepFlowContext and dependencies where possible
4. Mock only external dependencies (HTTP clients, external services)
5. Test all four main scenarios from the checklist
6. Use descriptive test method names following the pattern: `ExecuteAsync_[Scenario]_Should[ExpectedOutcome]Async`
7. Assert the correct `NodeState` result
8. Verify side effects (database changes, context property updates)

## Additional Notes

- Look for existing patterns in `*.Unit.Tests` projects by searching for `CreateStepFlowContext()` usage
- Reuse established fixture patterns that inherit from `BaseTestFixture`
- Keep tests isolated and deterministic
- Use Shouldly assertions for better error messages
