# Learnings

## TDD Implementation Insights

- **Primary constructor pattern**: C# 12 primary constructors work well for simple cases but become unwieldy with multiple constructor overloads - better to use explicit constructors for complex scenarios
- **Async/Sync method unification**: Using `Func<T, Task>?` and `Action<T>?` fields allows supporting both sync and async actions in the same class elegantly
- **Test design for concurrency**: Using `Interlocked` operations and tracking `maxConcurrency` provides reliable verification of parallel execution

## Quality Process

- **NUnit analyzer warnings**: Use constraint model instead of classic `Assert.AreEqual()`, etc
- **Build + test cycle**: Following strict TDD cycle (red-green-refactor) with immediate testing after each change ensures stability
- **Git commit patterns**: Repository follows descriptive commit messages with implementation context