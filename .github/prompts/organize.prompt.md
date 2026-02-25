---
agent: agent
description: This prompt is used to clean up C# files like CodeMaid would.
name: organize
---

# CodeMaid-style C# refactor (based on provided CodeMaid config)

You are refactoring **one or more C# files** as if the user ran **CodeMaid “Cleanup”** with the provided configuration.

## Input you will receive
- The full contents of a single `.cs` file (may include regions, XML doc comments, `using` directives, etc.).

## Output requirements
- Return the **entire updated file content**.
- Preserve behavior. Do **not** change public APIs or logic (only formatting/reorganization/cleanup).
- If a change could be behavior-altering or uncertain, **do not do it**.
- Keep any preprocessor directives (`#if`, `#pragma`, etc.) valid and correctly indented.

---

## Cleanup steps (apply in this order)

### 0) Respect ignore markers
Treat lines starting with either of these as **do-not-touch boundaries** (do not reflow comments or reformat the block they are guarding; keep their relative placement):
- `ReSharper disable `
- `ReSharper enable `

### 1) Exclusions (best-effort)
If the file path/name indicates it matches any of these patterns, **do not change the file** (return it unchanged):
- `\.Designer\.cs$`
- `\.Designer\.vb$`
- `\.resx$`
- `\.min\.css$`
- `\.min\.js$`
- `Migrations\.*`

(If you cannot determine the path, proceed normally.)

