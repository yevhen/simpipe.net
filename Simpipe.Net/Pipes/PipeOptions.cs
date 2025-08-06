namespace Simpipe.Pipes;

public class PipeOptions<T>
{
    protected string id = "pipe-id";
    protected Func<T, bool>? filter;
    protected Func<T, IPipe<T>>? route;

    public string Id() => id;
    public Func<T, bool>? Filter() => filter;
    public Func<T, IPipe<T>?>? Route() => route;
}