using System.Diagnostics;
using His.Hope.Bff.Core.Telemetry;

namespace His.Hope.Bff.Core.Aggregation;

public static class ParallelAggregationExecutor
{
    public sealed record AggregationExecutionResult(
        IReadOnlyDictionary<string, object> Successes,
        DegradedField[] Failures);

    public static async Task<AggregationExecutionResult> RunAsync(
        Dictionary<string, Func<Task<object>>> tasks,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new Dictionary<string, object>();
        var failures = new List<DegradedField>();

        var taskList = tasks.Select(async kvp =>
        {
            try
            {
                var result = await kvp.Value();
                lock (results) { results[kvp.Key] = result; }
            }
            catch (Exception ex)
            {
                BffMetrics.AggregationDegraded.Add(1);
                lock (failures)
                {
                    failures.Add(new DegradedField(kvp.Key, ex.Message,
                        Activity.Current?.Id ?? "unknown"));
                }
            }
        });

        await Task.WhenAll(taskList);
        sw.Stop();

        BffMetrics.AggregationDuration.Record(sw.ElapsedMilliseconds);

        return new AggregationExecutionResult(results, failures.ToArray());
    }
}
