---
description: Scaffold a new IMapper partial-class set (shell + mapping files) and update the .csproj with DependentUpon entries. Works for any project in the solution.
name: create-mapper
agent: agent
tools: [vscode/askQuestions, read/getNotebookSummary, read/readFile, agent, edit/createDirectory, edit/createFile, edit/editFiles, search, memory]
---

# Create Mapper — Scaffolding Prompt

## Purpose

Scaffold one complete mapper following the established **partial-class pattern** used throughout this solution.
Each mapper consists of:

| File | Role |
|---|---|
| **Shell file** (`{MapperName}.cs`) | Declares the `partial class`, lists `IMapper<TOut, TIn>` interfaces, and optionally accepts constructor-injected `IMapper` for nested mapping. |
| **Mapping file(s)** (`{SourceType}To{TargetType}.cs`) | Each is a `partial class` of the shell containing a single `Map` method for one direction. |

After creating the files, the agent **must** update the project's `.csproj` with `<Compile Update>` / `<DependentUpon>` entries so the mapping files nest under the shell in Solution Explorer.

---

## Workflow

### Step 1 — Gather Information

Ask the user the following questions (skip any that are already answered in the prompt or conversation):

1. **Project path** — Which `.csproj` file should the mapper live in?
   _Example: `API/Facilities/Facilities.Api/Facilities.Api.csproj`_
2. **Mapper folder** — Relative folder inside the project for mapper files (default: `Mappers`).
3. **Mapper class name** — The shell class name.
   _Example: `AddressDtoMapper`_
4. **Direction** — Bidirectional (A ↔ B) or one-way (A → B)?
5. **Type A** — Fully qualified or short name of the first type + its `using` namespace.
   _Example: `AddressDto` from `Facilities.Api.Dto.RateInquiries`_
6. **Type B** — Fully qualified or short name of the second type + its `using` namespace.
   _Example: `Address` from `Facilities.Domain.ValueObjects`_
7. **Needs nested mapping?** — Does any `Map` method need to call `IMapper` to map child objects? If yes, the shell constructor should accept `IMapper mapper`.

### Step 2 — Derive Names

Using the collected info, compute:

- **Namespace**: Derive from the project's root namespace + mapper folder (e.g., `Facilities.Api.Mappers`). Inspect an existing `.cs` file in the target folder if unsure.
- **Shell filename**: `{MapperName}.cs`
- **Mapping filenames**:
  - A → B: `{TypeAShortName}To{TypeBShortName}.cs`
  - B → A (if bidirectional): `{TypeBShortName}To{TypeAShortName}.cs`

Present the planned file list to the user for confirmation before generating.

### Step 3 — Generate Files

Use the patterns below **exactly**. These rules are **mandatory** and must not be deviated from:

1. **File-scoped namespace first** — `namespace X.Y.Z;` must be the very first line. No `using` statements above it.
2. **`using` statements inside the namespace** — all `using` directives go AFTER the file-scoped namespace declaration, not before it.
3. **`using MediR.Mapper.Interfaces;`** — this is the correct namespace for `IMapper<TOut, TIn>`. Never use `using MediR.Mapper;`.
4. **Implicit interface implementation** — use `public {ReturnType} Map({InputType} mapFrom)`, NOT explicit implementation like `ReturnType IMapper<...>.Map(...)`.
5. **Parameter name `mapFrom`** — always name the Map method parameter `mapFrom`, not `source` or anything else.
6. **Expression-bodied Map methods** — use `=> new(/* TODO: Provide constructor arguments */);` not block bodies with `return`.
7. **Tab indentation** — use tabs, not spaces.
8. **Mapping partial files omit `MediR.Mapper.Interfaces`** — only the shell file needs the `IMapper` using. Mapping files only need the type-specific usings.

#### 3a. Shell File — Bidirectional, No Nested Mapping

```csharp
namespace {Namespace};

using {TypeA.Namespace};
using MediR.Mapper.Interfaces;
using {TypeB.Namespace};

public partial class {MapperName} :
    IMapper<{TypeA}, {TypeB}>,
    IMapper<{TypeB}, {TypeA}>
{ }
```

#### 3b. Shell File — Bidirectional, With Nested Mapping

```csharp
namespace {Namespace};

using {TypeA.Namespace};
using MediR.Mapper.Interfaces;
using {TypeB.Namespace};

public partial class {MapperName}(IMapper mapper) :
    IMapper<{TypeA}, {TypeB}>,
    IMapper<{TypeB}, {TypeA}>
{ }
```

#### 3c. Shell File — One-Way (A → B), No Nested Mapping

```csharp
namespace {Namespace};

using {TypeA.Namespace};
using MediR.Mapper.Interfaces;
using {TypeB.Namespace};

public partial class {MapperName} : IMapper<{TypeB}, {TypeA}> { }
```

> Note: `IMapper<TOut, TIn>` — the first type param is the **output**, the second is the **input**.

#### 3d. Shell File — One-Way (A → B), With Nested Mapping

```csharp
namespace {Namespace};

using {TypeA.Namespace};
using MediR.Mapper.Interfaces;
using {TypeB.Namespace};

public partial class {MapperName}(IMapper mapper)
    : IMapper<{TypeB}, {TypeA}>
{ }
```

