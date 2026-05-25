using System.Text.Json;

namespace ClaudePM.Services.Storage;

/// <summary>Serializes a tag list to/from the JSON TEXT column.</summary>
internal static class TagSerializer
{
    public static string Serialize(List<string> tags) => JsonSerializer.Serialize(tags);

    public static List<string> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
