using VybeDesk.Services.Storage;
using Xunit;

namespace VybeDesk.Tests;

public class TagSerializerTests
{
    [Fact]
    public void Serialize_EmptyList_ReturnsEmptyJsonArray()
    {
        var result = TagSerializer.Serialize(new List<string>());
        Assert.Equal("[]", result);
    }

    [Fact]
    public void Serialize_MultipleItems_ReturnsJsonArray()
    {
        var result = TagSerializer.Serialize(new List<string> { "alpha", "beta", "gamma" });
        Assert.Equal("[\"alpha\",\"beta\",\"gamma\"]", result);
    }

    [Fact]
    public void Deserialize_ValidJson_ReturnsList()
    {
        var result = TagSerializer.Deserialize("[\"x\",\"y\"]");
        Assert.Equal(new[] { "x", "y" }, result);
    }

    [Fact]
    public void Deserialize_EmptyArray_ReturnsEmptyList()
    {
        var result = TagSerializer.Deserialize("[]");
        Assert.Empty(result);
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsEmptyList()
    {
        var result = TagSerializer.Deserialize("not json at all");
        Assert.Empty(result);
    }

    [Fact]
    public void Deserialize_NullJson_ReturnsEmptyList()
    {
        // JsonSerializer.Deserialize("null") returns null; should degrade to empty.
        var result = TagSerializer.Deserialize("null");
        Assert.Empty(result);
    }

    [Fact]
    public void RoundTrip_PreservesOrderAndContent()
    {
        var original = new List<string> { "setup", "testing", "CI/CD" };
        var json = TagSerializer.Serialize(original);
        var restored = TagSerializer.Deserialize(json);
        Assert.Equal(original, restored);
    }
}
