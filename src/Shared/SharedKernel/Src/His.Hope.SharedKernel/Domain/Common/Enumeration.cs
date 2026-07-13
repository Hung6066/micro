using System.Reflection;

namespace His.Hope.SharedKernel.Domain.Common;

public abstract class Enumeration<TEnum> : ValueObject, IComparable<TEnum>
    where TEnum : Enumeration<TEnum>
{
    public string Name { get; }
    public string Code { get; }

    private static readonly Lazy<Dictionary<string, TEnum>> _byCode = new(() =>
        typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.FieldType == typeof(TEnum))
            .Select(f => f.GetValue(null))
            .Cast<TEnum>()
            .ToDictionary(e => e.Code));

    private static readonly Lazy<Dictionary<string, TEnum>> _byName = new(() =>
        typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.FieldType == typeof(TEnum))
            .Select(f => f.GetValue(null))
            .Cast<TEnum>()
            .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase));

    protected Enumeration(string code, string name)
    {
        Code = Guard.Against.NullOrWhiteSpace(code, nameof(code));
        Name = Guard.Against.NullOrWhiteSpace(name, nameof(name));
    }

    public static TEnum FromCode(string code) =>
        _byCode.Value.TryGetValue(code, out var result)
            ? result
            : throw new InvalidOperationException($"'{code}' is not a valid {typeof(TEnum).Name}");

    public static TEnum FromName(string name) =>
        _byName.Value.TryGetValue(name, out var result)
            ? result
            : throw new InvalidOperationException($"'{name}' is not a valid {typeof(TEnum).Name}");

    public static IReadOnlyCollection<TEnum> GetAll() =>
        _byCode.Value.Values.ToList().AsReadOnly();

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Code;
    }

    public int CompareTo(TEnum? other) =>
        string.Compare(Code, other?.Code, StringComparison.Ordinal);
}
