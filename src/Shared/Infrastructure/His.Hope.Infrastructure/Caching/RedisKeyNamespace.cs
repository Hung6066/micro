using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace His.Hope.Infrastructure.Caching;

public static class RedisKeyNamespace
{
    private static readonly Regex ValidPrefix = new("^[A-Za-z0-9_-]+(:[A-Za-z0-9_-]+)*:$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string GetPrefix(IConfiguration configuration)
    {
        var prefix = configuration["Redis:KeyPrefix"]?.Trim() ?? string.Empty;
        if (prefix.Length > 0 && !ValidPrefix.IsMatch(prefix))
            throw new InvalidOperationException("Redis key prefix must be colon-delimited safe scope segments and end with a colon.");
        return prefix;
    }

    public static string ApplyPrefix(string key, string prefix) =>
        prefix.Length == 0 || key.StartsWith(prefix, StringComparison.Ordinal) ? key : prefix + key;
}

public class RedisKeyNamespaceDispatchProxy : DispatchProxy
{
    private object _target = null!;
    private byte[] _prefix = [];
    private string _prefixText = string.Empty;

    public static T Wrap<T>(T target, string prefix) where T : class
    {
        if (prefix.Length == 0) return target;
        var proxy = Create<T, RedisKeyNamespaceDispatchProxy>();
        ((RedisKeyNamespaceDispatchProxy)(object)proxy).Initialize(target, prefix);
        return proxy;
    }

    private void Initialize(object target, string prefix)
    {
        _target = target;
        _prefixText = prefix;
        _prefix = System.Text.Encoding.UTF8.GetBytes(prefix);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) throw new InvalidOperationException("Redis proxy received a call without a target method.");
        args ??= [];
        PrefixKeyArguments(targetMethod, args);

        object? result;
        try
        {
            result = targetMethod.Invoke(_target, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }

        return result switch
        {
            IConnectionMultiplexer multiplexer => Wrap(multiplexer, _prefixText),
            ITransaction transaction => Wrap(transaction, _prefixText),
            IBatch batch => Wrap(batch, _prefixText),
            IDatabase database => Wrap(database, _prefixText),
            _ => result,
        };
    }

    private void PrefixKeyArguments(MethodInfo targetMethod, object?[] args)
    {
        var parameters = targetMethod.GetParameters();
        for (var index = 0; index < Math.Min(parameters.Length, args.Length); index++)
        {
            if (parameters[index].ParameterType == typeof(RedisKey) && args[index] is RedisKey key)
            {
                args[index] = Prefix(key);
            }
            else if (parameters[index].ParameterType == typeof(RedisKey[]) && args[index] is RedisKey[] keys)
            {
                args[index] = keys.Select(Prefix).ToArray();
            }
        }
    }

    private RedisKey Prefix(RedisKey key)
    {
        var keyBytes = (byte[]?)key;
        if (keyBytes is null || keyBytes.Length == 0 || keyBytes.AsSpan().StartsWith(_prefix)) return key;

        var namespaced = new byte[_prefix.Length + keyBytes.Length];
        _prefix.CopyTo(namespaced, 0);
        keyBytes.CopyTo(namespaced, _prefix.Length);
        return (RedisKey)namespaced;
    }
}
