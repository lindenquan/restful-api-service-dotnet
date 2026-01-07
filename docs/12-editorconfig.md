# EditorConfig in .NET

This document explains why we use `.editorconfig`, how modern .NET supports it, and what methods were used before EditorConfig existed.

## What is EditorConfig?

EditorConfig is a **cross-platform, cross-IDE standard** for defining coding styles and formatting rules. It uses a simple `.editorconfig` file that IDEs and editors automatically detect and apply.

```
┌─────────────────────────────────────────────────────────────────────┐
│                         .editorconfig                               │
│                                                                     │
│  • Indentation (tabs vs spaces)                                     │
│  • Line endings (LF vs CRLF)                                        │
│  • Charset (UTF-8)                                                  │
│  • Naming conventions (IInterface, _privateField)                   │
│  • Code style (var vs explicit type, expression bodies)             │
│  • Analyzer severity (warnings, errors, suggestions)                │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Why Use EditorConfig?

### 1. **IDE-Agnostic**

Works across all major editors:

| Editor/IDE | Support |
|------------|---------|
| Visual Studio | ✅ Native |
| VS Code | ✅ Native (with extension) |
| Rider | ✅ Native |
| Vim/Neovim | ✅ Plugin |
| Sublime Text | ✅ Plugin |

### 2. **Version Controlled**

The `.editorconfig` file lives in your repository, so:
- ✅ All team members share the same settings
- ✅ Settings travel with the code
- ✅ Changes are tracked in Git history
- ✅ No "works on my machine" formatting issues

### 3. **Hierarchical**

EditorConfig files cascade - you can have different settings per folder:

```
/repo
├── .editorconfig          # root = true (base settings)
├── src/
│   └── .editorconfig      # Override for source code
└── tests/
    └── .editorconfig      # Override for test code (relaxed rules)
```

### 4. **Build-Time Enforcement**

Modern .NET can **enforce** EditorConfig rules during build, failing CI if rules are violated.

---

## The Old Days: Before EditorConfig (.NET Framework Era)

### Era 1: FxCop (2002-2015)

**FxCop** was a standalone static analysis tool for .NET Framework:

```
┌─────────────────────────────────────────────────────────────────────┐
│                     FxCop (Legacy)                                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ❌ Separate tool - not integrated with compiler        　　　      │
│  ❌ Analyzed compiled DLLs (post-build, slow)    　　　             │
│  ❌ XML-based rule configuration              　　　                │
│  ❌ No IDE integration - had to run manually           　　　       │
│  ❌ Only code quality, no code style　　　                          │
│                                                                     │
│  Configuration: FxCop.exe /rules:+Microsoft.Design#CA1001           │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Era 2: StyleCop (2008-2018)

**StyleCop** focused on code style and formatting:

```
┌─────────────────────────────────────────────────────────────────────┐
│                     StyleCop (Legacy)                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ❌ Separate NuGet package required             　　　              │
│  ❌ XML-based Settings.StyleCop configuration file    　　　        │
│  ❌ Not version-controlled easily                   　　　          │
│  ❌ Visual Studio only                            　　　            │
│  ❌ Slow - analyzed source at build time       　　　               │
│                                                                     │
│  Configuration: Settings.StyleCop (XML file)                        │
│                                                                     │
│  <StyleCopSettings>                                                 │
│    <Analyzers>                                                      │
│      <Analyzer AnalyzerId="StyleCop.CSharp.SpacingRules">           │
│        <Rules>                                                      │
│          <Rule Name="SA1027">                                       │
│            <RuleSettings>                                           │
│              <BooleanProperty Name="Enabled">False</BooleanProperty>│
│            </RuleSettings>                                          │
│          </Rule>                                                    │
│        </Rules>                                                     │
│      </Analyzer>                                                    │
│    </Analyzers>                                                     │
│  </StyleCopSettings>                                                │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Era 3: Ruleset Files (.NET Framework / Early .NET Core)

**Ruleset files** (`.ruleset`) configured analyzer severity:

```xml
<!-- MyProject.ruleset -->
<?xml version="1.0" encoding="utf-8"?>
<RuleSet Name="My Rules" ToolsVersion="15.0">
  <Rules AnalyzerId="Microsoft.CodeAnalysis.CSharp" RuleNamespace="Microsoft.CodeAnalysis.CSharp">
    <Rule Id="CS1591" Action="None" />  <!-- Missing XML comment -->
  </Rules>
  <Rules AnalyzerId="Microsoft.CodeQuality.Analyzers" RuleNamespace="Microsoft.CodeQuality.Analyzers">
    <Rule Id="CA1062" Action="Warning" />
    <Rule Id="CA1822" Action="None" />
  </Rules>
