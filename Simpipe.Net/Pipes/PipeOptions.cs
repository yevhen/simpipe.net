namespace Simpipe.Pipes;

public record PipeOptions<T>(
    string Id,
    Func<T, bool>? Filter = null,
    Func<T, Pipe<T>?>? Route = null
);