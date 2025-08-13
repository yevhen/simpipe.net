# SharpAssert

A pytest inspired assertion library for .NET with no special syntax.

## Overview

SharpAssert provides rich assertion diagnostics by automatically transforming your assertion expressions at compile time using MSBuild source rewriting, giving you detailed failure messages with powerful expression analysis.

```csharp
using static SharpAssert.Sharp;

var items = new[] { 1, 2, 3 };
var target = 4;

Assert(items.Contains(target));
// Assertion failed: items.Contains(target) at MyTest.cs:15
// items:  [1, 2, 3]  
// target: 4
// Result: false
```

## Quick Start

### 1. Install Package

```bash
dotnet add package SharpAssert
```

### 2. Use SharpAssert in Your Tests

```csharp
using static SharpAssert.Sharp;

[Test]
public void Should_find_matching_item()
{
    var users = new[] { "Alice", "Bob", "Charlie" };
    var searchName = "David";
    
    Assert(users.Contains(searchName));
    // Assertion failed: users.Contains(searchName)
    // users: ["Alice", "Bob", "Charlie"]
    // searchName: "David"  
    // Result: false
}
```

### Custom Error Messages

SharpAssert's `Assert()` method supports custom error messages as a second parameter (just like NUnit's Assert()).

```csharp
Assert(user.IsActive, $"User {user.Name} should be active for this operation");
```

### Asserting exceptions

```csharp
using static SharpAssert.Sharp;

[Test]
public async Task Throws_catch_exceptions_in_exception_result()
{
    // Thows returns ExceptionResult which allows using them as condition in Assert
    Assert(Throws<ArgumentException>(()=> new ArgumentException("foo")));
    Assert(Throws<ArgumentException>(()=> new ArgumentNullException("bar"))); // will throw unexpected exception
    Assert(!Throws<ArgumentException>(()=> {})); // negative assertion via C# not syntax 

    var ex = Throws<ArgumentException>(()=> new ArgumentException("baz")); // always returns ExceptionResult
    Assert(ex.Exception.ArgumentName == "baz"); // get thrown exception and assert on any custom property

    Assert(Throws<ArgumentException>(()=> 
        new ArgumentException("baz")).Data == "baz"); // shortcut form to assert on exception Data property

    Assert(Throws<ArgumentException>(()=> 
        new ArgumentException("bar")).Message.Contains("bar")); // shortcut form to assert on exception Message
    
    // async version
    Assert(await ThrowsAsync<ArgumentException>(()=> 
        Task.Run(() => throw ArgumentException("async"))));

    var ex = ThrowsAsync<ArgumentException>(()=> Task.Run(() => throw ArgumentException("async"))); // always returns ExceptionResult
    Assert(ex.Message.Contains("async")); // assert on message using shortcut on ExceptionResult 
}
```

## Architecture

SharpAssert is built on modern .NET technologies:

- **MSBuild Source Rewriting** - Compile-time code transformation
- **Roslyn Syntax Analysis** - Advanced C# code parsing and generation  
- **Expression Trees** - Runtime expression analysis
- **CallerArgumentExpression** - Fallback for edge cases
- **PowerAssert Backend** - Automatic fallback for complex scenarios

### PowerAssert Integration

SharpAssert includes PowerAssert integration and also uses it as a fallback mechanism for not yet implemented features.

To force PowerAssert for all assertions:

```xml
<PropertyGroup>
  <UsePowerAssert>true</UsePowerAssert>
</PropertyGroup>
```

## Known issues

- Warning about legacy RID used by PowerAssert (dependency). Fix by adding to project properties:
```xml
  <!-- Suppress NETSDK1206 warning from PowerAssert's Libuv dependency -->
  <NoWarn>$(NoWarn);NETSDK1206</NoWarn>
```
- Collection initializers could not be used in expression trees. Compiler limitation. Use `new[]{1,2,3}` instead of `[1, 2, 3]`

## Troubleshooting

### Rewriter not working
1. Verify `SharpAssert` package is installed (SharpAssert.Runtime comes automatically)
2. Ensure `using static Sharp;` import

### No detailed error messages
1. Check build output contains: "SharpAssert: Rewriting X source files"
2. Verify rewritten files exist in `obj/Debug/net9.0/SharpRewritten/`
3. Ensure `SharpInternal.Assert` calls are being made (check generated code)
4. Look for #line directives in generated files