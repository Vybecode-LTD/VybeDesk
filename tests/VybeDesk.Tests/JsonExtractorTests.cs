using VybeDesk.Services.Utilities;
using Xunit;

namespace VybeDesk.Tests;

public class JsonExtractorTests
{
    [Fact]
    public void ExtractJsonBlock_CleanJson_ReturnsVerbatim()
    {
        var json = "{\"key\": \"value\"}";
        Assert.Equal(json, JsonExtractor.ExtractJsonBlock(json));
    }

    [Fact]
    public void ExtractJsonBlock_FencedJson_ExtractsBody()
    {
        var input = "```json\n{\"a\": 1}\n```";
        Assert.Equal("{\"a\": 1}", JsonExtractor.ExtractJsonBlock(input));
    }

    [Fact]
    public void ExtractJsonBlock_FencedWithoutLanguage_ExtractsBody()
    {
        var input = "```\n{\"b\": 2}\n```";
        Assert.Equal("{\"b\": 2}", JsonExtractor.ExtractJsonBlock(input));
    }

    [Fact]
    public void ExtractJsonBlock_LeadingProse_FindsFirstBrace()
    {
        var input = "Here is the result:\n{\"status\": \"ok\"}";
        Assert.Equal("{\"status\": \"ok\"}", JsonExtractor.ExtractJsonBlock(input));
    }

    [Fact]
    public void ExtractJsonBlock_TrailingProse_StopsAtMatchingBrace()
    {
        var input = "{\"x\": 1}\n\nHope that helps!";
        Assert.Equal("{\"x\": 1}", JsonExtractor.ExtractJsonBlock(input));
    }

    [Fact]
    public void ExtractJsonBlock_NestedBraces_FindsOuterBalance()
    {
        var input = "{\"outer\": {\"inner\": true}}";
        Assert.Equal(input, JsonExtractor.ExtractJsonBlock(input));
    }

    [Fact]
    public void ExtractJsonBlock_NoBraces_ReturnsNull()
    {
        Assert.Null(JsonExtractor.ExtractJsonBlock("Just plain text, no JSON here."));
    }

    [Fact]
    public void ExtractJsonBlock_Null_ReturnsNull()
    {
        Assert.Null(JsonExtractor.ExtractJsonBlock(null));
    }

    [Fact]
    public void ExtractJsonBlock_EmptyString_ReturnsNull()
    {
        Assert.Null(JsonExtractor.ExtractJsonBlock(""));
    }

    [Fact]
    public void ExtractJsonBlock_WhitespaceOnly_ReturnsNull()
    {
        Assert.Null(JsonExtractor.ExtractJsonBlock("   \n\t  "));
    }

    [Fact]
    public void ExtractJsonBlockOrThrow_ValidJson_ReturnsIt()
    {
        var json = "{\"ok\": true}";
        Assert.Equal(json, JsonExtractor.ExtractJsonBlockOrThrow(json));
    }

    [Fact]
    public void ExtractJsonBlockOrThrow_NoJson_ThrowsWithProseMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => JsonExtractor.ExtractJsonBlockOrThrow("Just prose"));
        Assert.Contains("replied in prose", ex.Message);
    }

    [Fact]
    public void ExtractJsonBlockOrThrow_Empty_ThrowsWithEmptyMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => JsonExtractor.ExtractJsonBlockOrThrow(""));
        Assert.Contains("empty response", ex.Message);
    }

    [Fact]
    public void ExtractJsonBlock_UnbalancedBraces_ReturnsNull()
    {
        // Opening brace with no matching close — should return null, not hang.
        Assert.Null(JsonExtractor.ExtractJsonBlock("{\"orphan\": true"));
    }
}
