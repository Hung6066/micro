using System.Collections;
using System.Text;

namespace His.Hope.Configuration;

/// <summary>
/// Writes PEM material supplied through <c>PLATFORM_PKI_FILE_*</c> environment variables to
/// private files so libraries that only accept file paths (Npgsql, RabbitMQ, Redis CA) can use
/// platform mTLS client certificates. Call it before the host builder reads configuration.
/// <c>PLATFORM_PKI_FILE_PG_CLIENT_CRT</c> becomes <c>pg-client-crt.pem</c> in
/// <c>PLATFORM_PKI_DIR</c> (default <c>/tmp/openship-pki</c>).
/// </summary>
public static class PlatformPki
{
    private const string Prefix = "PLATFORM_PKI_FILE_";
    private const string DirectoryVariable = "PLATFORM_PKI_DIR";
    private const string DefaultDirectory = "/tmp/openship-pki";

    /// <returns>The directory that holds the files, or null when no variable was set.</returns>
    public static string? Materialize()
    {
        string? directory = null;
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string key || !key.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            if (entry.Value is not string value || string.IsNullOrWhiteSpace(value)) continue;

            var name = key[Prefix.Length..].ToLowerInvariant().Replace('_', '-');
            if (name.Length == 0 || !name.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
                throw new InvalidOperationException($"Environment variable {key} does not name a valid PKI file.");

            directory ??= PrepareDirectory();
            var path = Path.Combine(directory, name + ".pem");
            // Environment editors sometimes escape newlines; PEM needs real ones.
            var pem = value.Replace("\\n", "\n", StringComparison.Ordinal).Trim() + "\n";
            File.WriteAllText(path, pem, new UTF8Encoding(false));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return directory;
    }

    private static string PrepareDirectory()
    {
        var directory = Environment.GetEnvironmentVariable(DirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory)) directory = DefaultDirectory;
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return directory;
    }
}
