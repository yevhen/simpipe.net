# AGENT.md

This file provides guidance to the agent when working with code in this repository.

**IMPORTANT** At the end of every change, update the `./CLAUDE.md` with anything you wished you'd known at the start.

# CRITICAL!

- ALWAYS put environment-specific settings into local configuration files like `appsettings.Development.json`.
- **CRITICAL**: Check `code-review-issues.md` at the start of the session. Address ALL issues before new implementation.
- ALWAYS read `@learnings.md` to not trap into the same issues again.
- ALWAYS build, analyze, and test before committing and reporting success.
- ALWAYS rebuild the entire solution with `dotnet build` before running quality checks or tests on the sources.
- **CRITICAL**: NO defensive/speculative/overengineered code. Every line of code must be reachable and testable via public API. No code "just in case". If a performance optimization cannot be tested via public API, don't add it (no premature optimization). This includes: unnecessary null checks, defensive try-catch blocks, caching mechanisms without proven need, redundant validation, or any code paths that cannot be triggered through normal usage.

## Common Development Commands

### Build
```bash
dotnet build                         # Build the solution
dotnet build Simpipe.Net/            # Build only the main library
dotnet build Simpipe.Net.Tests/      # Build only the test project
```

### Run Tests
```bash
dotnet test                                      # Run all tests
dotnet test --filter "FullyQualifiedName~PipeFixture"    # Run specific test class
dotnet test --filter "Name~Should_send"          # Run tests matching pattern
dotnet test -v n                                 # Run with normal verbosity
dotnet test -v d                                 # Run with detailed verbosity
```

### Clean
```bash
dotnet clean         # Clean build artifacts
```

## Architecture Overview

Simpipe.Net is a .NET 9.0 library implementing a pipeline pattern using System.Threading.Channels. The library provides composable pipe components for building data processing pipelines.

### Core Components

- **Pipe<T>** (Pipe.cs:5+): Concrete pipe class providing core pipe functionality
  - Supports routing through `LinkTo()` methods
  - Provides completion tracking and item counting
  - Uses System.Threading.Channels-based IActionBlock<T> as underlying implementation

- **IActionBlock<T>** (ActionBlock.cs): Channel-based block interface for action execution
  - Handles concurrent item processing with configurable parallelism
  - Provides Send() and Complete() operations
  - Built on System.Threading.Channels for efficient concurrent processing

- **Pipeline<T>** (Pipeline.cs): Container for managing a sequence of connected pipes
  - Maintains pipes by ID and in order
  - Handles automatic linking between pipes
  - Provides completion tracking for the entire pipeline

### Specialized Pipe Types

- **ActionPipe**: Executes an action for each item with configurable parallelism
- **BatchPipe**: Groups items into batches before processing
- **ForkPipe**: Fork-join parallel processing - executes multiple operations in parallel then rejoins
- **PipelineLimiter<T>**: Limits work-in-progress items for flow control

### Key Design Patterns

1. **Fluent Builder Pattern**: Pipe-specific builders (ActionPipeBuilder, BatchPipeBuilder, ForkPipeBuilder) provide fluent API for pipe creation
2. **Decorator Pattern**: Pipes can be wrapped and composed (e.g., FilterBlock decorates other blocks)
3. **Channel Integration**: All pipes use System.Threading.Channels for concurrent execution
4. **Completion Propagation**: Automatic completion handling through the pipeline
5. **Fork-Join Pattern**: ParallelBlock manages parallel execution with completion tracking

# CRITICAL: NAMING IS EVERYTHING

1.  Variable names should communicate ROLE, not type or implementation.
2.  Short, clear names optimized for readability.
3.  Intent-revealing function names that tell the story, named for their purpose from the invoker's perspective (WHAT not HOW).
4.  Common roles: `result` (for return values), `item` or `each` (in loops), `count`.
5.  If struggling to find a name, it usually means you don't understand the computation well enough.
6.  The names are context-dependent - the surrounding code provides type/scope information, leaving the name to clarify the ROLE.

