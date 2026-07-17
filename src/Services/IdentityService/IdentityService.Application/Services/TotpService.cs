using System.Security.Cryptography;
using System.Text;

namespace His.Hope.IdentityService.Application.Services;

public class TotpService
{
    private const int StepSeconds = 30;
    private const int CodeDigits = 6;
    private const int AllowedDrift = 1;

    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly char[] Base32Chars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encode(bytes);
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(code))
            return false;

        var secretBytes = Base32Decode(secret);
        var counter = GetCurrentCounter();

        for (var i = -AllowedDrift; i <= AllowedDrift; i++)
        {
            var expected = GenerateTotp(secretBytes, counter + i);
            if (expected == code)
                return true;
        }
        return false;
    }

    public string GenerateQrCodeUri(string secret, string email, string issuer = "HisHope")
    {
        var encodedSecret = Uri.EscapeDataString(secret);
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(email);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={encodedSecret}&issuer={encodedIssuer}&algorithm=SHA1&digits={CodeDigits}&period={StepSeconds}";
    }

    private static string GenerateTotp(byte[] secret, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes);

        var offset = hash[^1] & 0x0f;
        var binaryCode = (hash[offset] & 0x7f) << 24
                       | (hash[offset + 1] & 0xff) << 16
                       | (hash[offset + 2] & 0xff) << 8
                       | (hash[offset + 3] & 0xff);

        var totp = binaryCode % (int)Math.Pow(10, CodeDigits);
        return totp.ToString(new string('0', CodeDigits));
    }

    private static long GetCurrentCounter()
    {
        var elapsed = DateTime.UtcNow - UnixEpoch;
        return (long)(elapsed.TotalSeconds / StepSeconds);
    }

    private static string Base32Encode(byte[] data)
    {
        var result = new StringBuilder();
        var bits = 0;
        var bitCount = 0;

        foreach (var b in data)
        {
            bits = (bits << 8) | b;
            bitCount += 8;

            while (bitCount >= 5)
            {
                bitCount -= 5;
                result.Append(Base32Chars[(bits >> bitCount) & 0x1f]);
            }
        }

        if (bitCount > 0)
            result.Append(Base32Chars[(bits << (5 - bitCount)) & 0x1f]);

        return result.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        var cleaned = input.Trim().ToUpperInvariant()
            .Replace(" ", "")
            .Replace("-", "")
            .TrimEnd('=');

        var bytes = new List<byte>();
        var bits = 0;
        var bitCount = 0;

        foreach (var c in cleaned)
        {
            var index = Array.IndexOf(Base32Chars, c);
            if (index < 0) continue;

            bits = (bits << 5) | index;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bitCount -= 8;
                bytes.Add((byte)((bits >> bitCount) & 0xff));
            }
        }

        return [.. bytes];
    }
}
