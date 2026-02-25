# Testing Memories

Accumulated lessons and patterns from testing work in this solution.

## Fixture Rules
- Fixture class and test class must always be in **separate files** — xUnit fixture sharing requires this.
- Helper DTOs/enums for a single test suite go as **nested types inside the fixture**, not standalone files.
- Always inherit fixture from `BaseTestFixture` — it provides AutoMapper, mock DbContext, logger builders, etc.

## EF Core InMemory
- Always use a **separate DbContext instance** for seeding vs. acting/asserting. Reusing the same context causes tracked-entity false positives.
- Use a unique InMemory database name per test class to prevent cross-test contamination.

## Shouldly
- Use `ShouldlyExtensions.cs` in `MediR.Testing/Extensions/` — check it before writing custom assertion logic, a helper may already exist.
- Never use FluentAssertions — Shouldly only.

## Moq
- Use `AddItemToConfiguration` from `MockExtensions` to mock IConfiguration values — do not manually setup `configuration["key"]`.
- Verify interactions only when the interaction itself is the contract being tested — avoid over-specifying.

## Controller Tests
- Always use `ControllerBuilder` from `MediR.Testing/Builders/` for HttpContext setup.
- Read `ControllerBuilder.cs` before using it — the fluent API has more options than are obvious.

## Logger Tests
- Use `LoggerBuilder<T>` from `BaseTestFixture.GetLogBuilder<T>()` — supports level, message content, exception, and event ID filtering.

## Add new entries here as lessons are learned during test work.
