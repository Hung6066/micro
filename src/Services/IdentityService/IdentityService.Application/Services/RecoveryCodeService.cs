using System.Security.Cryptography;
using System.Text;

namespace His.Hope.IdentityService.Application.Services;

public class RecoveryCodeService
{
    private const int CodeGroupLength = 5;
    private const int CodeGroupCount = 3;

    public string[] GenerateCodes(int count = 8)
    {
        var codes = new string[count];
        for (var i = 0; i < count; i++)
            codes[i] = GenerateSingleCode();
        return codes;
    }

    public string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateSingleCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(CodeGroupLength * CodeGroupCount);
        var groups = new string[CodeGroupCount];
        for (var i = 0; i < CodeGroupCount; i++)
        {
            var groupBytes = bytes[(i * CodeGroupLength)..((i + 1) * CodeGroupLength)];
            var value = 0;
            foreach (var b in groupBytes)
                value = (value * 256 + b) % 100000;
            groups[i] = value.ToString("D5");
        }
        return string.Join("-", groups);
    }
}
