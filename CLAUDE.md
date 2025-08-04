# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Development Commands

### Build
```bash
dotnet build                          # Build the solution
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

Simpipe.Net is a .NET 9.0 library implementing a pipeline pattern using TPL Dataflow. The library provides composable pipe components for building data processing pipelines.

### Core Components

- **IPipe<T>** (Pipe.cs:5-24): Base interface for all pipe components
  - Supports routing through `LinkTo()` methods
  - Provides completion tracking and item counting
  - Uses TPL Dataflow's `ITargetBlock<T>` as underlying implementation

- **Pipe<T>** (Pipe.cs:40+): Abstract base implementation providing common pipe functionality
  - Handles action execution with optional filtering
  - Manages input/output/working counts
  - Supports completion propagation

- **Pipeline<T>** (Pipeline.cs): Container for managing a sequence of connected pipes
  - Maintains pipes by ID and in order
  - Handles automatic linking between pipes
  - Provides completion tracking for the entire pipeline

### Specialized Pipe Types

- **ActionPipe<T>**: Executes an action for each item
- **BatchPipe<T>**: Groups items into batches before processing
- **DynamicBatchPipe<T>**: Batches items with dynamic intervals
- **GroupPipe<T>**: Groups items by key
- **BlockPipeAdapter<T>**: Adapts TPL Dataflow blocks to pipe interface
- **RoutingBlock<T>**: Provides conditional routing between pipes

### Key Design Patterns

1. **Fluent Builder Pattern**: `PipeBuilder<T>` provides fluent API for pipe creation
2. **Decorator Pattern**: Pipes can be wrapped and composed
3. **TPL Dataflow Integration**: All pipes wrap dataflow blocks for concurrent execution
4. **Completion Propagation**: Automatic completion handling through the pipeline

## Testing

Tests use NUnit 3.14 with FluentAssertions for assertions. Test structure:
- Fixtures per pipe type (e.g., PipeFixture.cs, BatchPipeFixture.cs)
- Setup/TearDown pattern for cancellation token management
- Mock implementations in PipeMock.cs for testing
- Integration tests in IntegrationFixture.cs

Note: The namespace `Youscan.Core.Pipes` is used throughout the codebase instead of `Simpipe.Net`.