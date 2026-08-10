using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace His.Hope.SharedKernel.Domain.Common;

public sealed class EnumerationJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => IsEnumeration(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(EnumerationJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private static bool IsEnumeration(Type type)
    {
        var current = type;

        while (current is not null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Enumeration<>))
                return true;

            current = current.BaseType;
        }

        return false;
    }
}

public sealed class EnumerationJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : Enumeration<TEnum>
{
    public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var code = reader.GetString();
        if (string.IsNullOrWhiteSpace(code))
            return default;

        var fromCode = typeof(TEnum).BaseType?.GetMethod(nameof(Enumeration<TEnum>.FromCode), BindingFlags.Public | BindingFlags.Static);
        return (TEnum)fromCode!.Invoke(null, [code])!;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Code);
}
