using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The content fingerprint: what two peers can honestly compare about their content,
/// and what a cut records. The canonical text is over the ADDRESS of every entry —
/// the owning mod id, the id, the kind and the schema version — never over a
/// definition's other members, because a mod-authored definition may COMPUTE those
/// (decision 251) and a computed value is not hashable. The text sorts, and it is
/// injective: a mod id is only "non-blank" and a kind only "non-whitespace" to the
/// registration rails, so either may carry the separators the text uses.
/// </summary>
public class ModContentFingerprintTests
{
	private const string ModId = "test.fingerprint";

	[Fact]
	public void Lines_CarryTheFourAddressFieldsOfOneEntry()
	{
		// The canonical shape, pinned: the length-prefixed mod id, then the address, the
		// kind and the schema version. The length prefix is what keeps the rendering
		// injective (see the forgery case below), so it belongs in the pin.
		var line = Assert.Single(ModContentFingerprint.Lines([Entry(ModId, "a", "item", 1)]));

		Assert.Equal("16:test.fingerprint\t1:a\t4:item\t1", line);
	}

	[Fact]
	public void Lines_AddressTheCanonicalIdWhenTheModDeclaredANamespace()
	{
		// A declared namespace is part of the address the world's rows use, so it must be
		// part of what a peer compares: two copies of one mod version that disagree about
		// their namespace address their content differently.
		var line = Assert.Single(ModContentFingerprint.Lines([Entry(ModId, "a", "item", 1, @namespace: "testns")]));

		Assert.Contains("8:testns:a", line, StringComparison.Ordinal);
	}

	[Fact]
	public void Lines_SortTheirEntriesAndIgnoreRegistrationOrder()
	{
		ModContentRegistration[] scrambled = [Entry(ModId, "z", "item", 1), Entry(ModId, "a", "recipe", 2), Entry(ModId, "m", "item", 1)];
		ModContentRegistration[] ordered = [scrambled[1], scrambled[2], scrambled[0]];

		var lines = ModContentFingerprint.Lines(scrambled);

		Assert.Equal(lines, ModContentFingerprint.Lines(ordered));
		Assert.Equal([lines[0], lines[1], lines[2]], lines.OrderBy(line => line, StringComparer.Ordinal));
	}

	[Fact]
	public void Digest_MovesWithEveryAddressField()
	{
		var baseline = FingerprintOf([Entry(ModId, "a", "item", 1)]);

		// One field at a time: the id, the kind, the schema version, the owning mod and a
		// namespace that appears. Each is a real difference between two peers' content.
		Assert.NotEqual(baseline, FingerprintOf([Entry(ModId, "b", "item", 1)]));
		Assert.NotEqual(baseline, FingerprintOf([Entry(ModId, "a", "recipe", 1)]));
		Assert.NotEqual(baseline, FingerprintOf([Entry(ModId, "a", "item", 2)]));
		Assert.NotEqual(baseline, FingerprintOf([Entry("test.other", "a", "item", 1)]));
		Assert.NotEqual(baseline, FingerprintOf([Entry(ModId, "a", "item", 1, @namespace: "testns")]));
		Assert.Equal(baseline, FingerprintOf([Entry(ModId, "a", "item", 1)]));
	}

	[Fact]
	public void Digest_CannotBeForgedByAModIdThatCarriesTheSeparators()
	{
		// One mod whose ID embeds a whole second line renders — WITHOUT the length prefix —
		// as the two honest entries below, i.e. two different content sets with one text.
		// That is a difference this comparison exists to catch, so the prefix is the rule
		// rather than decoration.
		var forged = FingerprintOf([Entry("m\ta\titem\t1\nn", "b", "item", 1)]);
		var honest = FingerprintOf([Entry("m", "a", "item", 1), Entry("n", "b", "item", 1)]);

		Assert.NotEqual(honest, forged);
	}

