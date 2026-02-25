---
applyTo: "**/*.cs"
---

# MediRoutes Platform — Copilot Instructions

## ⛔ Mandatory Workflow Rules
These rules are non-negotiable. Violating any of these is a hard failure.

1. **NEVER execute a plan without explicit user approval.**
   - Present the plan and STOP.
   - Do NOT call any tool that creates, modifies, or deletes files until the user responds with approval.
   - "Presenting the plan" and "executing the plan" are two separate conversations. Wait for the user between them.
2. **Always use Newtonsoft.Json** for JSON serialization and deserialization. Never use System.Text.Json.
3. **ALWAYS check and update affected unit tests** when making code changes, without being asked.
4. When writing unit tests, always leverage the pre-built mocks and helpers from `Infrastructure\MediR.Testing\MediR.Testing.csproj` over raw mocking. Check `BaseTestFixture` and the `Mocks/Builders` folders first before creating manual mocks.

## Available Resources

| Type         | Path                                      | Load When                           |
| ------------ | ----------------------------------------- | ----------------------------------- |
| Instructions | instructions/architecture.instructions.md | Auto-injected for all `.cs` files   |
| Instructions | instructions/testing.instructions.md      | Auto-injected for test files        |
| Instructions | instructions/memories.instructions.md     | Auto-injected (memory index)        |
| Instructions | instructions/review.instructions.md       | Code review requested               |
| Skill        | skills/api-developer/SKILL.md             | Working in API layer                |
| Skill        | skills/domain-developer/SKILL.md          | Working in Domain layer             |
| Skill        | skills/infrastructure-developer/SKILL.md  | Working in Infrastructure layer     |
| Skill        | skills/microsoft-code-reference/SKILL.md  | Microsoft API refs & code samples   |
| Skill        | skills/microsoft-docs/SKILL.md            | Official Microsoft documentation    |
| Skill        | skills/sql-metadata/SKILL.md              | Inspecting SQL Server schema        |
| Agent        | agents/api-developer.agent.md             | Creating/updating BFF API endpoints |
| Agent        | agents/unit-tests-writer.agent.md                | Writing/fixing unit tests           |
| Agent        | agents/postman-sync.agent.md              | Syncing Postman collections         |
| Prompt       | prompts/code-review.prompt.md             | Reviewing code                      |
| Prompt       | prompts/create-mapper.prompt.md           | Creating mappers between classes    |
| Prompt       | prompts/organize.prompt.md                | Organizing/formatting code          |
| Prompt       | prompts/prompt-confirm.prompt.md          | Confirming before executing plans   |
| Prompt       | prompts/stepflow-tests.prompt.md          | Generating StepFlow-based tests     |
| Prompt       | prompts/unit-tests-writer.prompt.md              | Generating unit tests               |
| Memory       | memories/memory.md                        | Index of all project memories       |

## Full Architecture & Coding Standards
See `instructions/architecture.instructions.md` — auto-injected for all `.cs` files.

## Testing Standards
See `instructions/testing.instructions.md` — auto-injected for test files.
