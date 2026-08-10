using System.Text.RegularExpressions;

namespace His.Hope.SharedKernel.Domain.Common;

public static partial class Guard
{
    public static class Against
    {
        public static string NullOrWhiteSpace(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"'{parameterName}' cannot be null or whitespace.", parameterName);
            return value;
        }

        public static T Null<T>(T? value, string parameterName) where T : class
        {
            if (value is null)
                throw new ArgumentNullException(parameterName, $"'{parameterName}' cannot be null.");
            return value;
        }

        public static string InvalidFormat(string value, string pattern, string parameterName)
        {
            NullOrWhiteSpace(value, parameterName);
            if (!Regex.IsMatch(value, pattern))
                throw new ArgumentException($"'{parameterName}' has invalid format.", parameterName);
            return value;
        }

        public static string Email(string email, string parameterName)
        {
            return InvalidFormat(email,
                @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
                parameterName);
        }

        public static string Phone(string phone, string parameterName)
        {
            return InvalidFormat(phone,
                @"^\+?[0-9\s\-\(\)]{7,20}$",
                parameterName);
        }

        public static DateTime OutOfRange(DateTime value, string parameterName,
            DateTime? min = null, DateTime? max = null)
        {
            if (min.HasValue && value < min.Value)
                throw new ArgumentOutOfRangeException(parameterName,
                    $"'{parameterName}' must be >= {min.Value:yyyy-MM-dd}.");
            if (max.HasValue && value > max.Value)
                throw new ArgumentOutOfRangeException(parameterName,
                    $"'{parameterName}' must be <= {max.Value:yyyy-MM-dd}.");
            return value;
        }

        public static void BusinessRule(IBusinessRule rule)
        {
            if (rule.IsBroken())
                throw new Domain.Exceptions.DomainException(rule.Message);
        }
    }
}

public interface IBusinessRule
{
    bool IsBroken();
    string Message { get; }
}
