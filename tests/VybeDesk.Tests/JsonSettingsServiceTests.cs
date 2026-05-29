using System.Text.Json;
using VybeDesk.Core.Models;
using VybeDesk.Services.Storage;
using Xunit;

namespace VybeDesk.Tests;

public class JsonSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public JsonSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vybedesk-test-settings-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ---- MigrateModelId (via reflection — internal static) ----

    [Theory]
    [InlineData("claude-opus-4-7")]
    [InlineData("claude-sonnet-4-6")]
    [InlineData("claude-haiku-4-5")]
    [InlineData("claude-opus-4-6")]
    [InlineData("claude-sonnet-4-5")]
    [InlineData("claude-opus-4-5")]
    [InlineData("claude-opus-4-1")]
    [InlineData("claude-sonnet-4-7")]
    public void MigrateModelId_BadId_ReplacedWithDefault(string badId)
    {
        var settings = new AppSettings { Model = badId };

        var method = typeof(JsonSettingsService).GetMethod(
            "MigrateModelId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var migrated = (bool)method.Invoke(null, new object[] { settings })!;

        Assert.True(migrated);
        Assert.Equal("claude-sonnet-4-20250514", settings.Model);
    }

    [Fact]
    public void MigrateModelId_BadId_CaseInsensitive()
    {
        var settings = new AppSettings { Model = "CLAUDE-OPUS-4-7" };

        var method = typeof(JsonSettingsService).GetMethod(
            "MigrateModelId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var migrated = (bool)method.Invoke(null, new object[] { settings })!;

        Assert.True(migrated);
        Assert.Equal("claude-sonnet-4-20250514", settings.Model);
    }

    [Fact]
    public void MigrateModelId_ValidId_NotMigrated()
    {
        var settings = new AppSettings { Model = "claude-sonnet-4-20250514" };

        var method = typeof(JsonSettingsService).GetMethod(
            "MigrateModelId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var migrated = (bool)method.Invoke(null, new object[] { settings })!;

        Assert.False(migrated);
        Assert.Equal("claude-sonnet-4-20250514", settings.Model);
    }

    [Fact]
    public void MigrateModelId_NullModel_NotMigrated()
    {
        var settings = new AppSettings { Model = null! };

        var method = typeof(JsonSettingsService).GetMethod(
            "MigrateModelId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var migrated = (bool)method.Invoke(null, new object[] { settings })!;

        Assert.False(migrated);
    }

    [Fact]
    public void MigrateModelId_EmptyModel_NotMigrated()
    {
        var settings = new AppSettings { Model = "" };

        var method = typeof(JsonSettingsService).GetMethod(
            "MigrateModelId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var migrated = (bool)method.Invoke(null, new object[] { settings })!;

        Assert.False(migrated);
    }
}
