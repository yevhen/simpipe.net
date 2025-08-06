using Simpipe.Blocks;

namespace Simpipe.Pipes;

public record PipeOptions<T>(
    string Id,
    BlockAction<T> Action,
    Func<T, bool>? Filter,
    Func<T, Pipe<T>?>? Route
);