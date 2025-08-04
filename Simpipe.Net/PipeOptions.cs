namespace Youscan.Core.Pipes
{
    public class PipeOptions<T>
    {
        protected string id;
        protected Func<T, bool>? filter;
        protected Func<T, IPipe<T>>? route;

        public PipeOptions() => 
            id = "pipe-id";

        public string Id() => id;
        public Func<T, bool>? Filter() => filter;
        public Func<T, IPipe<T>?>? Route() => route;
    }

    public class GroupPipeOptions<T> : PipeOptions<T>
    {
        CancellationToken cancellationToken;
        int degreeOfParallelism = 1;
        int? boundedCapacity;

        public GroupPipeOptions<T> Id(string value)
        {
            id = value;
            return this;
        }

        public GroupPipeOptions<T> Filter(Func<T, bool> value)
        {
            filter = value;
            return this;
        }

        public GroupPipeOptions<T> CancellationToken(CancellationToken value)
        {
            cancellationToken = value;
            return this;
        }

        public GroupPipeOptions<T> BoundedCapacity(int? value)
        {
            boundedCapacity = value;
            return this;
        }

        public GroupPipeOptions<T> DegreeOfParallelism(int value)
        {
            degreeOfParallelism = value;
            return this;
        }

        public int? BoundedCapacity() => boundedCapacity;
        public int DegreeOfParallelism() => degreeOfParallelism;
        public CancellationToken CancellationToken() => cancellationToken;

        public GroupPipe<T> ToPipe() => new(this);
    }

    public sealed class ActionPipeOptions<T> : PipeOptions<T>
    {
        readonly PipeAction<T> action;
        int? boundedCapacity;
        CancellationToken cancellationToken;
        int degreeOfParallelism = 1;

        public ActionPipeOptions(PipeAction<T> action)
        {
            this.action = action;
        }

        public ActionPipeOptions<T> Id(string value)
        {
            id = value;
            return this;
        }

        public ActionPipeOptions<T> Filter(Func<T, bool> value)
        {
            filter = value;
            return this;
        }

        public ActionPipeOptions<T> Route(Func<T, IPipe<T>> value)
        {
            route = value;
            return this;
        }

        public ActionPipeOptions<T> CancellationToken(CancellationToken value)
        {
            cancellationToken = value;
            return this;
        }

        public ActionPipeOptions<T> DegreeOfParallelism(int value)
        {
            degreeOfParallelism = value;
            return this;
        }

        public ActionPipeOptions<T> BoundedCapacity(int? value)
        {
            boundedCapacity = value;
            return this;
        }

        public ActionPipe<T> ToPipe() => (ActionPipe<T>) new ActionPipe<T>(this);

        public static implicit operator Pipe<T>(ActionPipeOptions<T> options) => options.ToPipe();
        public PipeAction<T> Action() => action;
        public int? BoundedCapacity() => boundedCapacity;
        public CancellationToken CancellationToken() => cancellationToken;
        public int DegreeOfParallelism() => degreeOfParallelism;
    }

    public sealed class BatchPipeOptions<T> : PipeOptions<T>
    {
        readonly int batchSize;
        TimeSpan batchTriggerPeriod;
        readonly PipeAction<T> action;
        int? boundedCapacity;
        CancellationToken cancellationToken;
        int degreeOfParallelism = 1;

        public BatchPipeOptions(int batchSize, PipeAction<T> action)
        {
            this.action = action;
            this.batchSize = batchSize;
        }

        public int BatchSize() => batchSize;
        public TimeSpan BatchTriggerPeriod() => batchTriggerPeriod;

        public BatchPipeOptions<T> Id(string value)
        {
            id = value;
            return this;
        }

        public BatchPipeOptions<T> Filter(Func<T, bool> value)
        {
            filter = value;
            return this;
        }

        public BatchPipeOptions<T> BatchTriggerPeriod(TimeSpan value)
        {
            batchTriggerPeriod = value;
            return this;
        }

        public BatchPipeOptions<T> CancellationToken(CancellationToken value)
        {
            cancellationToken = value;
            return this;
        }

        public BatchPipeOptions<T> DegreeOfParallelism(int value)
        {
            degreeOfParallelism = value;
            return this;
        }

        public BatchPipeOptions<T> BoundedCapacity(int? value)
        {
            boundedCapacity = value;
            return this;
        }

        public BatchPipe<T> ToPipe() => new(this);

        public static implicit operator Pipe<T>(BatchPipeOptions<T> options) => options.ToPipe();
        public PipeAction<T> Action() => action;
        public int? BoundedCapacity() => boundedCapacity;
        public CancellationToken CancellationToken() => cancellationToken;
        public int DegreeOfParallelism() => degreeOfParallelism;
    }
    
    public sealed class DynamicBatchPipeOptions<T> : PipeOptions<T>
    {
        TimeSpan initialBatchInterval = TimeSpan.FromMilliseconds(100);
        TimeSpan maxBatchInterval = TimeSpan.FromSeconds(30);
        int maxBatchSize = 1000;
        readonly PipeAction<T> action;
        CancellationToken cancellationToken;
        int degreeOfParallelism = 1;

        public DynamicBatchPipeOptions(PipeAction<T> action)
        {
            this.action = action;
        }

        public TimeSpan InitialBatchInterval() => initialBatchInterval;
        public TimeSpan MaxBatchInterval() => maxBatchInterval;
        public int MaxBatchSize() => maxBatchSize;

        public DynamicBatchPipeOptions<T> Id(string value)
        {
            id = value;
            return this;
        }

        public DynamicBatchPipeOptions<T> Filter(Func<T, bool> value)
        {
            filter = value;
            return this;
            
        }

        public DynamicBatchPipeOptions<T> MaxBatchSize(int value)
        {
            maxBatchSize = value;
            return this;
        }
        
        public DynamicBatchPipeOptions<T> MaxBatchInterval(TimeSpan value)
        {
            maxBatchInterval = value;
            return this;
        }
        
        public DynamicBatchPipeOptions<T> InitialBatchInterval(TimeSpan value)
        {
            initialBatchInterval = value;
            return this;
        }
        
        public DynamicBatchPipeOptions<T> DegreeOfParallelism(int value)
        {
            degreeOfParallelism = value;
            return this;
        }

        public DynamicBatchPipeOptions<T> CancellationToken(CancellationToken value)
        {
            cancellationToken = value;
            return this;
        }

        public DynamicBatchPipe<T> ToPipe() => new(this);

        public static implicit operator Pipe<T>(DynamicBatchPipeOptions<T> options) => options.ToPipe();
        public PipeAction<T> Action() => action;
        public CancellationToken CancellationToken() => cancellationToken;
        public int DegreeOfParallelism() => degreeOfParallelism;
    }
}