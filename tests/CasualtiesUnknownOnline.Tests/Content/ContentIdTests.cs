using System;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Content;

/// <summary>
/// The canonical content-id vocabulary: grammar, normalisation, equality and
/// ordering. Every console resource suggestion and every namespaced mod content
/// id flows through this type, so the malformed cases are locked here.
/// </summary>
public class ContentIdTests
{
	[Theory]
	[InlineData("cu:fentanyl", "cu", "fentanyl")]
	[InlineData("CU:Fentanyl", "cu", "fentanyl")]
	[InlineData("  cu:bandage  ", "cu", "bandage")]
	[InlineData("mymod:wooden.sword", "mymod", "wooden.sword")]
	[InlineData("cu:9mm", "cu", "9mm")]
	[InlineData("a1_b2:a-b_c.9", "a1_b2", "a-b_c.9")]
	public void TryParse_AcceptsAndNormalisesCase(string text, string expectedNamespace, string expectedPath)
	{
		Assert.True(ContentId.TryParse(text, out var id));

		Assert.Equal(expectedNamespace, id.Namespace);
		Assert.Equal(expectedPath, id.Path);
		Assert.Equal($"{expectedNamespace}:{expectedPath}", id.ToString());
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("cu")]
	[InlineData(":x")]
	[InlineData("cu:")]
	[InlineData("cu:a:b")]
	[InlineData("CU Fentanyl")]
	[InlineData("cu :a")]
	[InlineData("cu: fen")]
	[InlineData("cu:fen tanyl")]
	[InlineData("cu:fen/tanyl")]
	[InlineData("cu:芬太尼")]
	[InlineData("9cu:x")]
	[InlineData("_cu:x")]
	[InlineData("cu:fen:tanyl")]
	public void TryParse_RejectsMalformedInput(string text)
	{
		Assert.False(ContentId.TryParse(text, out var id));
		Assert.Equal(default, id);
	}

	[Fact]
	public void NamespaceLengthCap_IsExact()
	{
		var maxNamespace = new string('a', ContentId.MaxNamespaceLength);
		var overNamespace = new string('a', ContentId.MaxNamespaceLength + 1);

		Assert.True(ContentId.TryParse($"{maxNamespace}:x", out _));
		Assert.False(ContentId.TryParse($"{overNamespace}:x", out _));
	}

	[Fact]
	public void PathLengthCap_IsExact()
	{
		var maxPath = new string('a', ContentId.MaxPathLength);
		var overPath = new string('a', ContentId.MaxPathLength + 1);

		Assert.True(ContentId.TryParse($"cu:{maxPath}", out _));
		Assert.False(ContentId.TryParse($"cu:{overPath}", out _));
		Assert.Equal(ContentId.MaxLength, ContentId.MaxNamespaceLength + 1 + ContentId.MaxPathLength);
	}

	[Fact]
	public void TryCreate_NormalisesCaseAndRefusesInvalidParts()
	{
		Assert.True(ContentId.TryCreate("MyMod", "Sword", out var id));
		Assert.Equal("mymod:sword", id.ToString());

		Assert.False(ContentId.TryCreate(null, "sword", out _));
		Assert.False(ContentId.TryCreate("mymod", null, out _));
		Assert.False(ContentId.TryCreate("", "sword", out _));
		Assert.False(ContentId.TryCreate("mymod", "", out _));
		Assert.False(ContentId.TryCreate("my mod", "sword", out _));
		Assert.False(ContentId.TryCreate(" cu", "sword", out _));
		Assert.False(ContentId.TryCreate("mymod", " sword", out _));
		Assert.False(ContentId.TryCreate("mymod", "sword:x", out _));
	}

	[Fact]
	public void DefaultId_IsSafeToHashAndFormat()
	{
		var id = default(ContentId);

		Assert.Equal(string.Empty, id.ToString());
		Assert.Equal(0, id.GetHashCode());
		Assert.False(id.IsBuiltIn);
		Assert.NotEqual(id, ContentId.Parse("cu:player"));
	}

	[Fact]
	public void IsValidPath_IsStrictLowercaseSoRegisteredIdsAreAlreadyCanonical()
	{
		Assert.True(ContentId.IsValidPath("wooden.sword"));
		Assert.True(ContentId.IsValidPath("a-b_c.9"));
		Assert.True(ContentId.IsValidPath("9mm"));

		Assert.False(ContentId.IsValidPath("Wooden.Sword"));
		Assert.False(ContentId.IsValidPath("bad id"));
		Assert.False(ContentId.IsValidPath("ns:path"));
		Assert.False(ContentId.IsValidPath(""));
		Assert.False(ContentId.IsValidPath(null));
	}

	[Fact]
	public void IsValidNamespace_IsStrictLowercase()
	{
		Assert.True(ContentId.IsValidNamespace("cu"));
		Assert.True(ContentId.IsValidNamespace("my_mod2"));

		Assert.False(ContentId.IsValidNamespace("Cu"));
		Assert.False(ContentId.IsValidNamespace("9mod"));
		Assert.False(ContentId.IsValidNamespace("mod!"));
		Assert.False(ContentId.IsValidNamespace(""));
		Assert.False(ContentId.IsValidNamespace(null));
	}

	[Fact]
	public void Equality_IsValueBased()
	{
		ContentId.TryParse("cu:fentanyl", out var first);
		ContentId.TryParse("CU:FENTANYL", out var second);
		ContentId.TryParse("cu:bandage", out var other);

		Assert.True(first == second);
		Assert.False(first != second);
		Assert.Equal(first.GetHashCode(), second.GetHashCode());
		Assert.True(first.Equals((object)second));
		Assert.False(first == other);
		Assert.True(other.CompareTo(first) < 0);
		Assert.True(first.CompareTo(other) > 0);
		Assert.True(first.IsBuiltIn);
		Assert.False(other == default);
	}

	[Fact]
	public void Parse_ReturnsCanonicalIdOrThrows()
	{
		Assert.Equal("cu:fentanyl", ContentId.Parse("CU:Fentanyl").ToString());
		Assert.Throws<FormatException>(() => ContentId.Parse("not-an-id"));
	}
}