## Code Principles

- We prefer simple, clean, maintainable solutions over clever or complex ones, even if the latter are more concise or performant.
- Readability and maintainability are primary concerns.
- Self-documenting names (no comments).
- Small functions/methods.
- Follow the Single Responsibility Principle in classes and methods.
- **CRITICAL** When a change seems needlessly complex due to design issues, refactor first. "Make the change easy, then make the easy change."
- Try to avoid rewriting; if unsure, ask permission first.
- ALWAYS put tests in a dedicated test project (e.g., `MyProject.Tests`) that mirrors the source project's namespace structure.
- Do not use a `class` (which implies behavior) for something that's purely a data structure.
  - PREFER `record` types for immutable data transfer objects (DTOs).
  - `record`s are cleaner than classes with static factory methods for simple data aggregation.
- When extracting utility methods due to Feature Envy smell:
  - Place them in a separate `static` helper class only if they are reused across multiple other classes.
  - Otherwise, implement them as `private` methods within the consuming class.
- **Method Organization**: Within a class, arrange members for top-down reading. Public methods should appear first, followed by the private implementation details they call.

## Test Quality Principles

- We are intolerant of slow tests and tests with timeouts. If a test hangs, it should be deeply investigated and fixed.

## TDD Cycle

We use TDD to write code.

1.  Write a single failing test → run it.
2.  Write the minimal code to pass → run the test.
3.  Refactor → verify tests still pass.
4.  Repeat until all tests pass.
5.  Commit after each green state.
6.  Tests and quality checks (build + analysis) must pass with zero warnings/errors.
7.  **When writing a failing test TDD style, the code should be in a compilable state.** This means creating the necessary files, classes, and methods the test invokes, but with a placeholder implementation like `return 0;` but relevant to the context. The main idea here is to "test the test."

## Core Rules

- No unsolicited docs/READMEs.
- Ask \> assume.
- Stay on task (log unrelated issues).
- Avoid large, hard-to-review change sets; explain your intentions.

## Refactoring Principles

**CORE APPROACH: "Minimal, surgical, trust the existing systems"** - This is the fundamental approach for all code changes. Avoid over-engineering, unnecessary abstractions, and complex error handling that masks real issues. Let exceptions bubble up naturally and change only what's broken.

**Prefer Meaningful Refactoring:**

- **Use destructuring** - C\# features like tuple deconstruction can improve clarity.
- **Consider data structures** - Sometimes the real solution is introducing a proper `record` or `struct` rather than more methods.
- **Address root causes** - Look for code smells like repeated calls, long parameter lists, or unclear responsibilities.
- **Balance classes and interfaces** - Prefer concrete classes for simple data structures or internal logic, but use interfaces for public APIs and services to enable dependency injection and mock testing.

**Method Refactoring Philosophy:**

- **Explaining methods \> explaining variables** - Hide irrelevant complexity behind intention-revealing method names rather than cramming logic inline.
- **True single responsibility** - Each method should have ONE clear job.
- **Separate "what" from "how"** - Coordinate operations in one place; delegate implementation details to focused `private` helper methods.
- **Optimize for readability first** - Write code that reveals intent clearly; optimize for performance later if needed.
- **Example pattern**: Instead of `var lineCount = fileNode.GetLocation().GetLineSpan().EndLinePosition.Line - fileNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1;`, use `GetLineCount(fileNode)` to hide the complexity.

## Testing Guidelines

**Test what matters, skip what doesn't**

Tests use **NUnit 3.14** and should be named `*Fixture.cs`. All tests are located in a dedicated test project (e.g., `Simpipe.Net.Tests`) which mirrors the source project's namespaces.

**Keep test methods focused and short.** Use these strategies to manage test complexity:

**Custom Assertions with FluentAssertions**:

