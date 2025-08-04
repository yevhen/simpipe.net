# Learnings

## TDD Implementation Insights

- **Primary constructor pattern**: C# 12 primary constructors work well for simple cases but become unwieldy with multiple constructor overloads - better to use explicit constructors for complex scenarios
- **Async/Sync method unification**: Using `Func<T, Task>?` and `Action<T>?` fields allows supporting both sync and async actions in the same class elegantly
- **Semaphore for concurrency control**: `SemaphoreSlim` with `Task.Run` provides clean parallel processing with bounded concurrency
- **Test design for concurrency**: Using `Interlocked` operations and tracking `maxConcurrency` provides reliable verification of parallel execution

## Channel-Based Architecture

- **Channel namespace**: New implementation uses `Simpipe.Channels` namespace to distinguish from legacy TPL Dataflow implementation
- **Constructor overloading**: Multiple constructors support backward compatibility while adding new functionality
- **Sequential vs parallel paths**: Clean separation between sequential (parallelism=1) and parallel processing logic improves readability

## Quality Process

- **NUnit analyzer warnings**: Many existing tests use classic Assert methods instead of constraint model - not critical but noted for future cleanup
- **Build + test cycle**: Following strict TDD cycle (red-green-refactor) with immediate testing after each change ensures stability
- **Git commit patterns**: Repository follows descriptive commit messages with implementation context