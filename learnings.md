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
- **Flaky test discovered**: `BatchPipeFixture.Input_count()` has a race condition - fails when run with other tests but passes in isolation. Uses TaskCompletionSource blocker and AutoResetEvent timing that creates race condition between batch processing and InputCount checking. Pre-existing issue not related to PipeMock migration. Needs separate investigation.

## ActionBlock Implementation Insights

- **In-place mutation support**: ActionBlock already supports in-place mutation of reference types - no additional implementation needed for Increment 4
- **Constructor usage**: ActionBlock requires both action and done parameters for mutation scenarios, using the 3-parameter constructor
- **Channel completion**: Must complete intermediate channels manually after blocks finish to avoid deadlocks in pipeline scenarios

## BatchBlock Implementation Insights

- **Batching strategy**: Simple List<T> accumulation with ToArray() when full or at completion works efficiently for most batch sizes
- **Final batch handling**: Critical to emit incomplete final batches when input channel completes - don't lose data
- **Primary constructor pattern**: BatchBlock<T>(reader, batchSize, done) provides clean API for pure batching without action processing
- **Done callback signature**: Action<T[]> callback receives arrays, not individual items, distinguishing it from ActionBlock's item-by-item processing
- **Timer-based flushing**: System.Threading.Timer with proper disposal in finally block provides reliable time-based batch flushing
- **Constructor overloading**: Multiple constructors for backward compatibility while adding time-based flushing functionality
- **Timer lifecycle**: Start timer on first item, reset after flush, dispose in finally block - handles race conditions between size and time-based flushing