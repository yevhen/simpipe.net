namespace Simpipe.Pipes;

public record PipeOptions<T>(
    string Id,
    PipeAction<T> Action,
    Func<T, bool>? Filter,
    Func<T, Pipe<T>?>? Route
);