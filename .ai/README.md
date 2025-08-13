# SharpAssert

A pytest inspired assertion library for .NET with no special syntax.

## Overview

SharpAssert provides rich assertion diagnostics by automatically transforming your assertion expressions at compile time using MSBuild source rewriting, giving you detailed failure messages with powerful expression analysis.

```csharp
using static Sharp;

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
using static Sharp;

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

```csharp
Assert(user.IsActive, $"User {user.Name} should be active for this operation");
```

### Asserting exceptions

```csharp
using static Sharp;

[Test]
public async Task Throws_catch_exceptions_in_exception_result()
{
    // Thows returns ExceptionResult which allows using them as condition in Assert
    Assert(Throws<ArgumentException>(()=> new ArgumentException("foo")));
    Assert(Throws<ArgumentException>(()=> new ArgumentNullException("bar"))); // will throw unexpected exception
    Assert(!Throws<ArgumentException>(()=> {})); // negative assertion via C# not syntax 

    Assert(Throws<ArgumentException>(()=> 
        new ArgumentException("baz")).Exception.ArgumentName == "baz"); // assert on any custom exception property

    Assert(Throws<ArgumentException>(()=> 
        new ArgumentException("baz")).Data == "baz"); // shortcut form to assert on exception Data property

    Assert(Throws<ArgumentException>(()=> 
        new ArgumentException("bar")).Message.Contains("bar")); // shortcut form to assert on exception Message
    
    // async version
    Assert(await ThrowsAsync<ArgumentException>(async ()=> 
        await Task.Run(() => throw ArgumentException("async")))); // shortcut form to assert on exception Message
 
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

SharpAssert includes PowerAssert as an intelligent fallback mechanism. 
When SharpAssert encounters expressions it doesn't yet fully support, it automatically delegates to PowerAssert to ensure you always get meaningful diagnostics. 
This happens transparently - you'll still get detailed error messages regardless of the underlying engine.

To force PowerAssert for all assertions (useful for comparison or debugging):

```xml
<PropertyGroup>
  <UsePowerAssert>true</UsePowerAssert>
</PropertyGroup>
```

## Troubleshooting

### Rewriter not working
1. Verify `SharpAssert` package is installed (SharpAssert.Runtime comes automatically)
2. Ensure `using static Sharp;` import

### No detailed error messages
1. Check build output contains: "SharpAssert: Rewriting X source files"
2. Verify rewritten files exist in `obj/Debug/net9.0/SharpRewritten/`
3. Ensure `SharpInternal.Assert` calls are being made (check generated code)
4. Look for #line directives in generated files