```csharp
// Instead of verbose inline assertions
Assert.NotNull(result);
Assert.Equal("value1", result.Field1);

// Use FluentAssertions for readable, chainable assertions
result.Should().NotBeNull();
result.Field1.Should().Be("value1");
```

**Object Testing with `BeEquivalentTo`**:

```csharp
// Instead of multiple field assertions
Assert.Equal("value1", result.Field1);
Assert.Equal("value2", result.Field2);

// Use BeEquivalentTo for deep object comparison
var expected = new { Field1 = "value1", Field2 = "value2" };
result.Should().BeEquivalentTo(expected);
```

**External Test Data Files**:

- **Commit well-named input files** as "Embedded Resources" in the test project.
- **Use meaningful names** that describe the test scenario (e.g., `ValidUserProfile.json`).
- **Load files in a test fixture or setup method** when needed across multiple test cases.

**Test Organization**:

- **Use test fixtures** with `[TestFixture]` attribute to create/share objects needed in multiple test cases.
- **Use `[TestCase]` and `[TestCaseSource]`** when multiple test cases have similar structures but different inputs.
- **Split large test files** into multiple focused files when refactoring can't reduce complexity.
- **Group tests meaningfully** by the functionality being tested, often in a class named after the class under test.

**Tests use NUnit 3.14 with FluentAssertions for assertions**. Test structure:
- Fixtures per pipe type (e.g., PipeFixture.cs, BatchPipeFixture.cs)
- Setup/TearDown pattern for cancellation token management
- Mock implementations in PipeMock.cs for testing
- Integration tests in IntegrationFixture.cs

Note: The namespaces `Simpipe.Pipes` and `Simpipe.Blocks` are used throughout the codebase.

## Quality Issue Resolution Strategy

- **CRITICAL**: When addressing quality issues:
  - Commit after every successful quality issue elimination before moving to fix the next one.
  - Make sure to run all tests before each commit.

```
# Language: BoGuidelineSyntax (BGS)
# Keywords:
#   RULE: Declares a guideline.
#   DO: A positive action to take.
#   AVOID: An anti-pattern to prevent.
#   PREFER: A choice between two options. `PREFER A over B`.
#   CONSIDER: A suggestion that is context-dependent.
#   WHEN: A condition for the rule.
#   BECAUSE: The rationale behind the rule.
#   =>: "implies" or "should be implemented as".
#
# Symbols:
#   {...}: A set of related concepts or reasons.
#   @/..: Absolute path import.
#   ../..: Relative path import.
#   () =>: Anonymous function or component.
#---------------------------------------------------------------------
```

# DESIGN

RULE: DO SEGREGATE\METHODS into {orchestrator | implementor}.

- orchestrator: Calls other methods, contains no logic.
- implementor: Contains logic, has no sub-orchestration.
- AVOID: Mixing implementation\logic with high-level_oordination.
- BECAUSE: {clarity, single\responsibility, testability}.

RULE: DO ISOLATE\LOGIC into {pure | stateful}.

- pure: Predictable, no side-effects (`static` methods are great for this).
- stateful: Has side-effects, interacts with external world (e.g., `fileService.SaveBaselineAsync(baseline)`).
- BECAUSE: {predictability, easy\testing\of\pure\logic}.

RULE: DO USE the Decorator\Pattern or middleware-style delegates to wrap_methods_with_cross_cutting_concerns.

- Examples: Logging decorators, retry policies.
- BECAUSE: {DRY, separation_of_concerns}.

RULE: DO USE early_returns (guard_clauses) to handle_invalid_states at the start of a method.

- AVOID: Deeply nested `if` statements for validation.
- BECAUSE: {reduces_nesting, improves_readability}.

# EXTERNAL INTERACTIONS & STATE

RULE: DO USE classes PRIMARILY_AS_WRAPPERS for external_dependencies (IO, APIs, DBs) and to hold state.