</RuleSet>
```

**Problems with Ruleset:**
- ❌ XML is verbose and error-prone
- ❌ Separate file from code style settings
- ❌ No IDE support for code style (only analyzer rules)
- ❌ Had to reference in `.csproj`: `<CodeAnalysisRuleSet>MyProject.ruleset</CodeAnalysisRuleSet>`

---

## The Modern Era: EditorConfig + Roslyn (.NET 5+)

### What Changed?

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Modern .NET (5, 6, 7, 8, 9, 10)                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ✅ Roslyn Analyzers built into SDK - no NuGet needed    　　　     │
│  ✅ EditorConfig is THE standard for all settings      　　　       │
│  ✅ Code analysis enabled by default (.NET 5+)             　　　   │
│  ✅ Build-time enforcement with EnforceCodeStyleInBuild     　　　  │
│  ✅ IDE + CLI + CI all use same rules                        　　　 │
│  ✅ Human-readable format (not XML)                         　　　  │
│                                                                     │
│  Configuration: .editorconfig (simple key=value)                    │
│                                                                     │
│  [*.cs]                                                             │
│  dotnet_diagnostic.CA1822.severity = warning                        │
│  csharp_style_var_for_built_in_types = true:suggestion              │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

---

## .NET 8/9/10 EditorConfig Features

### 1. Build-Time Enforcement

Add to your `.csproj` to enforce code style during build:

```xml
<PropertyGroup>
  <!-- Enforce code style rules during build (not just in IDE) -->
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>

  <!-- Treat all warnings as errors (optional, for strict enforcement) -->
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>

  <!-- Or just treat code analysis warnings as errors -->
  <CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>
</PropertyGroup>
```

### 2. Analysis Levels (.NET 8+)

Control how strict the analyzer is:

```xml
<PropertyGroup>
  <!-- Options: None, Default, Minimum, Recommended, All -->
  <AnalysisLevel>latest-Recommended</AnalysisLevel>

  <!-- Or lock to specific .NET version -->
  <AnalysisLevel>9.0</AnalysisLevel>
</PropertyGroup>
```

| Level | Description |
|-------|-------------|
| `None` | All rules disabled |
| `Default` | Small set of rules as warnings |
| `Minimum` | More aggressive than Default |
| `Recommended` | Good balance of rules |
| `All` | All rules enabled as warnings |

### 3. Severity Format (.NET 9+)

Starting in .NET 9, you can specify severity inline with the option:

```ini
# Old format (still works)
dotnet_style_require_accessibility_modifiers = always
dotnet_diagnostic.IDE0040.severity = warning

# New format (.NET 9+) - more concise
dotnet_style_require_accessibility_modifiers = always:warning
```

---

## Comparison: Old vs New

| Aspect | Old (.NET Framework) | New (.NET 5+) |
|--------|---------------------|---------------|
| **Code Quality** | FxCop (standalone) | Roslyn Analyzers (built-in) |
| **Code Style** | StyleCop (NuGet) | EditorConfig (built-in) |
| **Configuration** | `.ruleset` (XML) | `.editorconfig` (INI) |
| **IDE Support** | Visual Studio only | All editors |
| **Build Enforcement** | Manual setup | `EnforceCodeStyleInBuild` |
| **Naming Rules** | Custom StyleCop XML | `dotnet_naming_*` |
| **Severity** | XML rules | `dotnet_diagnostic.*.severity` |

---

## Our Project's EditorConfig

Our `.editorconfig` configures:

### Basic Formatting
```ini
[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true
```

### C# Specific
```ini
[*.cs]
# File-scoped namespaces (C# 10+)
csharp_style_namespace_declarations = file_scoped:warning

# Primary constructors (C# 12+)
csharp_style_prefer_primary_constructors = true:suggestion

# var preferences
csharp_style_var_for_built_in_types = true:suggestion
```

### Naming Conventions
```ini
# Interfaces must start with I
dotnet_naming_rule.interface_should_be_begins_with_i.severity = warning

# Private fields should be _camelCase
dotnet_naming_rule.private_fields_should_be_camel_case.severity = suggestion

# Async methods must end with Async
dotnet_naming_rule.async_methods_should_end_with_async.severity = suggestion
```

### Analyzer Rules
```ini
# IDE0005: Remove unnecessary using
dotnet_diagnostic.IDE0005.severity = warning

# IDE0055: Fix formatting
dotnet_diagnostic.IDE0055.severity = warning

# CA1822: Mark members as static
dotnet_diagnostic.CA1822.severity = suggestion
```

---

## Quick Reference: Common EditorConfig Settings

### Code Style (IDExxxx)

| Rule | Description | Example |
|------|-------------|---------|
| `IDE0003` | Remove `this.` qualification | `this.name` → `name` |
| `IDE0005` | Remove unnecessary using | Unused imports |
| `IDE0040` | Add accessibility modifiers | `void Foo()` → `private void Foo()` |
| `IDE0055` | Fix formatting | Spacing, indentation |
| `IDE0161` | File-scoped namespace | `namespace X { }` → `namespace X;` |

### Code Quality (CAxxxx)

| Rule | Description | Severity |
|------|-------------|----------|
| `CA1822` | Mark members as static | Suggestion |
| `CA1062` | Validate public method arguments | Warning |
| `CA2007` | Don't await with ConfigureAwait | Suggestion |
| `CA1848` | Use LoggerMessage delegates | Suggestion |

---

## Migration Guide: Ruleset to EditorConfig

If migrating from `.ruleset`:

**Before (`.ruleset`):**
```xml
<Rule Id="CA1822" Action="Warning" />
<Rule Id="CA2007" Action="None" />
```

**After (`.editorconfig`):**
```ini
dotnet_diagnostic.CA1822.severity = warning
dotnet_diagnostic.CA2007.severity = none
```

---

## See Also

- [Official EditorConfig Spec](https://editorconfig.org/)
- [.NET Code Analysis Docs](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview)
- [Code Style Options](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options)
- [Naming Rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules)

