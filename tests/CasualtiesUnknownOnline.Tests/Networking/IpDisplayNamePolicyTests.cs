using CasualtiesUnknownOnline.Runtime.Steam;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

public class IpDisplayNamePolicyTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void TryValidate_RejectsEmptyOrWhitespace(string? name)
	{
		Assert.False(IpDisplayNamePolicy.TryValidate(name, out var error));
		Assert.False(string.IsNullOrWhiteSpace(error));
	}

	[Fact]
	public void TryValidate_AcceptsMaxLengthAndTrimsNormalization()
	{
		var name = new string('a', IpDisplayNamePolicy.MaxLength);
		Assert.True(IpDisplayNamePolicy.TryValidate($"  {name}  ", out _));
		Assert.Equal(name, IpDisplayNamePolicy.Normalize($"  {name}  "));
	}

	[Fact]
	public void TryValidate_RejectsLongerThanMaxLength()
	{
		var name = new string('a', IpDisplayNamePolicy.MaxLength + 1);
		Assert.False(IpDisplayNamePolicy.TryValidate(name, out var error));
		Assert.Contains("24", error);
	}

	[Fact]
	public void TryValidate_RejectsControlCharacters()
	{
		Assert.False(IpDisplayNamePolicy.TryValidate("bad\u0007name", out var error));
		Assert.Contains("control", error);
	}
}
