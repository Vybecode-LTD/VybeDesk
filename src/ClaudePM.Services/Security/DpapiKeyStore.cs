using System.Security.Cryptography;
using System.Text;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Security;

/// <summary>
/// Stores the Anthropic API key encrypted at rest using Windows DPAPI
/// (current-user scope). Windows-only — matches the v1 target platform.
/// On non-Windows, ProtectedData throws PlatformNotSupportedException; a
/// Keychain/libsecret implementation behind ISecureKeyStore would be added
/// when the app targets macOS/Linux.
/// </summary>
public sealed class DpapiKeyStore : ISecureKeyStore
{
    private readonly string _path = Path.Combine(Paths.AppDataDir(), "apikey.bin");

    public bool HasKey => File.Exists(_path);

    public void SaveKey(string apiKey)
    {
        var cipher = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiKey), optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, cipher);
    }

    public string? LoadKey()
    {
        if (!File.Exists(_path)) return null;
        var plain = ProtectedData.Unprotect(
            File.ReadAllBytes(_path), optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plain);
    }

    public void ClearKey()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