- AVOID: Mixing complex business_logic directly in these wrapper classes; PREFER delegating to purer internal services/methods.
- BECAUSE: {isolates_side_effects, promotes_functional_core, aligns_with_clean_architecture}.

RULE: DO USE a dedicated_logger_service (`ILogger<T>`). AVOID `Console.WriteLine` for application_logging.

- BECAUSE: {structured_logs, central_control, destination_flexibility}.

RULE: AVOID over-defensive_programming internally.

- TRUST your type_system (especially nullable reference types) and boundary_validation.
- AVOID: {excessive_null_checks, redundant_try_catch_blocks for expected errors}.

# CODING STYLE & CONVENTIONS

RULE: CRITICAL AVOID comments; DO WRITE self_documenting_code.

- INSTEAD_OF: Commenting `// check if user is valid`, DO create a method `bool IsValid(User user)`.
- NEVER add comments that simply reiterate what is obvious from the code.
- BECAUSE: {comments_rot, encourages_clearer_code}.

RULE: DO NOT add XML doc comments for internal APIs.

- ONLY document public APIs that are part of the user-facing surface.
- NEVER add XML doc comments for test project files/classes as it's not a public API.
- BECAUSE: {reduces_noise, focuses_documentation_effort_on_user_value}.

RULE: DO NOT add brackets for simple one-line if statements.

```csharp
// BAD
if (result)
{
    return string.Empty;
}

// GOOD
if (result)
    return string.Empty;
```
- BECAUSE: {reduces_visual_noise, improves_readability}.

RULE: DO NOT add default `private` modifier.

- The `private` modifier is implicit for class members.
- BECAUSE: {reduces_verbosity, cleaner_code}.

RULE: PREFER using `var` instead of specifying exact type for variable declarations.

```csharp
// BAD
bool left = false;
bool right = true;
string message = "hello";

// GOOD  
var left = false;
var right = true;
var message = "hello";
```
- BECAUSE: {reduces_verbosity, improves_readability, lets_compiler_infer_obvious_types}.

RULE: PREFER strongly-typed `enum`s or the "smart enum" pattern OVER raw strings or integers.

- BECAUSE: {compile-time_type_safety, improved_readability, IDE_intellisense}.

RULE: DO put `const` values in the same file as the consumer, unless it's a shared constant used across the application.

- WHEN: Constants are shared, create a dedicated `public static class Constants`.
- BECAUSE: {locality_of_reference, easier_to_find, reduces_dependency_clutter}.

RULE: DO USE dedicated configuration_objects for method_signatures WHEN params.count \>= 3.

- `MyMethod(MyMethodOptions options)` OVER `MyMethod(string param1, int param2, bool param3)`.
- BECAUSE: {readability, extensibility, no_param_order_memorization}.

# NAMING CONVENTIONS

RULE: ADHERE_TO_CSHARP_NAMING {
class, record, struct, enum, delegate: `PascalCase`
interface: `IPascalCase`
method, property, event, public field: `PascalCase`
method\parameter, local\variable: `camelCase`
private\or\internal\field: `camelCase`
constant: `PascalCase`
namespace: `PascalCase.PascalCase`
project, filename: `PascalCase.cs`
boolean\property/variable: is/has/can/should =\> `IsLoading`, `HasError`, `CanExecute`
async\method: Suffix with `Async` =\> `GetDataAsync()`
}

## Learnings Protocol

**CRITICAL**: Both the main agent and any subagents (spawned via a Task tool) must maintain the `learnings.md` knowledge base.

### What to Log:

- **Gotchas**: Unexpected behaviors, edge cases, tricky bugs (e.g., `ConfigureAwait(false)` issues).
- **Judgement calls**: Architecture decisions, trade-offs made (e.g., choosing NUnit over xUnit, System.Threading.Channels over TPL Dataflow).
- **File discoveries**: Important files found, solution/project structure insights.
- **Problems solved**: Non-obvious solutions, workarounds (e.g., a clever LINQ query).
- **Plan deviations**: Things not anticipated by the original plan.
- **Interesting findings**: Performance insights, useful NuGet packages, C\# patterns discovered.