	[Fact]
	public void ByMod_AnswersOnlyForModsThatRegisteredContent()
	{
		var store = new ModContentStore();
		store.Add("mod.a", null, new ModItemDefinition { Id = "one" });
		store.Add("mod.a", null, new ModItemDefinition { Id = "two" });
		store.Add("mod.b", null, new ModItemDefinition { Id = "three" });

		var byMod = store.ByMod;

		// The value a peer reads for a mod is the fingerprint of THAT mod's entries, and a
		// mod that registered nothing is absent — its peers send null, which the host reads
		// as "no content" rather than as an unknown value.
		Assert.Equal(2, byMod.Count);
		Assert.Equal(FingerprintOf(store.Entries.Where(entry => entry.ModId == "mod.a")), byMod["mod.a"]);
		Assert.Equal(FingerprintOf(store.Entries.Where(entry => entry.ModId == "mod.b")), byMod["mod.b"]);
		Assert.DoesNotContain("mod.c", byMod.Keys);
		Assert.NotEqual(byMod["mod.a"], byMod["mod.b"]);
	}

	[Fact]
	public void Fingerprint_CoversEveryModAndMovesWhenAnEntryIsRetired()
	{
		var store = new ModContentStore();
		store.Add("mod.a", null, new ModItemDefinition { Id = "one" });
		var single = store.Fingerprint;

		store.Add("mod.b", null, new ModItemDefinition { Id = "two" });
		var both = store.Fingerprint;

		// The whole-set value is what a manifest records: one mod's arrival moves it, and so
		// does an entry that goes away — the drift a load cannot see through per-entry salvage.
		Assert.NotEqual(single, both);
		Assert.True(store.Remove("mod.b", "two"));
		Assert.Equal(single, store.Fingerprint);
		Assert.Empty(store.ByMod.Keys.Except(["mod.a"], StringComparer.Ordinal));
	}

	[Fact]
	public void Entries_AreASnapshotThatSurvivesALaterRegistration()
	{
		var store = new ModContentStore();
		store.Add("mod.a", null, new ModItemDefinition { Id = "one" });

		var held = store.Entries;
		store.Add("mod.a", null, new ModItemDefinition { Id = "two" });

		// The read view promises a copy that is safe to hold (IModContentControl.Entries):
		// a consumer that enumerates it must not see a later registration appear in it.
		Assert.Single(held);
		Assert.Equal(2, store.Entries.Count);
	}

	[Fact]
	public void CurrentModInfos_CarriesTheFingerprintOfEachModsContent()
	{
		var registry = new ModRegistry(NullLogger<ModRegistry>.Instance);
		var discovered = registry.Discover([typeof(ModContentFingerprintTests).Assembly]);
		Assert.NotEmpty(discovered);
		var target = discovered[0].Manifest.Id;
		var store = new ModContentStore();
		var provider = new ModHandshakeListProvider(registry, store);

		// Before anything is registered every mod reports "no content" — null, never an
		// unknown value — and after the registration only the owning mod carries one.
		Assert.All(provider.CurrentModInfos(), info => Assert.Null(info.ContentFingerprint));

		store.Add(target, null, new ModItemDefinition { Id = "carried.item" });

		var infos = provider.CurrentModInfos();
		Assert.Equal(store.ByMod[target], infos.Single(info => info.Id == target).ContentFingerprint);
		Assert.All(infos.Where(info => info.Id != target), info => Assert.Null(info.ContentFingerprint));
	}

	private static ModContentRegistration Entry(string modId, string id, string kind, int schemaVersion, string? @namespace = null) =>
		new(modId, new AddressOnlyDefinition(id, kind, schemaVersion), @namespace);

	private static string FingerprintOf(IEnumerable<ModContentRegistration> entries) =>
		ModContentFingerprint.Digest(ModContentFingerprint.Lines(entries));

	/// <summary>A mod-authored definition with only the three address members — the shape a mod writes when it computes its other values.</summary>
	private sealed class AddressOnlyDefinition(string id, string kind, int schemaVersion) : IModContentDefinition
	{
		public string Id { get; } = id;

		public string Kind { get; } = kind;

		public int SchemaVersion { get; } = schemaVersion;
	}
}
