using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The host-rule census gate: the ONE question — does a member's client read this
/// rule? — must be answered for every rule, and a rule added later must not be able to
/// skip it. Four surfaces must agree for every rule: the
/// <see cref="IHostRules"/> property a reader calls, the census row that classifies it,
/// the <see cref="HostRuleSet"/> field an announcement carries, and the
/// <see cref="HostRulesMsg"/> field it rides on. Any one of them answering a different
/// set is a rule that reaches a member's screen with the wrong value, or a rule that
/// reaches no member while its screen still renders it.
///
/// <para>
/// The reach is derived from the source of truth (the interface) rather than from a
/// list kept beside it, and the matcher is pinned by a negative sample: the last cases
/// ask the wrong SHAPE, which must fail loudly instead of reading a neighbouring
/// field's meaning.
/// </para>
/// </summary>
public class HostRuleCensusTests
{
	[Fact]
	public void EveryHostRule_IsNamedByTheCensus()
	{
		var onTheSurface = Properties<IHostRules>().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();
		var classified = Enum.GetNames(typeof(HostRuleId)).OrderBy(name => name, StringComparer.Ordinal).ToList();

		Assert.Equal(onTheSurface, classified);
	}

	[Fact]
	public void EveryClassifiedRule_HasARowInTheCensusTable()
	{
		foreach (var rule in Rules())
		{
			var row = HostRuleCensus.Of(rule);
			Assert.Equal(rule, row.Id);
		}
	}

	[Fact]
	public void TheCensusTable_CarriesNoStaleRow()
	{
		var rows = HostRuleCensus.Rows.Select(row => row.Id).ToList();

		Assert.Equal(rows.Count, rows.Distinct().Count());
		Assert.Equal(Rules().Count, rows.Count);
	}

	[Fact]
	public void EveryClassifiedRule_HasAFieldOnTheAnnouncedSet()
	{
		var onTheSurface = Properties<IHostRules>().Select(property => (property.Name, property.PropertyType)).OrderBy(pair => pair.Name, StringComparer.Ordinal).ToList();
		var inTheSet = Properties<HostRuleSet>().Select(property => (property.Name, property.PropertyType)).OrderBy(pair => pair.Name, StringComparer.Ordinal).ToList();

		Assert.Equal(onTheSurface, inTheSet);
	}

	[Fact]
	public void EveryMemberRule_HasAFieldOnTheWireMessage()
	{
		var carried = HostRuleCensus.MemberRows.Select(row => row.Id).ToHashSet();
		var wired = Properties<HostRulesMsg>().Select(property => property.Name).ToHashSet();

		foreach (var rule in Rules())
		{
			Assert.Equal(carried.Contains(rule), wired.Contains(rule.ToString()));
		}
	}

	[Fact]
	public void EveryMemberRule_WireFieldCarriesTheRulesValueShape()
	{
		// The name check above catches a rule whose field is missing; this one catches a field
		// whose TYPE drifted. An enum-valued rule travels as its integer value, so the wire type
		// is int for an enum and the property's own type otherwise (the bool-to-int change of the
		// remote-backpack rule is the shape this pins).
		var surface = Properties<IHostRules>().ToDictionary(property => property.Name, property => property.PropertyType);
		var wire = Properties<HostRulesMsg>().ToDictionary(property => property.Name, property => property.PropertyType);

		foreach (var rule in HostRuleCensus.MemberRows)
		{
			var name = rule.Id.ToString();
			Assert.True(wire.TryGetValue(name, out var wireType), $"{name} has no wire field on {nameof(HostRulesMsg)}");
			var surfaceType = surface[name];
			Assert.Equal(surfaceType.IsEnum ? typeof(int) : surfaceType, wireType);
		}
	}

	[Fact]
	public void EveryCensusLabel_IsTranslatedInBothLanguages()
	{
		foreach (var row in HostRuleCensus.Rows)
		{
			Assert.True(LocalizationCatalog.English.ContainsKey(row.LabelKey), $"{row.Id} names the label key '{row.LabelKey}', which the English catalogue does not carry.");
			Assert.True(LocalizationCatalog.Chinese.ContainsKey(row.LabelKey), $"{row.Id} names the label key '{row.LabelKey}', which the Chinese catalogue does not carry.");
		}
	}

	[Fact]
	public void MemberRows_AreExactlyTheMemberAudience()
	{
		var expected = HostRuleCensus.Rows.Where(row => row.Audience == HostRuleCensus.Audience.Members).Select(row => row.Id).ToList();

		Assert.Equal(expected, HostRuleCensus.MemberRows.Select(row => row.Id).ToList());
	}