### Rules:

- The `learnings.md` file is organized by topic (e.g., "Project & Solution Structure", "MSBuild Source Rewriter").
- Add new learnings as bullet points to the most relevant existing topic section.
- If no relevant topic exists, create a new `## Topic Name` section.
- Keep entries extremely concise - one line per insight.
- Only log unique, non-obvious information.
- Focus on future value for similar tasks.
- Subagents MUST update this file before reporting success.

# CRITICAL MEMORIES

- NEVER use underscores for private/internal members
- NEVER create coverage reports on top level. Always specify subfolder under ./TestResults dir

# BUG HUNTING METHODOLOGY

### Systematic Bug Hunt Algorithm:
1. **Fix the bug**: Add failing test first and then fix the bug
2. **Think other places where similar bug could happen**: Add failing tests and fix the bug
3. **Check test coverage**: Run tests with coverage and verify branch coverage if the failing test was a missing test

### Test Coverage Gaps to Watch:
- Only testing negative scenarios (failures/errors) without positive scenarios (success) or other way around
- Missing tests for the "happy path" where operations should succeed

# Documentation Standards

## Required Elements for All Public APIs
- `<summary>` - Clear, concise description of purpose
- `<param>` - For all parameters with validation rules
- `<returns>` - For all methods with return values
- `<exception>` - For all thrown exceptions
- `<remarks>` - Performance, threading, and usage guidance
- `<example>` - Practical code examples

## Formatting Guidelines
- Use `<see cref="">` for cross-references
- Use `<paramref name="">` for parameter references
- Use `<typeparamref name="">` for generic type references
- Use `<code>` blocks for inline code
- Use `<para>` for paragraph breaks in remarks
- Use `<list type="bullet|number">` for structured information

## Content Guidelines
- **CRITICAL**: Focus on practical usage scenarios
- Include performance implications
- **IMPORTANT**: Document threading and concurrency behavior
- Provide realistic, copy-pasteable examples
- **CRITICAL**: Explain the "why" not just the "what"
- **CRITICAL**: Always write XML doc comments from the user's perspective, not implementation details
- Cover common pitfalls and best practices

- ## Performance Documentation Template

Each performance-sensitive class should include this section:

```xml
/// <remarks>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><strong>Throughput</strong>: Up to X items/second depending on action complexity</item>
/// <item><strong>Memory</strong>: Bounded by capacity setting; approximately Y bytes per queued item</item>
/// <item><strong>Latency</strong>: Minimal queuing delay under normal load; back-pressure under overload</item>
/// <item><strong>Concurrency</strong>: Thread-safe with configurable parallelism</item>
/// </list>
/// <para><strong>Threading Model:</strong></para>
/// <para>
/// All operations are thread-safe. Items are processed on ThreadPool threads with degree of parallelism
/// controlling the maximum number of concurrent operations. Completion and metrics can be accessed 
/// from any thread safely.
/// </para>
/// </remarks>
```

## Best Practices Documentation Template

Each main class should include this section:

```xml
/// <remarks>
/// <para><strong>Best Practices:</strong></para>
/// <list type="number">
/// <item><strong>Always Complete</strong>: Call Complete() and await Completion for proper shutdown</item>
/// <item><strong>Monitor Metrics</strong>: Use Block properties to identify bottlenecks</item>  
/// <item><strong>Configure Capacity</strong>: Set bounded capacity based on memory constraints</item>
/// <item><strong>Handle Exceptions</strong>: Implement proper error handling in actions</item>
/// <item><strong>Use Cancellation</strong>: Support cancellation tokens for responsive shutdown</item>
/// </list>
/// </remarks>
```