#### 3e. Mapping File Template

```csharp
namespace {Namespace};

using {InputType.Namespace};
using {OutputType.Namespace};

public partial class {MapperName}
{
    public {OutputType} Map({InputType} mapFrom)
        => new({OutputType constructor args — leave as TODO placeholders});
}
```

- Include only the `using` statements needed for that specific mapping file (omit `MediR.Mapper.Interfaces` since it's not directly used).
- If nested mapping is needed, the `mapper` field from the primary constructor is accessible without additional `using` — but add `using MediR.Mapper.Interfaces;` if the non-generic `IMapper` is used to call `mapper.Map<T>()`.
- Add `// TODO: Provide constructor arguments` as the placeholder inside `new(...)` so the user knows to fill it in.

### Step 4 — Update .csproj

Open the target `.csproj` file and add `<Compile Update>` entries inside an existing or new `<ItemGroup>`.

For each mapping file, add:

```xml
<Compile Update="{MapperFolder}\{MappingFileName}.cs">
    <DependentUpon>{ShellFileName}.cs</DependentUpon>
</Compile>
```

**Rules:**
- If there is already an `<ItemGroup>` containing other `<Compile Update>` entries with `<DependentUpon>`, add the new entries to that same group, maintaining alphabetical order by the `Update` attribute value.
- If no such group exists, create a new `<ItemGroup>` at the end of the project file (before the closing `</Project>` tag).
- Use backslash (`\`) as the path separator in the `Update` attribute value to match the existing convention.

### Step 5 — Verify

After generating all files and updating the `.csproj`:

1. Confirm to the user which files were created and which `.csproj` entries were added.
2. Remind the user to:
   - Fill in the `// TODO:` constructor arguments in each mapping file.
   - Ensure the mapper assembly is passed to `AddMappers()` in the DI registration if it's a new project that doesn't already do so.

---

## Reference Examples

### Bidirectional — No Nested Mapping

**Shell** (`AddressDtoMapper.cs`):
```csharp
namespace Facilities.Api.Mappers;

using Facilities.Api.Dto.RateInquiries;
using MediR.Mapper.Interfaces;
using Facilities.Domain.ValueObjects;

public partial class AddressDtoMapper :
    IMapper<Address, AddressDto>,
    IMapper<AddressDto, Address>
{ }
```

**A → B** (`AddressDtoToAddress.cs`):
```csharp
namespace Facilities.Api.Mappers;

using Facilities.Api.Dto.RateInquiries;
using Facilities.Domain.ValueObjects;

public partial class AddressDtoMapper
{
    public Address Map(AddressDto mapFrom)
        => new(mapFrom.Line1, mapFrom.Line2, mapFrom.City, mapFrom.State, mapFrom.Zip);
}
```

**B → A** (`AddressToAddressDto.cs`):
```csharp
namespace Facilities.Api.Mappers;

using Facilities.Api.Dto.RateInquiries;
using Facilities.Domain.ValueObjects;

public partial class AddressDtoMapper
{
    public AddressDto Map(Address mapFrom)
        => new(mapFrom.Line1, mapFrom.Line2, mapFrom.City, mapFrom.State, mapFrom.Zip);
}
```

**csproj entries:**
```xml
<Compile Update="Mappers\AddressDtoToAddress.cs">
    <DependentUpon>AddressDtoMapper.cs</DependentUpon>
</Compile>
<Compile Update="Mappers\AddressToAddressDto.cs">
    <DependentUpon>AddressDtoMapper.cs</DependentUpon>
</Compile>
```

### One-Way — With Nested Mapping

**Shell** (`TripMapper.cs`):
```csharp
namespace Facilities.Api.Mappers;

using Facilities.Api.Dto.TripRequests;
using Facilities.Domain.Entities;
using MediR.Mapper.Interfaces;

public partial class TripMapper(IMapper mapper)
    : IMapper<TripResponseDto, Trip>
{ }
```

**Mapping** (`TripToTripResponse.cs`):
```csharp
namespace Facilities.Api.Mappers;

using Facilities.Api.Dto.RateInquiries;
using Facilities.Api.Dto.TripRequests;
using Facilities.Domain.Entities;

public partial class TripMapper
{
    public TripResponseDto Map(Trip mapFrom)
        => new(
            mapFrom.Id,
            mapFrom.Status.ToString(),
            mapFrom.OptionId,
            mapper.Map<ProviderResponseDto>(mapFrom.Provider),
            mapFrom.ServiceLevel.ToString(),
            mapFrom.LegType.ToString(),
            mapFrom.LegDirection?.ToString(),
            mapFrom.CorrelationId,
            mapFrom.AppointmentTime,
            mapFrom.EstimatedPickupTime,
            mapFrom.EstimatedDropoffTime,
            mapFrom.PriceCents,
            mapFrom.Currency,
            mapFrom.CompanionCount,
            mapFrom.CreatedAt,
            mapFrom.UpdatedAt);
}
```

**csproj entry:**
```xml
<Compile Update="Mappers\TripToTripResponse.cs">
    <DependentUpon>TripMapper.cs</DependentUpon>
</Compile>
```
