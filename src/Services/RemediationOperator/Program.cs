using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using k8s;
using k8s.Models;
using Prometheus;
using Serilog;
using Serilog.Events;

// ============================================================================
// His.Hope Auto-Remediation Operator
//
// Receives Alertmanager webhooks, matches alerts against RemediationPolicy
// CRDs, executes remediation actions (scale/restart/rollback/notify), and
// records an audit trail via RemediationAction CRDs.
// ============================================================================

// ---------------------------------------------------------------------------
// Program Entry Point (top-level statements must precede type declarations)
// ---------------------------------------------------------------------------

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("KubernetesClient", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", Constants.OperatorName)
    .Enrich.WithProperty("Version", Constants.OperatorVersion)
    .WriteTo.Console()
    .WriteTo.File("/var/log/remediation-operator/log-.json",
        rollingInterval: RollingInterval.Day,
        formatter: new Serilog.Formatting.Json.JsonFormatter())
    .CreateLogger();

try
{
    Log.Information("Starting {OperatorName} v{Version}", Constants.OperatorName, Constants.OperatorVersion);

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });

    builder.Host.UseSerilog();

    // -----------------------------------------------------------------------
    // Configuration
    // -----------------------------------------------------------------------
    var configSection = builder.Configuration.GetSection("RemediationOperator");
    var webhookPort = configSection.GetValue<int>("WebhookPort");
    var metricsPort = configSection.GetValue<int>("MetricsPort");
    var operatorNamespace = configSection.GetValue<string>("Namespace") ?? "his-hope";
    var cooldownDefaultMinutes = configSection.GetValue<int>("CooldownDefaultMinutes");
    var maxConcurrentActions = configSection.GetValue<int>("MaxConcurrentActions");
    var dryRun = configSection.GetValue<bool>("DryRun");
    var ipWhitelist = configSection.GetSection("AlertmanagerSourceIpWhitelist").Get<string[]>() ?? [];

    // -----------------------------------------------------------------------
    // Kubernetes Client
    // -----------------------------------------------------------------------
    var k8sConfig = KubernetesClientConfiguration.InClusterConfig();
    var k8sClient = new Kubernetes(k8sConfig);

    // -----------------------------------------------------------------------
    // Services
    // -----------------------------------------------------------------------
    var cooldownManager = new CooldownManager();
    var remediationEngine = new RemediationEngine(
        Log.Logger.ForContext<RemediationEngine>(),
        k8sClient,
        cooldownManager,
        maxConcurrentActions,
        dryRun);

    // -----------------------------------------------------------------------
    // Web Application
    // -----------------------------------------------------------------------
    builder.WebHost.UseKestrel(options =>
    {
        options.ListenAnyIP(webhookPort == 0 ? 8080 : webhookPort, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
        });
    });

    // Add metrics server on separate port
    builder.WebHost.ConfigureKestrel(options =>
    {
        // Metrics endpoint on separate port
        options.ListenAnyIP(metricsPort == 0 ? 9090 : metricsPort, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
        });
    });

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Prometheus metrics middleware (on all ports)
    app.UseHttpMetrics();
    app.UseMetricServer();

    app.UseRouting();

    // Health check endpoint
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        operatorName = Constants.OperatorName,
        version = Constants.OperatorVersion,
        timestamp = DateTime.UtcNow
    }));

    // Readiness probe
    app.MapGet("/ready", () => Results.Ok(new
    {
        status = "ready",
        timestamp = DateTime.UtcNow
    }));

    // -------------------------------------------------------------------
    // Alertmanager Webhook Receiver
    // -------------------------------------------------------------------
    app.MapPost("/api/v1/alerts", async (HttpContext httpContext) =>
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        // Optional source IP whitelisting
        if (ipWhitelist.Length > 0)
        {
            var remoteIp = httpContext.Connection.RemoteIpAddress;
            if (remoteIp is not null)
            {
                var allowed = ipWhitelist.Any(w =>
                {
                    if (IPNetwork.TryParse(w, out var network))
                        return network.Contains(remoteIp);
                    return IPAddress.TryParse(w, out var addr) && Equals(remoteIp, addr);
                });

                if (!allowed)
                {
                    Log.Warning("Alert webhook rejected from unauthorized IP: {RemoteIp}", remoteIp);
                    return Results.StatusCode(403);
                }
            }
        }

        try
        {
            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(cts.Token);

            if (string.IsNullOrWhiteSpace(body))
            {
                Log.Warning("Received empty alert webhook payload");
                return Results.BadRequest(new { error = "Empty payload" });
            }

            var payload = JsonSerializer.Deserialize<AlertmanagerWebhookPayload>(body);
            if (payload?.Alerts is null || payload.Alerts.Count == 0)
            {
                Log.Warning("Alert webhook received with no alerts");
                return Results.Ok(new { processed = 0 });
            }

            Log.Information("Received {AlertCount} alerts from Alertmanager webhook", payload.Alerts.Count);

            var processingTasks = new List<Task>();
            foreach (var alert in payload.Alerts)
            {
                processingTasks.Add(ProcessAlertSafelyAsync(remediationEngine, alert, operatorNamespace, cts.Token));
            }

            await Task.WhenAll(processingTasks);

            return Results.Ok(new { processed = payload.Alerts.Count });
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Failed to deserialize Alertmanager webhook payload");
            return Results.BadRequest(new { error = "Invalid JSON payload" });
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Alert webhook processing timed out");
            return Results.StatusCode(504);
        }
    });

    // Cooldown cleanup background task
    _ = Task.Run(async () =>
    {
        while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), app.Lifetime.ApplicationStopping);
                cooldownManager.Cleanup();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }, app.Lifetime.ApplicationStopping);

    Log.Information("Remediation Operator listening on webhook port {WebhookPort} and metrics port {MetricsPort}",
        webhookPort, metricsPort);

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Remediation Operator terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Safely process a single alert, catching and logging any errors.
/// </summary>
static async Task ProcessAlertSafelyAsync(RemediationEngine engine, AlertmanagerAlert alert, string ns, CancellationToken ct)
{
    try
    {
        await engine.ProcessAlertAsync(alert, ns, ct);
    }
    catch (Exception ex)
    {
        var alertName = alert.Labels?.GetValueOrDefault("alertname", "unknown") ?? "unknown";
        Log.Error(ex, "Failed to process alert {AlertName}", alertName);
    }
}