	[Fact]
	public void AnUnclassifiedRule_IsRefusedByName()
	{
		var refused = Assert.Throws<ArgumentOutOfRangeException>(() => HostRuleCensus.Of((HostRuleId)999));

		Assert.Contains("no census row", refused.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void TheNumberRule_IsNotReadAsAFlag()
	{
		var set = Set();
		Assert.Throws<ArgumentOutOfRangeException>(() => set.Boolean(HostRuleId.PiggybackWeightMultiplier));
		Assert.Throws<ArgumentOutOfRangeException>(() => set.Boolean(HostRuleId.NativeBindingParity));
		Assert.Throws<ArgumentOutOfRangeException>(() => set.Boolean(HostRuleId.RemoteBackpackAccess));
	}

	[Fact]
	public void TheParityRule_IsNotReadAsANumber()
	{
		var set = Set();
		Assert.Throws<ArgumentOutOfRangeException>(() => set.Number(HostRuleId.NativeBindingParity));
		Assert.Throws<ArgumentOutOfRangeException>(() => set.Parity(HostRuleId.AllowLateJoin));
		Assert.Throws<ArgumentOutOfRangeException>(() => set.Number(HostRuleId.RemoteBackpackAccess));
		Assert.Throws<ArgumentOutOfRangeException>(() => set.Access(HostRuleId.NativeBindingParity));
		Assert.Throws<ArgumentOutOfRangeException>(() => set.Access(HostRuleId.AllowLateJoin));
	}

	[Fact]
	public void TheCensus_NamesEveryRuleItsOwnValueShape()
	{
		var set = Set();

		Assert.True(set.Boolean(HostRuleId.Permadeath));
		Assert.Equal(1.25f, set.Number(HostRuleId.PiggybackWeightMultiplier));
		Assert.Equal(NativeBindingParity.Require, set.Parity(HostRuleId.NativeBindingParity));
		Assert.Equal(RemoteBackpackAccess.UnconsciousOrDeadOnly, set.Access(HostRuleId.RemoteBackpackAccess));
	}

	[Fact]
	public void TheAnnouncedSet_RoundTripsThroughTheWireMessage()
	{
		var set = Set();

		Assert.Equal(set, HostRulesMsg.From(set).ToSet());
	}

	[Fact]
	public void EveryRuleName_IsLowercaseSnakeCase()
	{
		foreach (var row in HostRuleCensus.Rows)
		{
			Assert.Matches("^[a-z][a-z0-9]*(_[a-z0-9]+)*$", row.Name);
		}
	}

	[Fact]
	public void EveryRuleName_IsUniqueAndFoundByItsExactSpelling()
	{
		var names = HostRuleCensus.Rows.Select(row => row.Name).ToList();
		Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());

		foreach (var row in HostRuleCensus.Rows)
		{
			Assert.Same(row, HostRuleCensus.Find(row.Name));

			// The config spelling and an upper-case spelling are not second accepted spellings: the
			// verb's arguments are case-sensitive and the console names are the rule's own spelling.
			Assert.Null(HostRuleCensus.Find(row.Id.ToString()));
			Assert.Null(HostRuleCensus.Find(row.Name.ToUpperInvariant()));
		}
	}

	[Fact]
	public void TheNamesList_IsEveryNameSorted()
	{
		var expected = HostRuleCensus.Rows.Select(row => row.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();

		Assert.Equal(expected, HostRuleCensus.Names);
	}

	[Fact]
	public void OnlyTheNumericRule_DeclaresAValueRange()
	{
		foreach (var row in HostRuleCensus.Rows)
		{
			Assert.Equal(row.Kind == HostRuleKind.Number, row.Range.HasValue);
		}

		var range = HostRuleCensus.Of(HostRuleId.PiggybackWeightMultiplier).Range!.Value;
		Assert.Equal(0f, range.Minimum);
		Assert.Equal(3f, range.Maximum);
	}

	/// <summary>A set with every rule away from its default, so a mapping that quietly reads the wrong field cannot pass.</summary>
	private static HostRuleSet Set() => new(
		PvpEnabled: true,
		AutoContinue: true,
		AllowLateJoin: false,
		RemoteBackpackAccess: RemoteBackpackAccess.UnconsciousOrDeadOnly,
		AllowMemberCheatVerbs: true,
		WidenRunSettings: false,
		PiggybackWeightMultiplier: 1.25f,
		NativeBindingParity: NativeBindingParity.Require,
		SaveInventory: false,
		ReviveFromTrader: false,
		ReviveOnNextLevel: false,
		Permadeath: true,
		RadiationLineStopsAboveTheFloor: false,
		LayerCompleteLivingShare: 0.5f);

	private static IReadOnlyList<PropertyInfo> Properties<T>() =>
		[.. typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.GetIndexParameters().Length == 0)];

	private static List<HostRuleId> Rules() => [.. Enum.GetValues(typeof(HostRuleId)).Cast<HostRuleId>()];
}