### 2) Run Visual Studio “Format Document” equivalent
Apply standard C# formatting:
- Fix indentation and braces consistently.
- Normalize spaces around operators, commas, and keywords.
- Ensure consistent newlines and remove trailing whitespace.
- Keep line endings consistent within the file.
- Do not change formatting of comments or string literals (except for whitespace normalization); preserve original formatting as much as possible while applying these rules.
- Do not add additional blank lines beyond what is necessary for the next steps (e.g., do not add extra blank lines between members and comments or attributes.

### 3) Sort `using` statements (CodeMaid “Sort”)
Because `ThirdParty_OtherCleaningCommandsExpression = Edit.RemoveAndSort` and cleanup does not skip it:
- Because you are not hooked into the Roslyn compiler, you cannot reliably determine which usings are truly unused. Therefore, **do not remove any usings** (even if they appear unused); only sort them.
- Sort remaining `using` directives (System* first, then others; alphabetical within groups).
- Keep `using static` and `using alias = ...` correctly ordered (place them with the rest in a sensible, stable order).
- Do not introduce global usings; only adjust the current file.

### 4) Reorganize members (CodeMaid reorganizer)
Reorganizing runs at start of cleanup (`Reorganizing_RunAtStartOfCleanup = True`).

Reorder members into these groups (regions are not required; do not add regions because `Reorganizing_RegionsInsertNewRegions = False`):

1. Using Statements
2. Fields (properties/variables). All property declarations, including private fields (prefixed with `_`) and static fields. Order them by: private first, then protected, then public. Group abstract fields with other fields of the same access level within each protected/public grouping.
    Example Order:
    ```csharp
        private readonly Dictionary<string, object> _dataSources = new();
        private string _azureMapsKey = string.Empty;

        protected AzureMaps? _azureMaps = null;
        protected abstract IConfigService _configService { get; }
        protected abstract ILogger _logger { get; }

        public AtlasMap? Map = null;
        public abstract string MapName { get; }
        public abstract string SubscriptionKey { get; }

        internal AtlasMap? OldMap = null;

        public static int InstanceCount = 0;
    ```
3. Properties (getters/setters) (`Reorganizing_MemberTypeProperties`). Order them by: private first, then protected, then public
    Example Order:
    ```csharp
        private Dictionary<string, object> DataSources
        {
            get => _dataSources;
        }

        private string AzureMapsKey
        {
            get => _azureMapsKey;
        }

        protected AzureMaps? AzureMaps
        {
            get => _azureMaps;
        }

        public AtlasMap? MapValue
        {
            get => Map;
        }

        public AtlasMap? OldMapValue
        {
            get => OldMap;
        }

        public static int GetInstanceCount()
        {
            return InstanceCount;
        }
    ```
4. Constructors (`Reorganizing_MemberTypeConstructors`)
5. Destructors (`Reorganizing_MemberTypeDestructors`)
6. Delegates (`Reorganizing_MemberTypeDelegates`)
7. Events (`Reorganizing_MemberTypeEvents`)
8. Enums (`Reorganizing_MemberTypeEnums`)
9. Interfaces (`Reorganizing_MemberTypeInterfaces`)
10. Indexers (`Reorganizing_MemberTypeIndexers`)
11. Methods (`Reorganizing_MemberTypeMethods`)
12. Structs (`Reorganizing_MemberTypeStructs`)
13. Classes (`Reorganizing_MemberTypeClasses`)
14. Other members not explicitly listed above:
   - Nested classes/structs/records

Additional rules:
- Do **not** order by access level (`Reorganizing_PrimaryOrderByAccessLevel = False`).
- Preserve relative order within the same group when possible, especially for logically related members.
- Keep partial type declarations coherent (don’t move members across partial files; only within this file).

### 5) Blank-line / padding rules from config
Apply these specific settings:
- **Do NOT** insert blank line padding after namespaces (`Cleaning_InsertBlankLinePaddingAfterNamespaces = False`).
- **DO** insert blank line padding before single-line properties (`Cleaning_InsertBlankLinePaddingBeforePropertiesSingleLine = True`).
- **Do NOT** insert blank space before self-closing angle brackets in XML (`Cleaning_InsertBlankSpaceBeforeSelfClosingAngleBrackets = False`).
- **Do NOT** update `#region` / `#endregion` directive names (`Cleaning_UpdateEndRegionDirectives = False`).
- Do not insert/keep empty regions (`Reorganizing_RegionsInsertKeepEvenIfEmpty = False`) — but since you must not add regions, only avoid creating empty ones if reordering would do so; otherwise leave regions as-is.

### 6) XML doc comment formatting (run during cleanup)
Because `Formatting_CommentRunDuringCleanup = True`, format XML documentation comments as follows:

- Wrap comment text at **120 columns** (`Formatting_CommentWrapColumn = 120`) when safe.
- For `<summary>...</summary>`:
  - Split summary to multiple lines (`Formatting_CommentXmlSplitSummaryTagToMultipleLines = True`) if it improves readability.
  - Keep related XML tags together (`Formatting_CommentXmlKeepTagsTogether = True`)—don’t separate opening/closing tags from their content unnecessarily.
- Indentation:
  - Indent XML doc tag values by **2 spaces** (`Formatting_CommentXmlValueIndent = 2`). This is Very important. Do not use tabs.
- Spacing:
  - Ensure a single space in single tags when appropriate (`Formatting_CommentXmlSpaceSingleTags = True`).
  - Ensure consistent spacing between tags (`Formatting_CommentXmlSpaceTags = True`).
- Do not rewrite the meaning of documentation; only format it.

### 7) Language scope
This `/organize` prompt is for **C# only**. Ignore non-C# cleaning toggles (XAML/XML/JS/etc.) even if present in the config.

### 8) Namespaces
Use file scoped namespaces if the file is using C# 10 or later and it is safe to do so (i.e., no nested namespaces, no preprocessor directives that would interfere, etc.). Otherwise, keep the existing namespace style.

✅ GOOD:
```csharp
    namespace MyNamespace;

    public class MyClass
    {
        // ...
    }
```
❌ BAD:
```csharp
    namespace MyNamespace
    {
        public class MyClass
        {
            // ...
        }
    }
```

### 9) Sorting the members
Once the members are categorized, sort them alphabetically within each category. For example, within the "Methods" category, sort based on teh following sore groups:
- public abstract methods alphabetically by their names
- public methods alphabetically by their names
- protected abstract methods alphabetically by their names
- protected methods alphabetically by their names
- private methods alphabetically by their names

### 10) Sorting object properties
For static fields that contain object literals, sort the properties alphabetically by their key names. For example:
```csharp
    public static readonly IReadOnlyDictionary<string, string> RouterDrawer = new Dictionary<string, string>
    {
        ["ApplyFilters"] = "filtersApply",
        ["Close"] = "close",
        ["CollapseTableView"] = "collapseTableView",
        ["ExpandTableView"] = "expandTableView",
        ["ManageRoutes"] = "manageRoutes",
        ["RouteSelect"] = "routeSelect",
        ["Search"] = "search",
        ["ShowMenu"] = "showMenu",
    };
```

---

## Safety checks before you finalize
- The file should compile the same way it did before (no symbol renames, no signature changes).
- Usings removal must not remove required usings for attributes, extension methods, or conditional compilation.
- Member reordering must not break code that depends on textual order (rare, but watch for `#region`, `#if`, partial methods, or source generators).

---

## Success Criteria
- All `using` directives are correctly sorted and unused ones are removed.
- Members are organized into the correct groups and sorted alphabetically within those groups.
- XML documentation comments are formatted according to the specified rules.
- The overall formatting of the code is consistent and adheres to standard C# conventions.
- All comments, attributes, XML Comments, and formatting have been preserved. The code is valid C# and maintains all existing functionality.


## Response format
Return only a list of updated items as a summary. Do not output the entire file content in the summary; only list the types of changes made (e.g., "Removed 3 unused usings", "Sorted 5 using directives", "Reordered members into 3 groups", "Formatted XML doc comments"). The full updated file content should be returned as the main response, not in the summary.
