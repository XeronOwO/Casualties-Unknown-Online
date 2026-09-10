using System;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// Decision 162: the player key is transport-scoped. Steam keys on the account,
/// IP-direct mode on the display name — two distinct key spaces, so a Steam world
/// is never claimed by an IP-direct name collision — and the key is always safe to
/// use as a file name under <c>characters/</c>.
///
/// The identity requirement is the stronger one: two DIFFERENT players must never
/// share a key, because they would overwrite each other's character.
/// </summary>
public class PlayerKeyTests
{
	[Fact]
	public void SteamKey_IsTheAccountPrefixed()
	{
		Assert.Equal("steam-76561198000000000", PlayerKey.ForSteam(76561198000000000UL));
		Assert.StartsWith(PlayerKey.SteamPrefix, PlayerKey.ForSteam(1));
	}

	[Fact]
	public void DisplayNameKey_IsSanitizedAndPrefixed()
	{
		Assert.Equal("name-alice", PlayerKey.ForDisplayName("Alice"));
		Assert.Equal("name-alice", PlayerKey.ForDisplayName("  Alice  "));
		Assert.Equal("name-alice", PlayerKey.ForDisplayName("ALICE"));
		Assert.Equal("name-alice-bob", PlayerKey.ForDisplayName("Alice   Bob"));
		Assert.Equal("name-alice-bob", PlayerKey.ForDisplayName("Alice-Bob"));
	}

	[Fact]
	public void TheTwoSpacesNeverCollideEvenForTheSameText()
	{
		var steam = PlayerKey.ForSteam(76561198000000000UL);
		var name = PlayerKey.ForDisplayName("76561198000000000");

		Assert.NotEqual(steam, name);
		Assert.StartsWith(PlayerKey.SteamPrefix, steam, StringComparison.Ordinal);
		Assert.StartsWith(PlayerKey.NamePrefix, name, StringComparison.Ordinal);
	}

	public static TheoryData<string, string> HostileNames
	{
		get
		{
			var data = new TheoryData<string, string>();
			data.Add("../../evil", "name-evil");
			data.Add(HostilePaths.DriveAbsolutePayload, "name-q-evil-txt");
			data.Add("..", "name-unnamed-x5ec1f7e7");
			data.Add(string.Empty, "name-unnamed");
			data.Add("   ", "name-unnamed");
			data.Add("a/b\\c:d*e?f\"g<h>i|j", "name-a-b-c-d-e-f-g-h-i-j");
			return data;
		}
	}

	[Theory]
	[MemberData(nameof(HostileNames))]
	public void HostileDisplayNames_ProduceASafeDeterministicKey(string displayName, string expected)
	{
		var key = PlayerKey.ForDisplayName(displayName);

		Assert.Equal(expected, key);
		Assert.Null(ArchivePathPolicy.DescribeUnsafePath(SaveArchiveFormat.CharactersFolderName + "/" + key + ".json"));
		Assert.DoesNotContain("..", key, StringComparison.Ordinal);
		Assert.DoesNotContain("/", key, StringComparison.Ordinal);
		Assert.DoesNotContain("\\", key, StringComparison.Ordinal);
	}

	[Fact]
	public void EveryKeyFitsItsPathBudget()
	{
		string[] names = [string.Empty, "..", "  ", new string('a', 400), "玩家一号", "Alice (玩家二号)", new string('9', 200)];

		Assert.All(names, name =>
		{
			var key = PlayerKey.ForDisplayName(name);
			Assert.True(key.Length <= PlayerKey.NamePrefix.Length + PlayerKey.MaxSanitizedLength + PlayerKey.DigestMarker.Length + 8, key);
			Assert.Null(ArchivePathPolicy.DescribeUnsafePath(SaveArchiveFormat.CharactersFolderName + "/" + key + ".json"));
		});
	}

	[Fact]
	public void DistinctNonAsciiDisplayNames_StayDistinctKeys()
	{
		var first = PlayerKey.ForDisplayName("玩家一号");
		var second = PlayerKey.ForDisplayName("玩家二号");

		Assert.NotEqual(first, second);
		Assert.StartsWith(PlayerKey.NamePrefix, first, StringComparison.Ordinal);
		Assert.Null(ArchivePathPolicy.DescribeUnsafePath(SaveArchiveFormat.CharactersFolderName + "/" + first + ".json"));
		Assert.Equal(-1, first.IndexOfAny(Path.GetInvalidFileNameChars()));
		Assert.Equal(first, PlayerKey.ForDisplayName("玩家一号"));
	}

	[Fact]
	public void NonAsciiNamesThatShareALatinPart_StayDistinctKeys()
	{
		var first = PlayerKey.ForDisplayName("Alice (玩家一号)");
		var second = PlayerKey.ForDisplayName("Alice (玩家二号)");

		Assert.NotEqual(first, second);
		Assert.StartsWith("name-alice", first, StringComparison.Ordinal);
	}

	[Fact]
	public void NamesThatDifferOnlyInCaseOrSpacing_AreTheSamePlayer()
	{
		Assert.Equal(PlayerKey.ForDisplayName("Alice Bob"), PlayerKey.ForDisplayName("alice   bob"));
		Assert.Equal(PlayerKey.ForDisplayName("Alice"), PlayerKey.ForDisplayName("  aLiCe  "));
	}

	[Fact]
	public void LongDisplayNames_StayDistinctAfterTruncation()
	{
		var first = PlayerKey.ForDisplayName(new string('a', 400) + "one");
		var second = PlayerKey.ForDisplayName(new string('a', 400) + "two");

		Assert.NotEqual(first, second);
		Assert.StartsWith(PlayerKey.NamePrefix + new string('a', PlayerKey.MaxSanitizedLength), first, StringComparison.Ordinal);
		Assert.True(first.Length <= PlayerKey.NamePrefix.Length + PlayerKey.MaxSanitizedLength + 16, first);
		Assert.Null(ArchivePathPolicy.DescribeUnsafePath(SaveArchiveFormat.CharactersFolderName + "/" + first + ".json"));
	}

	[Fact]
	public void CharacterFileKey_RoundTripsThroughTheReaderHelper()
	{
		var key = PlayerKey.ForSteam(76561198000000000UL);
		var file = new SnapshotFile(
			SaveArchiveFormat.CharactersFolderName + "/" + key + ".json",
			SaveTestData.Bytes("{\"schemaVersion\":1}"),
			new SaveManifest.SaveManifestFile(SaveArchiveFormat.CharactersFolderName + "/" + key + ".json", new string('0', 64), 19));

		Assert.True(SaveArchiveReader.IsCharacterFile(file));
		Assert.Equal(key, SaveArchiveReader.CharacterKeyOf(file));
		Assert.False(SaveArchiveReader.IsCharacterFile(new SnapshotFile("items.json", [], file.ManifestEntry)));
	}
}