// ============================================================================
// Type Declarations (all declarations after top-level statements)
// ============================================================================

// ---------------------------------------------------------------------------
// Data Models
// ---------------------------------------------------------------------------

/// <summary>
/// Alertmanager webhook payload.
/// </summary>
public sealed class AlertmanagerWebhookPayload
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("groupKey")]
    public string? GroupKey { get; set; }

    [JsonPropertyName("truncatedAlerts")]
    public int TruncatedAlerts { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("receiver")]
    public string? Receiver { get; set; }

    [JsonPropertyName("groupLabels")]
    public Dictionary<string, string>? GroupLabels { get; set; }

    [JsonPropertyName("commonLabels")]
    public Dictionary<string, string>? CommonLabels { get; set; }

    [JsonPropertyName("commonAnnotations")]
    public Dictionary<string, string>? CommonAnnotations { get; set; }

    [JsonPropertyName("externalURL")]
    public string? ExternalUrl { get; set; }

    [JsonPropertyName("alerts")]
    public List<AlertmanagerAlert>? Alerts { get; set; }
}

/// <summary>
/// A single alert from the Alertmanager webhook payload.
/// </summary>
public sealed class AlertmanagerAlert
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; set; }

    [JsonPropertyName("annotations")]
    public Dictionary<string, string>? Annotations { get; set; }

    [JsonPropertyName("startsAt")]
    public DateTime? StartsAt { get; set; }

    [JsonPropertyName("endsAt")]
    public DateTime? EndsAt { get; set; }

    [JsonPropertyName("generatorURL")]
    public string? GeneratorUrl { get; set; }

    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; set; }
}

/// <summary>
/// Severity levels matching the RemediationPolicy CRD.
/// </summary>
public enum SeverityLevel
{
    P0,
    P1,
    P2,
    P3
}

/// <summary>
/// Remediation action type.
/// </summary>
public enum RemediationActionType
{
    Scale,
    Restart,
    Rollback,
    Notify
}

/// <summary>
/// Action execution result status.
/// </summary>
public enum ActionResultStatus
{
    Success,
    Failed,
    Throttled
}

/// <summary>
/// In-memory representation of a RemediationPolicy CRD.
/// </summary>
public sealed class RemediationPolicy
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string AlertName { get; set; } = string.Empty;
    public SeverityLevel Severity { get; set; } = SeverityLevel.P3;
    public string Cooldown { get; set; } = "5m";
    public int MaxConcurrency { get; set; } = 1;
    public List<RemediationActionSpec> Actions { get; set; } = [];
}

/// <summary>
/// A single remediation action definition from the policy.
/// </summary>
public sealed class RemediationActionSpec
{
    public RemediationActionType Type { get; set; }
    public string Target { get; set; } = string.Empty;
    public ActionParams? Params { get; set; }
}

/// <summary>
/// Action-specific parameters.
/// </summary>
public sealed class ActionParams
{
    public int? Replicas { get; set; }
    public string? CpuRequest { get; set; }
    public string? MemoryRequest { get; set; }
    public string? CpuLimit { get; set; }
    public string? MemoryLimit { get; set; }
    public int? Revision { get; set; }
    public string? Message { get; set; }
    public string? Channel { get; set; }
    public string? MaxUnavailable { get; set; }
}

/// <summary>
/// Result of a single remediation action execution.
/// </summary>
public sealed class ActionExecutionResult
{
    public ActionResultStatus Status { get; set; }
    public string? Error { get; set; }
    public ResourceState? BeforeState { get; set; }
    public ResourceState? AfterState { get; set; }
    public List<string>? NotificationsSent { get; set; }
}

/// <summary>
/// Snapshot of resource state before/after remediation.
/// </summary>
public sealed class ResourceState
{
    public int? Replicas { get; set; }
    public int? Revision { get; set; }
    public string? ResourceVersion { get; set; }
}

/// <summary>
/// Cooldown tracker entry.
/// </summary>
internal sealed class CooldownEntry
{
    public DateTime ExpiresAt { get; set; }
    public int ExecutionCount { get; set; }
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

internal static class Constants
{
    public const string RemediationPolicyGroup = "his-hope.io";
    public const string RemediationPolicyVersion = "v1";
    public const string RemediationPolicyPlural = "remediationpolicies";
    public const string RemediationActionPlural = "remediationactions";
    public const string OperatorName = "hishope-remediation-operator";
    public const string OperatorVersion = "1.0.0";

    // Prometheus metric names
    public const string MetricWebhookReceived = "remediation_webhook_alerts_received_total";
    public const string MetricPolicyMatched = "remediation_policy_matched_total";
    public const string MetricActionExecuted = "remediation_action_executed_total";
    public const string MetricActionDuration = "remediation_action_duration_seconds";
    public const string MetricActionErrors = "remediation_action_errors_total";
    public const string MetricThrottled = "remediation_action_throttled_total";
    public const string MetricCooldownActive = "remediation_cooldown_active";
}

// ---------------------------------------------------------------------------
// Prometheus Metrics
// ---------------------------------------------------------------------------

internal static class MetricsRegistry
{
    public static readonly Counter WebhookAlertsReceived = Metrics
        .CreateCounter(Constants.MetricWebhookReceived,
            "Total number of alerts received from Alertmanager webhook",
            new CounterConfiguration { LabelNames = ["alertname", "severity"] });

