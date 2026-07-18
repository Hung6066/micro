namespace His.Hope.Infrastructure.Qos;

/// <summary>
/// Constants for the 5-tier request priority system (P0–P4).
/// Used across K8s PriorityClasses, HTTP header propagation,
/// gRPC metadata, and RabbitMQ message headers.
/// </summary>
public static class PriorityConstants
{
    /// <summary>
    /// P0 — Critical system operations (life-critical alerts, emergency overrides).
    /// K8s PriorityClass value: 1,000,000 (PreemptLowerPriority).
    /// Admission: always admitted.
    /// </summary>
    public const string P0 = "P0";

    /// <summary>
    /// P1 — Interactive user requests (patient check-in, physician orders).
    /// K8s PriorityClass value: 100,000 (default).
    /// Admission: always admitted.
    /// </summary>
    public const string P1 = "P1";

    /// <summary>
    /// P2 — Business-as-usual operations (scheduled reports, background sync).
    /// K8s PriorityClass value: 10,000.
    /// Admission: admitted if P0+P1+P2 active &lt; 70% of max.
    /// </summary>
    public const string P2 = "P2";

    /// <summary>
    /// P3 — Batch processing (analytics exports, data migration jobs).
    /// K8s PriorityClass value: 1,000.
    /// Admission: admitted if total active &lt; 50% of max.
    /// </summary>
    public const string P3 = "P3";

    /// <summary>
    /// P4 — Best-effort workloads (non-urgent maintenance, low-priority sync).
    /// K8s PriorityClass value: 100.
    /// Admission: admitted if total active &lt; 50% of max.
    /// </summary>
    public const string P4 = "P4";

    /// <summary>
    /// All priority levels ordered from highest (P0) to lowest (P4).
    /// Useful for iteration and ranking comparisons.
    /// </summary>
    public static readonly IReadOnlyList<string> AllPriorities = [P0, P1, P2, P3, P4];

    /// <summary>
    /// The HTTP header name used to convey the request priority.
    /// Value: <c>X-Priority</c>.
    /// </summary>
    public const string HeaderName = "X-Priority";

    /// <summary>
    /// The default priority assigned when no <c>X-Priority</c> header is present.
    /// </summary>
    public const string DefaultPriority = P1;

    /// <summary>
    /// The <see cref="HttpContext.Items"/> key used to store the resolved priority
    /// for the current request.
    /// </summary>
    public const string ContextItemsKey = "Priority";

    /// <summary>
    /// Returns the numeric rank of a priority string.
    /// Lower rank = higher priority (P0=0, P1=1, ..., P4=4).
    /// Returns 1 (P1) for unknown values as a safe default.
    /// </summary>
    public static int GetRank(string priority) => priority switch
    {
        P0 => 0,
        P1 => 1,
        P2 => 2,
        P3 => 3,
        P4 => 4,
        _ => 1 // safe default for unrecognised values
    };

    /// <summary>
    /// Returns true if the priority string represents a high-priority request
    /// (P0 or P1) that should always be admitted.
    /// </summary>
    public static bool IsHighPriority(string priority) =>
        priority is P0 or P1;
}
