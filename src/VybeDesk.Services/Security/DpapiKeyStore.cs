using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using VybeDesk.Core.Services;

namespace VybeDesk.Services.Security;

/// <summary>
/// Stores the Anthropic API key encrypted at rest using Windows DPAPI
/// (current-user scope). Windows-only — matches the v1 target platform.
/// On non-Windows, ProtectedData throws PlatformNotSupportedException; a
/// Keychain/libsecret implementation behind ISecureKeyStore would be added
/// when the app targets macOS/Linux.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiKeyStore : ISecureKeyStore
{
    private readonly string _path = Path.Combine(Paths.AppDataDir(), "apikey.bin");

    public bool HasKey => File.Exists(_path);

    public void SaveKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is empty.", nameof(apiKey));
        foreach (var c in apiKey)
        {
            if (c > 127)
                throw new ArgumentException(
                    "API key contains non-ASCII characters (often a smart-quote " +
                    "or em-dash from a rich-text copy-paste). Re-paste the key as " +
                    "raw text from the Anthropic console.", nameof(apiKey));
        }

        var cipher = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiKey), optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, cipher);
    }

    public string? LoadKey()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var plain = ProtectedData.Unprotect(
                File.ReadAllBytes(_path), optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            // Corrupt or re-keyed data — treat as no key.
            return null;
        }
        catch (FormatException)
        {
            // Malformed blob — treat as no key.
            return null;
        }
    }

    public void ClearKey()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