    public static readonly Counter PolicyMatched = Metrics
        .CreateCounter(Constants.MetricPolicyMatched,
            "Total number of policy matches",
            new CounterConfiguration { LabelNames = ["policy", "alertname"] });

    public static readonly Counter ActionExecuted = Metrics
        .CreateCounter(Constants.MetricActionExecuted,
            "Total number of remediation actions executed",
            new CounterConfiguration { LabelNames = ["policy", "action_type", "result"] });

    public static readonly Histogram ActionDuration = Metrics
        .CreateHistogram(Constants.MetricActionDuration,
            "Duration of remediation actions in seconds",
            new HistogramConfiguration
            {
                LabelNames = ["policy", "action_type"],
                Buckets = [0.1, 0.5, 1, 2, 5, 10, 30, 60]
            });

    public static readonly Counter ActionErrors = Metrics
        .CreateCounter(Constants.MetricActionErrors,
            "Total number of remediation action errors",
            new CounterConfiguration { LabelNames = ["policy", "action_type", "error_type"] });

    public static readonly Counter Throttled = Metrics
        .CreateCounter(Constants.MetricThrottled,
            "Total number of throttled remediation actions",
            new CounterConfiguration { LabelNames = ["policy", "reason"] });

    public static readonly Gauge CooldownActive = Metrics
        .CreateGauge(Constants.MetricCooldownActive,
            "Indicates if cooldown is active (1) or not (0) for a policy",
            new GaugeConfiguration { LabelNames = ["policy"] });
}

// ---------------------------------------------------------------------------
// Severity Helpers
// ---------------------------------------------------------------------------

internal static class SeverityHelper
{
    private static readonly Dictionary<string, SeverityLevel> LevelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["P0"] = SeverityLevel.P0,
        ["P1"] = SeverityLevel.P1,
        ["P2"] = SeverityLevel.P2,
        ["P3"] = SeverityLevel.P3,
        ["critical"] = SeverityLevel.P0,
        ["warning"] = SeverityLevel.P2,
        ["info"] = SeverityLevel.P3,
    };

    public static SeverityLevel Parse(string? severity) =>
        severity is not null && LevelMap.TryGetValue(severity, out var level) ? level : SeverityLevel.P3;

    /// <summary>
    /// Returns true if alertSeverity is >= policySeverity (P0 > P1 > P2 > P3).
    /// </summary>
    public static bool MeetsThreshold(SeverityLevel alertSeverity, SeverityLevel policySeverity) =>
        (int)alertSeverity <= (int)policySeverity;
}

// ---------------------------------------------------------------------------
// Cooldown Manager
// ---------------------------------------------------------------------------

internal sealed class CooldownManager
{
    private readonly ConcurrentDictionary<string, CooldownEntry> _cooldowns = new(StringComparer.OrdinalIgnoreCase);

    public bool TryEnter(string policyName, string alertName, TimeSpan cooldown)
    {
        var key = $"{policyName}:{alertName}";
        var now = DateTime.UtcNow;

        var entry = _cooldowns.GetOrAdd(key, _ => new CooldownEntry
        {
            ExpiresAt = now.Add(cooldown),
            ExecutionCount = 1
        });

        lock (entry)
        {
            if (now < entry.ExpiresAt)
            {
                entry.ExecutionCount++;
                return false;
            }

            entry.ExpiresAt = now.Add(cooldown);
            entry.ExecutionCount = 1;
            return true;
        }
    }

    public void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _cooldowns)
        {
            if (now >= kvp.Value.ExpiresAt)
            {
                _cooldowns.TryRemove(kvp.Key, out _);
            }
        }
    }

    public bool IsInCooldown(string policyName, string alertName)
    {
        var key = $"{policyName}:{alertName}";
        return _cooldowns.TryGetValue(key, out var entry) && DateTime.UtcNow < entry.ExpiresAt;
    }
}

// ---------------------------------------------------------------------------
// Remediation Engine
// ---------------------------------------------------------------------------

internal sealed class RemediationEngine
{
    private readonly Serilog.ILogger _logger;
    private readonly IKubernetes _k8s;
    private readonly CooldownManager _cooldownManager;
    private readonly SemaphoreSlim _globalThrottle;
    private readonly bool _dryRun;
    private static readonly HttpClient _httpClient = new();

    public RemediationEngine(Serilog.ILogger logger, IKubernetes k8s, CooldownManager cooldownManager, int maxConcurrent, bool dryRun)
    {
        _logger = logger;
        _k8s = k8s;
        _cooldownManager = cooldownManager;
        _globalThrottle = new SemaphoreSlim(maxConcurrent);
        _dryRun = dryRun;
    }

    /// <summary>
    /// Process a single alert from Alertmanager against all matching policies.
    /// </summary>
    public async Task ProcessAlertAsync(AlertmanagerAlert alert, string alertNamespace, CancellationToken ct)
    {
        if (alert.Labels is null)
        {
            _logger.Warning("Alert received with no labels, skipping");
            return;
        }

        alert.Labels.TryGetValue("alertname", out var alertName);
        alert.Labels.TryGetValue("severity", out var severityStr);

        if (string.IsNullOrEmpty(alertName))
        {
            _logger.Warning("Alert received without alertname label, skipping");
            return;
        }

        var alertSeverity = SeverityHelper.Parse(severityStr);

        MetricsRegistry.WebhookAlertsReceived
            .WithLabels(alertName, alertSeverity.ToString())
            .Inc();

        // Fetch matching RemediationPolicies from the namespace
        var policies = await GetMatchingPoliciesAsync(alertName, alertSeverity, alertNamespace, ct);

        if (policies.Count == 0)
        {
            _logger.Debug("No matching RemediationPolicy found for alert {AlertName} severity {Severity}",
                alertName, alertSeverity);
            return;
        }

        foreach (var policy in policies)
        {
            MetricsRegistry.PolicyMatched
                .WithLabels(policy.Name, alertName)
                .Inc();

            await ExecutePolicyAsync(policy, alert, ct);
        }
    }

