namespace Youscan.Core.Pipes
{
    /// <summary>
    /// Implemented as descibed by whitepaper "Adaptive Stream Processing using Dynamic Batch Sizing"
    /// Link: https://www2.eecs.berkeley.edu/Pubs/TechRpts/2014/EECS-2014-133.html
    /// </summary>
    public class DynamicBatchIntervalController
    {
        const double q = 0.7;   // slack to relax stability condition line in order to cope with noise
        const double r = 0.25;  // reduction factor for superlinear workloads

        readonly int initialInterval;
        readonly int maxInterval;
        readonly Func<int, int> round;

        int xLast;    // last interval
        int x2Last;   // 2nd-last interval

        int pLast;    // last processing time
        int p2Last;   // 2nd-last processing time

        public DynamicBatchIntervalController(int initialInterval, int? roundToMultipleOf = null, bool roundUp = true, int maxInterval = int.MaxValue)
        {
            this.initialInterval = initialInterval;
            this.maxInterval = maxInterval;
            round = SetupRounding(roundToMultipleOf, roundUp);
        }

        public void Reset() => x2Last = xLast = p2Last = pLast = 0;

        public void RecordLastBatch(int interval, int processingTime)
        {
            x2Last = xLast;
            p2Last = pLast;

            xLast = interval;
            pLast = processingTime;
        }

        public int NextBatchInterval()
        {
            if (xLast == 0)
                return round(initialInterval);      // initial guess, start small (TCP Slow Start)

            if (x2Last == 0)
                return round(initialInterval * 10); // grow exponentially to test ideal batch interval hypothesis

            if (xLast > maxInterval && x2Last > maxInterval)
                return round(initialInterval);     // if last 2 were higher than max reset hypothesis and start over

            return Math.Min(round(CalculateNextBatchInterval()), maxInterval);
        }

        int CalculateNextBatchInterval()
        {
            var xSmall = Math.Min(xLast, x2Last);
            var xLarge = Math.Max(xLast, x2Last);

            var pSmall = xSmall == xLast ? pLast : p2Last;
            var pLarge = xLarge == xLast ? pLast : p2Last;

            // test for superlinear workload
            if ((double)pLarge / xLarge > (double)pSmall / xSmall && pLast > q * xLast)
                return (int)((1 - r) * xSmall); // reduce batch interval

            // otherwise use Fixed-Point iteration
            return (int)(pLast / q);
        }

        static Func<int, int> SetupRounding(int? roundToMultipleOf, bool roundUp)
        {
            if (roundToMultipleOf == null)
                return x => x;

            if (roundUp)
                return x => RoundUpTo(x, roundToMultipleOf.Value);

            return x => RoundDownTo(x, roundToMultipleOf.Value);

            int RoundUpTo(int value, int multiple)
            {
                if (value % multiple == 0)
                    return value;

                return multiple - value % multiple + value;
            }

            int RoundDownTo(int value, int multiple)
            {
                if (value < multiple)
                    return multiple;

                if (value % multiple == 0)
                    return value;

                return value - value % multiple;
            }
        }
    }
}