    /// <summary>
    /// Fetch all RemediationPolicies matching the given alert name and severity threshold.
    /// </summary>
    private async Task<List<RemediationPolicy>> GetMatchingPoliciesAsync(
        string alertName, SeverityLevel alertSeverity, string ns, CancellationToken ct)
    {
        var policies = new List<RemediationPolicy>();

        try
        {
            var response = await _k8s.CustomObjects.ListNamespacedCustomObjectAsync(
                Constants.RemediationPolicyGroup,
                Constants.RemediationPolicyVersion,
                ns,
                Constants.RemediationPolicyPlural,
                cancellationToken: ct);

            var json = JsonSerializer.Serialize(response);
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.TryGetProperty("items", out var itemsElement)
                ? itemsElement.EnumerateArray()
                : [];

            foreach (var item in items)
            {
                try
                {
                    var policy = ParsePolicyFromJson(item);
                    if (policy is null) continue;

                    // Match alertName (case-insensitive)
                    if (!string.Equals(policy.AlertName, alertName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Check severity threshold
                    if (!SeverityHelper.MeetsThreshold(alertSeverity, policy.Severity))
                        continue;

                    policies.Add(policy);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to parse RemediationPolicy from JSON");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to list RemediationPolicies in namespace {Namespace}", ns);
        }

        return policies;
    }

    /// <summary>
    /// Deserialize a RemediationPolicy from a JSON element.
    /// </summary>
    private static RemediationPolicy? ParsePolicyFromJson(JsonElement item)
    {
        if (!item.TryGetProperty("metadata", out var metadata))
            return null;

        var name = metadata.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        var ns = metadata.TryGetProperty("namespace", out var nsEl) ? nsEl.GetString() : "default";
        var uid = metadata.TryGetProperty("uid", out var uidEl) ? uidEl.GetString() : "";

        if (string.IsNullOrEmpty(name)) return null;

        if (!item.TryGetProperty("spec", out var spec))
            return null;

        var alertName = spec.TryGetProperty("alertName", out var anEl) ? anEl.GetString() : null;
        if (string.IsNullOrEmpty(alertName)) return null;

        var severityStr = spec.TryGetProperty("severity", out var sevEl) ? sevEl.GetString() : "P3";
        var cooldown = spec.TryGetProperty("cooldown", out var coolEl) ? coolEl.GetString() : "5m";
        var maxConcurrency = spec.TryGetProperty("maxConcurrency", out var mcEl) ? mcEl.GetInt32() : 1;

        var actions = new List<RemediationActionSpec>();
        if (spec.TryGetProperty("actions", out var actionsEl))
        {
            foreach (var actionEl in actionsEl.EnumerateArray())
            {
                var action = ParseActionFromJson(actionEl);
                if (action is not null) actions.Add(action);
            }
        }

        if (actions.Count == 0) return null;

        return new RemediationPolicy
        {
            Name = name,
            Namespace = ns,
            Uid = uid ?? "",
            AlertName = alertName,
            Severity = SeverityHelper.Parse(severityStr),
            Cooldown = cooldown ?? "5m",
            MaxConcurrency = maxConcurrency,
            Actions = actions
        };
    }

    /// <summary>
    /// Deserialize a single action from JSON.
    /// </summary>
    private static RemediationActionSpec? ParseActionFromJson(JsonElement element)
    {
        var typeStr = element.TryGetProperty("type", out var tEl) ? tEl.GetString() : null;
        var target = element.TryGetProperty("target", out var tarEl) ? tarEl.GetString() : null;

        if (string.IsNullOrEmpty(typeStr) || string.IsNullOrEmpty(target))
            return null;

        if (!Enum.TryParse<RemediationActionType>(typeStr, true, out var actionType))
            return null;

        ActionParams? actionParams = null;
        if (element.TryGetProperty("params", out var pEl))
        {
            actionParams = new ActionParams
            {
                Replicas = pEl.TryGetProperty("replicas", out var rEl) ? rEl.GetInt32() : null,
                CpuRequest = pEl.TryGetProperty("cpuRequest", out var crEl) ? crEl.GetString() : null,
                MemoryRequest = pEl.TryGetProperty("memoryRequest", out var mrEl) ? mrEl.GetString() : null,
                CpuLimit = pEl.TryGetProperty("cpuLimit", out var clEl) ? clEl.GetString() : null,
                MemoryLimit = pEl.TryGetProperty("memoryLimit", out var mlEl) ? mlEl.GetString() : null,
                Revision = pEl.TryGetProperty("revision", out var revEl) ? revEl.GetInt32() : null,
                Message = pEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null,
                Channel = pEl.TryGetProperty("channel", out var chEl) ? chEl.GetString() : null,
                MaxUnavailable = pEl.TryGetProperty("maxUnavailable", out var muEl) ? muEl.GetString() : null
            };
        }

        return new RemediationActionSpec
        {
            Type = actionType,
            Target = target,
            Params = actionParams
        };
    }

    /// <summary>
    /// Execute all actions for a matched policy against the alert.
    /// </summary>
    private async Task ExecutePolicyAsync(RemediationPolicy policy, AlertmanagerAlert alert, CancellationToken ct)
    {
        var alertName = alert.Labels?.GetValueOrDefault("alertname", "unknown") ?? "unknown";

        // Parse cooldown duration
        var cooldown = ParseDuration(policy.Cooldown);
        if (!_cooldownManager.TryEnter(policy.Name, alertName, cooldown))
        {
            _logger.Information(
                "Policy {PolicyName} is in cooldown for alert {AlertName}, throttling",
                policy.Name, alertName);
            MetricsRegistry.Throttled.WithLabels(policy.Name, "cooldown").Inc();
            MetricsRegistry.CooldownActive.WithLabels(policy.Name).Set(1);
            return;
        }

        MetricsRegistry.CooldownActive.WithLabels(policy.Name).Set(0);

        _logger.Information(
            "Executing policy {PolicyName} ({ActionCount} actions) for alert {AlertName} severity {Severity}",
            policy.Name, policy.Actions.Count, alertName, policy.Severity);

        foreach (var action in policy.Actions)
        {
            await _globalThrottle.WaitAsync(ct);
            try
            {
                var result = await ExecuteSingleActionAsync(policy, action, alert, ct);

                // Create RemediationAction audit record
                await CreateAuditRecordAsync(policy, action, alert, result, ct);
            }
            catch (Exception ex)
            {
                _logger.Error(ex,
                    "Failed to execute action {ActionType} on {Target} for policy {PolicyName}",
                    action.Type, action.Target, policy.Name);

                MetricsRegistry.ActionErrors
                    .WithLabels(policy.Name, action.Type.ToString(), "execution_error")
                    .Inc();

                // Still record the failure
                try
                {
                    await CreateAuditRecordAsync(policy, action, alert,
                        new ActionExecutionResult { Status = ActionResultStatus.Failed, Error = ex.Message }, ct);
                }
                catch (Exception auditEx)
                {
                    _logger.Error(auditEx, "Failed to create audit record for failed action");
                }
            }
            finally
            {
                _ = _globalThrottle.Release();
            }
        }
    }

    /// <summary>
    /// Execute a single remediation action.
    /// </summary>
    private async Task<ActionExecutionResult> ExecuteSingleActionAsync(
        RemediationPolicy policy, RemediationActionSpec action, AlertmanagerAlert alert, CancellationToken ct)
    {
        if (_dryRun)
        {
            _logger.Information("DRY-RUN: Would execute {ActionType} on {Target}", action.Type, action.Target);
            return new ActionExecutionResult { Status = ActionResultStatus.Success };
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        ActionExecutionResult result;

        try
        {
            result = action.Type switch
            {
                RemediationActionType.Scale => await ExecuteScaleActionAsync(policy, action, ct),
                RemediationActionType.Restart => await ExecuteRestartActionAsync(policy, action, ct),
                RemediationActionType.Rollback => await ExecuteRollbackActionAsync(policy, action, ct),
                RemediationActionType.Notify => await ExecuteNotifyActionAsync(policy, action, alert, ct),
                _ => new ActionExecutionResult { Status = ActionResultStatus.Failed, Error = $"Unknown action type: {action.Type}" }
            };

            sw.Stop();
            MetricsRegistry.ActionDuration
                .WithLabels(policy.Name, action.Type.ToString())
                .Observe(sw.Elapsed.TotalSeconds);

            MetricsRegistry.ActionExecuted
                .WithLabels(policy.Name, action.Type.ToString(), result.Status.ToString())
                .Inc();

            _logger.Information(
                "Action {ActionType} on {Target} for policy {PolicyName} completed with result {Result} in {DurationMs}ms",
                action.Type, action.Target, policy.Name, result.Status, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.Error(ex, "Action {ActionType} on {Target} failed unexpectedly", action.Type, action.Target);

            result = new ActionExecutionResult { Status = ActionResultStatus.Failed, Error = ex.Message };

            MetricsRegistry.ActionErrors
                .WithLabels(policy.Name, action.Type.ToString(), "unhandled_exception")
                .Inc();
        }

        return result;
    }

    /// <summary>
    /// Execute a scale action: adjust Deployment replica count and/or resource limits.
    /// </summary>
    private async Task<ActionExecutionResult> ExecuteScaleActionAsync(
        RemediationPolicy policy, RemediationActionSpec action, CancellationToken ct)
    {
        var (kind, name) = ParseTarget(action.Target);
        if (kind != "deployment")
        {
            return new ActionExecutionResult
            {
                Status = ActionResultStatus.Failed,
                Error = $"Scale action only supports 'deployment' targets, got '{kind}'"
            };
        }

        try
        {
            var deployment = await _k8s.AppsV1.ReadNamespacedDeploymentAsync(name, policy.Namespace, cancellationToken: ct);
            var beforeReplicas = deployment.Spec.Replicas;

            // Scale replicas if specified
            if (action.Params?.Replicas.HasValue == true)
            {
                deployment.Spec.Replicas = action.Params.Replicas.Value;
                _logger.Information("Scaling deployment {Name} replicas from {Before} to {After}",
                    name, beforeReplicas, action.Params.Replicas.Value);
            }

            // Update resource requests/limits
            if (deployment.Spec.Template.Spec.Containers.Count > 0)
            {
                var container = deployment.Spec.Template.Spec.Containers[0];
                if (container.Resources is null)
                {
                    container.Resources = new V1ResourceRequirements();
                }

                if (action.Params?.CpuRequest is not null)
                    container.Resources.Requests["cpu"] = new k8s.ResourceQuantity(action.Params.CpuRequest);
                if (action.Params?.MemoryRequest is not null)
                    container.Resources.Requests["memory"] = new k8s.ResourceQuantity(action.Params.MemoryRequest);
                if (action.Params?.CpuLimit is not null)
                    container.Resources.Limits["cpu"] = new k8s.ResourceQuantity(action.Params.CpuLimit);
                if (action.Params?.MemoryLimit is not null)
                    container.Resources.Limits["memory"] = new k8s.ResourceQuantity(action.Params.MemoryLimit);
            }

            await _k8s.AppsV1.ReplaceNamespacedDeploymentAsync(deployment, name, policy.Namespace, cancellationToken: ct);

            return new ActionExecutionResult
            {
                Status = ActionResultStatus.Success,
                BeforeState = new ResourceState { Replicas = beforeReplicas },
                AfterState = new ResourceState { Replicas = deployment.Spec.Replicas }
            };
        }
        catch (k8s.Autorest.HttpOperationException ex)
        {
            return new ActionExecutionResult
            {
                Status = ActionResultStatus.Failed,
                Error = $"Kubernetes API error: {ex.Response.StatusCode} - {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Execute a restart action: force-roll a Deployment restart.
    /// </summary>
    private async Task<ActionExecutionResult> ExecuteRestartActionAsync(
        RemediationPolicy policy, RemediationActionSpec action, CancellationToken ct)
    {
        var (kind, name) = ParseTarget(action.Target);

        if (kind != "deployment")
        {
            return new ActionExecutionResult
            {
                Status = ActionResultStatus.Failed,
                Error = $"Restart action only supports 'deployment' targets, got '{kind}'"
            };
        }

        try
        {
            var deployment = await _k8s.AppsV1.ReadNamespacedDeploymentAsync(name, policy.Namespace, cancellationToken: ct);

            // Annotate to trigger a rollout restart
            if (deployment.Spec.Template.Metadata.Annotations is null)
            {
                deployment.Spec.Template.Metadata.Annotations = new Dictionary<string, string>();
            }

            var restartTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            deployment.Spec.Template.Metadata.Annotations["kubectl.kubernetes.io/restartedAt"] = restartTime;
            deployment.Spec.Template.Metadata.Annotations["hishope.io/remediation-restart"] = restartTime;

            // Apply maxUnavailable via RollingUpdate
            if (action.Params?.MaxUnavailable is not null &&
                deployment.Spec.Strategy?.RollingUpdate is not null)
            {
                deployment.Spec.Strategy.RollingUpdate.MaxUnavailable = new k8s.ResourceQuantity(action.Params.MaxUnavailable);
            }

            await _k8s.AppsV1.ReplaceNamespacedDeploymentAsync(deployment, name, policy.Namespace, cancellationToken: ct);

            _logger.Information("Triggered restart of deployment {Name} in namespace {Ns}",
                name, policy.Namespace);

            return new ActionExecutionResult
            {
                Status = ActionResultStatus.Success
            };
        }
        catch (k8s.Autorest.HttpOperationException ex)
        {
            return new ActionExecutionResult
            {
                Status = ActionResultStatus.Failed,
                Error = $"Kubernetes API error: {ex.Response.StatusCode} - {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Execute a rollback action: revert a Deployment to a previous revision.
    /// </summary>
    private async Task<ActionExecutionResult> ExecuteRollbackActionAsync(
        RemediationPolicy policy, RemediationActionSpec action, CancellationToken ct)
    {
        var (kind, name) = ParseTarget(action.Target);

        if (kind != "deployment")
        {
            return new ActionExecutionResult
            {
                Status = ActionResultStatus.Failed,
                Error = $"Rollback action only supports 'deployment' targets, got '{kind}'"
            };
        }

        try
        {
            var deployment = await _k8s.AppsV1.ReadNamespacedDeploymentAsync(name, policy.Namespace, cancellationToken: ct);
            var currentRevision = deployment.Metadata.Annotations?.GetValueOrDefault("deployment.kubernetes.io/revision", "0");

            if (action.Params?.Revision is null)
            {
                return new ActionExecutionResult
                {
                    Status = ActionResultStatus.Failed,
                    Error = "Rollback action requires a 'revision' parameter"
                };
            }

            // List ReplicaSets to find the target revision
            var rsList = await _k8s.AppsV1.ListNamespacedReplicaSetAsync(
                policy.Namespace,
                labelSelector: $"app={name}",
                cancellationToken: ct);

            var targetRs = rsList.Items.FirstOrDefault(rs =>
            {
                var rev = rs.Metadata.Annotations?.GetValueOrDefault("deployment.kubernetes.io/revision", "0");
                return int.TryParse(rev, out var r) && r == action.Params.Revision.Value;
            });

            if (targetRs is null)
            {
                return new ActionExecutionResult
                {
                    Status = ActionResultStatus.Failed,
                    Error = $"No ReplicaSet found for revision {action.Params.Revision}"
                };
            }

            // Perform rollback by updating the Deployment's template to match the target ReplicaSet
            deployment.Spec.Template.Spec = targetRs.Spec.Template.Spec;
            deployment.Spec.Template.Metadata.Labels = targetRs.Spec.Template.Metadata.Labels;

            await _k8s.AppsV1.ReplaceNamespacedDeploymentAsync(deployment, name, policy.Namespace, cancellationToken: ct);

            _logger.Information("Rolled back deployment {Name} to revision {Revision} (was {CurrentRevision})",
                name, action.Params.Revision, currentRevision);

            return new ActionExecutionResult
            {
                Status = ActionResultStatus.Success,
                BeforeState = new ResourceState { Revision = int.TryParse(currentRevision, out var cr) ? cr : 0 },
                AfterState = new ResourceState { Revision = action.Params.Revision.Value }
            };
        }
        catch (k8s.Autorest.HttpOperationException ex)
        {
            return new ActionExecutionResult
            {
                Status = ActionResultStatus.Failed,
                Error = $"Kubernetes API error: {ex.Response.StatusCode} - {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Execute a notify action: send Slack, PagerDuty, and OpsGenie notifications via webhook.
    /// </summary>
    private async Task<ActionExecutionResult> ExecuteNotifyActionAsync(
        RemediationPolicy policy, RemediationActionSpec action, AlertmanagerAlert alert, CancellationToken ct)
    {
        var channel = action.Params?.Channel ?? "default";
        var alertName = alert.Labels is not null && alert.Labels.TryGetValue("alertname", out var an) ? an : "unknown";
        var service = action.Target;
        var severity = policy.Severity switch
        {
            SeverityLevel.P0 => "critical",
            SeverityLevel.P1 => "error",
            SeverityLevel.P2 => "warning",
            SeverityLevel.P3 => "info",
            _ => "info"
        };
        var message = action.Params?.Message ?? $"Remediation triggered for alert: {alertName}";
        var description = message;
        if (alert.Annotations is not null)
        {
            alert.Annotations.TryGetValue("description", out var desc);
            alert.Annotations.TryGetValue("summary", out var summary);
            description = desc ?? summary ?? message;
        }

        _logger.Information(
            "NOTIFY channel={Channel} target={Target} alert={Alert} severity={Severity} message={Message}",
            channel, service, alertName, severity, message);

        var slackWebhook = Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL");
        var pagerdutyKey = Environment.GetEnvironmentVariable("PAGERDUTY_ROUTING_KEY")
                           ?? Environment.GetEnvironmentVariable("PAGERDUTY_INTEGRATION_KEY");
        var opsgenieKey = Environment.GetEnvironmentVariable("OPSGENIE_API_KEY");

        var notificationsSent = new List<string>();

        // Slack
        if (!string.IsNullOrEmpty(slackWebhook))
        {
            try
            {
                var slackPayload = new
                {
                    text = "",
                    blocks = new object[]
                    {
                        new
                        {
                            type = "header",
                            text = new { type = "plain_text", text = ":warning: Auto-Remediation Triggered" }
                        },
                        new
                        {
                            type = "section",
                            fields = new object[]
                            {
                                new { type = "mrkdwn", text = $"*Alert:* {alertName}" },
                                new { type = "mrkdwn", text = $"*Service:* {service}" },
                                new { type = "mrkdwn", text = $"*Action:* {action.Type}" },
                                new { type = "mrkdwn", text = $"*Severity:* {policy.Severity}" }
                            }
                        },
                        new
                        {
                            type = "section",
                            text = new { type = "mrkdwn", text = description }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(slackPayload);
                using var slackContent = new StringContent(json, Encoding.UTF8, "application/json");
                var slackResponse = await _httpClient.PostAsync(slackWebhook, slackContent, ct);
                slackResponse.EnsureSuccessStatusCode();
                notificationsSent.Add("slack");
                _logger.Information("Slack notification sent for alert {AlertName}", alertName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send Slack notification for alert {AlertName}", alertName);
            }
        }
        else
        {
            _logger.Information("SLACK_WEBHOOK_URL not configured, skipping Slack notification");
        }

        // PagerDuty
        if (!string.IsNullOrEmpty(pagerdutyKey))
        {
            try
            {
                var pdPayload = new
                {
                    routing_key = pagerdutyKey,
                    event_action = "trigger",
                    payload = new
                    {
                        summary = message,
                        source = "remediation-operator",
                        severity,
                        component = service,
                        group = alertName,
                        @class = "auto-remediation"
                    }
                };

                var json = JsonSerializer.Serialize(pdPayload);
                using var pdContent = new StringContent(json, Encoding.UTF8, "application/json");
                var pdResponse = await _httpClient.PostAsync("https://events.pagerduty.com/v2/enqueue", pdContent, ct);
                pdResponse.EnsureSuccessStatusCode();
                notificationsSent.Add("pagerduty");
                _logger.Information("PagerDuty notification sent for alert {AlertName}", alertName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send PagerDuty notification for alert {AlertName}", alertName);
            }
        }
        else
        {
            _logger.Information("PAGERDUTY_ROUTING_KEY not configured, skipping PagerDuty notification");
        }

        // OpsGenie (optional)
        if (!string.IsNullOrEmpty(opsgenieKey))
        {
            try
            {
                var ogPayload = new
                {
                    message,
                    alias = $"remediation-{alertName}-{DateTime.UtcNow:O}",
                    description,
                    priority = policy.Severity switch
                    {
                        SeverityLevel.P0 => "P1",
                        SeverityLevel.P1 => "P2",
                        SeverityLevel.P2 => "P3",
                        SeverityLevel.P3 => "P4",
                        _ => "P4"
                    },
                    source = "remediation-operator",
                    details = new Dictionary<string, string>
                    {
                        ["alert"] = alertName,
                        ["service"] = service,
                        ["action"] = action.Type.ToString()
                    }
                };

                var json = JsonSerializer.Serialize(ogPayload);
                using var ogContent = new StringContent(json, Encoding.UTF8, "application/json");
                var ogResponse = await _httpClient.PostAsync(
                    $"https://api.opsgenie.com/v2/alerts", ogContent, ct);
                ogResponse.EnsureSuccessStatusCode();
                notificationsSent.Add("opsgenie");
                _logger.Information("OpsGenie notification sent for alert {AlertName}", alertName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send OpsGenie notification for alert {AlertName}", alertName);
            }
        }
        else
        {
            _logger.Information("OPSGENIE_API_KEY not configured, skipping OpsGenie notification");
        }

        return new ActionExecutionResult
        {
            Status = ActionResultStatus.Success,
            NotificationsSent = notificationsSent.Count > 0 ? notificationsSent : null
        };
    }

    /// <summary>
    /// Create a RemediationAction CRD record for audit trail.
    /// </summary>
    private async Task CreateAuditRecordAsync(
        RemediationPolicy policy, RemediationActionSpec action, AlertmanagerAlert alert,
        ActionExecutionResult result, CancellationToken ct)
    {
        var actionName = $"{policy.Name}-{action.Type.ToString().ToLowerInvariant()}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var alertName = alert.Labels?.GetValueOrDefault("alertname", "unknown") ?? "unknown";
        var alertUid = alert.Fingerprint ?? "unknown";

        var auditRecord = new Dictionary<string, object>
        {
            ["apiVersion"] = $"{Constants.RemediationPolicyGroup}/{Constants.RemediationPolicyVersion}",
            ["kind"] = "RemediationAction",
            ["metadata"] = new Dictionary<string, object>
            {
                ["name"] = actionName,
                ["namespace"] = policy.Namespace,
                ["ownerReferences"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["apiVersion"] = $"{Constants.RemediationPolicyGroup}/{Constants.RemediationPolicyVersion}",
                        ["kind"] = "RemediationPolicy",
                        ["name"] = policy.Name,
                        ["uid"] = policy.Uid,
                        ["controller"] = true,
                        ["blockOwnerDeletion"] = true
                    }
                },
                ["labels"] = new Dictionary<string, string>
                {
                    ["app.kubernetes.io/part-of"] = "his-hope",
                    ["app.kubernetes.io/managed-by"] = "remediation-operator",
                    ["hishope.io/remediation-policy"] = policy.Name,
                    ["hishope.io/alert-name"] = SanitizeLabelValue(alertName)
                }
            },
            ["spec"] = new Dictionary<string, object>
            {
                ["policyRef"] = new Dictionary<string, object>
                {
                    ["name"] = policy.Name,
                    ["uid"] = policy.Uid,
                    ["alertName"] = alertName,
                    ["alertUid"] = alertUid
                },
                ["actionType"] = action.Type.ToString().ToLowerInvariant(),
                ["target"] = action.Target,
                ["severity"] = policy.Severity.ToString(),
                ["reason"] = $"Alert {alertName} triggered remediation policy {policy.Name}"
            },
            ["status"] = new Dictionary<string, object>
            {
                ["result"] = result.Status.ToString(),
                ["executedAt"] = DateTime.UtcNow.ToString("o"),
                ["completedAt"] = DateTime.UtcNow.ToString("o"),
                ["durationSeconds"] = 0,
                ["conditions"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = result.Status == ActionResultStatus.Success ? "Completed" : "Failed",
                        ["status"] = "True",
                        ["reason"] = result.Status == ActionResultStatus.Success ? "ActionSucceeded" : "ActionFailed",
                        ["message"] = result.Error ?? "Action completed successfully",
                        ["lastTransitionTime"] = DateTime.UtcNow.ToString("o")
                    }
                }
            }
        };

        if (result.Error is not null)
        {
            ((Dictionary<string, object>)auditRecord["status"])["error"] = result.Error;
        }

        if (result.BeforeState is not null)
        {
            var before = new Dictionary<string, object>();
            if (result.BeforeState.Replicas.HasValue)
                before["replicas"] = result.BeforeState.Replicas.Value;
            if (result.BeforeState.Revision.HasValue)
                before["revision"] = result.BeforeState.Revision.Value;
            ((Dictionary<string, object>)auditRecord["status"])["before"] = before;
        }

        if (result.AfterState is not null)
        {
            var after = new Dictionary<string, object>();
            if (result.AfterState.Replicas.HasValue)
                after["replicas"] = result.AfterState.Replicas.Value;
            if (result.AfterState.Revision.HasValue)
                after["revision"] = result.AfterState.Revision.Value;
            ((Dictionary<string, object>)auditRecord["status"])["after"] = after;
        }

        try
        {
            await _k8s.CustomObjects.CreateNamespacedCustomObjectAsync(
                auditRecord,
                Constants.RemediationPolicyGroup,
                Constants.RemediationPolicyVersion,
                policy.Namespace,
                Constants.RemediationActionPlural,
                cancellationToken: ct);

            _logger.Information("Created RemediationAction audit record {ActionName} in namespace {Ns}",
                actionName, policy.Namespace);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create RemediationAction audit record {ActionName}", actionName);
        }
    }

    /// <summary>
    /// Parse a target string like "deployment/identity-service" into (kind, name).
    /// </summary>
    private static (string Kind, string Name) ParseTarget(string target)
    {
        var parts = target.Split('/', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2
            ? (parts[0].ToLowerInvariant(), parts[1])
            : ("deployment", parts[0]);
    }

    /// <summary>
    /// Parse a duration string like "5m", "30s", "1h" into TimeSpan.
    /// </summary>
    private static TimeSpan ParseDuration(string duration)
    {
        var match = Regex.Match(duration, @"^(\d+)([smh])$");
        if (!match.Success) return TimeSpan.FromMinutes(5);

        var value = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return match.Groups[2].Value switch
        {
            "s" => TimeSpan.FromSeconds(value),
            "m" => TimeSpan.FromMinutes(value),
            "h" => TimeSpan.FromHours(value),
            _ => TimeSpan.FromMinutes(5)
        };
    }

    /// <summary>
    /// Sanitize a string for use as a Kubernetes label value.
    /// </summary>
    private static string SanitizeLabelValue(string value)
    {
        // K8s label values: alphanumeric, '-', '_', '.' — max 63 chars
        var sanitized = Regex.Replace(value, @"[^a-zA-Z0-9\-_\.]", "-");
        return sanitized.Length > 63 ? sanitized[..63] : sanitized;
    }
}
