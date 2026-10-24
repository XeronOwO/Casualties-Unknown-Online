using System;
using System.Linq;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The host-rule value grammar the console's <c>/gamerule</c> verb speaks: the value each rule kind
/// accepts, the canonical text a write carries, the value a query prints, and the names a refusal
/// lists. The choices are checked against the text forms the config itself uses, so a level added to
/// either closed set cannot leave the console behind.
/// </summary>
public class HostRuleValueTextTests
{
	private static HostRuleCensus.Row Flag => HostRuleCensus.Of(HostRuleId.PvpEnabled);

	private static HostRuleCensus.Row Number => HostRuleCensus.Of(HostRuleId.PiggybackWeightMultiplier);

	private static HostRuleCensus.Row Parity => HostRuleCensus.Of(HostRuleId.NativeBindingParity);

	private static HostRuleCensus.Row Access => HostRuleCensus.Of(HostRuleId.RemoteBackpackAccess);

	[Fact]
	public void Choices_AreTheConfigsOwnTextForms()
	{
		Assert.Equal(["true", "false"], HostRuleValueText.Choices(Flag));
		Assert.Equal(["allow", "warn", "require"], HostRuleValueText.Choices(Parity));
		Assert.Equal(["allow", "unconscious_or_dead", "deny"], HostRuleValueText.Choices(Access));
		Assert.Empty(HostRuleValueText.Choices(Number));

		// Every named choice is accepted exactly as listed and round-trips to itself.
		foreach (var row in new[] { Flag, Parity, Access })
		{
			foreach (var choice in HostRuleValueText.Choices(row))
			{
				Assert.True(HostRuleValueText.TryParse(row, choice, out var value, out var error), $"{row.Name} should accept '{choice}': {error}");
				Assert.Equal(choice, value);
			}
		}
	}

	[Theory]
	[InlineData("true", true)]
	[InlineData("false", false)]
	public void FlagValues_AreAccepted(string text, bool expected)
	{
		Assert.True(HostRuleValueText.TryParse(Flag, text, out var value, out var error), error);
		Assert.Equal(expected ? "true" : "false", value);
	}

	[Theory]
	[InlineData("True")]
	[InlineData("FALSE")]
	[InlineData("1")]
	[InlineData("yes")]
	public void FlagValues_AreCaseSensitiveAndNamedOnRefusal(string text)
	{
		Assert.False(HostRuleValueText.TryParse(Flag, text, out _, out var error));
		Assert.Equal($"'{text}' is not one of true, false.", error);
	}

	[Theory]
	[InlineData("allow")]
	[InlineData("unconscious_or_dead")]
	[InlineData("deny")]
	public void AccessValues_AreAccepted(string text)
	{
		Assert.True(HostRuleValueText.TryParse(Access, text, out var value, out var error), error);
		Assert.Equal(text, value);
	}

	[Theory]
	[InlineData("ALLOW")]
	[InlineData("unconscious")]
	[InlineData("unconscious-or-dead")]
	public void AccessValues_AreRefusedByName(string text)
	{
		Assert.False(HostRuleValueText.TryParse(Access, text, out _, out var error));
		Assert.Equal($"'{text}' is not one of allow, unconscious_or_dead, deny.", error);
	}

	[Theory]
	[InlineData("allow")]
	[InlineData("warn")]
	[InlineData("require")]
	public void ParityValues_AreAccepted(string text)
	{
		Assert.True(HostRuleValueText.TryParse(Parity, text, out var value, out var error), error);
		Assert.Equal(text, value);
	}

	[Fact]
	public void ParityValues_AreRefusedByName()
	{
		Assert.False(HostRuleValueText.TryParse(Parity, "strict", out _, out var error));
		Assert.Equal("'strict' is not one of allow, warn, require.", error);
	}

	[Theory]
	[InlineData("0.8", "0.8")]
	[InlineData("0.50", "0.5")]
	[InlineData("0.1", "0.1")]
	[InlineData("1.2345", "1.2345")]
	[InlineData("1", "1")]
	[InlineData("3", "3")]
	[InlineData("0", "0")]
	public void NumberValues_AreCanonicalized(string text, string expected)
	{
		Assert.True(HostRuleValueText.TryParse(Number, text, out var value, out var error), error);
		Assert.Equal(expected, value);
	}

	[Theory]
	[InlineData("5")]
	[InlineData("-0.1")]
	[InlineData("3.0001")]
	[InlineData("abc")]
	[InlineData("NaN")]
	[InlineData("")]
	public void NumberValues_OutsideTheDeclaredRangeAreRefusedWithTheRange(string text)
	{
		Assert.False(HostRuleValueText.TryParse(Number, text, out _, out var error));
		Assert.Equal($"'{text}' is not a number between 0 and 3.", error);
	}

	[Fact]
	public void Describe_PrintsWhatTheParserAcceptsBack()
	{
		var rules = new HostRuleSet(
			PvpEnabled: true,
			AutoContinue: false,
			AllowLateJoin: true,
			RemoteBackpackAccess: RemoteBackpackAccess.UnconsciousOrDeadOnly,
			AllowMemberCheatVerbs: false,
			WidenRunSettings: true,
			PiggybackWeightMultiplier: 0.8f,
			NativeBindingParity: NativeBindingParity.Require,
			SaveInventory: true,
			ReviveFromTrader: false,
			ReviveOnNextLevel: true,
			Permadeath: false,
			RadiationLineStopsAboveTheFloor: true);

		Assert.Equal("true", HostRuleValueText.Describe(rules, Flag));
		Assert.Equal("unconscious_or_dead", HostRuleValueText.Describe(rules, Access));
		Assert.Equal("require", HostRuleValueText.Describe(rules, Parity));
		Assert.Equal("0.8", HostRuleValueText.Describe(rules, Number));

		foreach (var row in new[] { Flag, Access, Parity, Number })
		{
			var printed = HostRuleValueText.Describe(rules, row);
			Assert.True(HostRuleValueText.TryParse(row, printed, out var value, out var error), $"{row.Name} should accept its own printed value: {error}");
			Assert.Equal(printed, value);
		}
	}

	[Fact]
	public void AFlagValue_IsNotAcceptedByANamedRule()
	{
		Assert.False(HostRuleValueText.TryParse(Access, "true", out _, out var error));
		Assert.Contains("is not one of allow, unconscious_or_dead, deny", error, StringComparison.Ordinal);
	}

	[Fact]
	public void ChoicesForClamp_PutTheConservativeValueFirst()
	{
		// BepInEx replaces a config text the acceptable-value list does not accept with the list's FIRST
		// entry (a typo, or a different capitalisation), so the first entry is what such a value clamps to
		// and each named rule declares the fail-closed one.
		Assert.Equal(["deny", "allow", "unconscious_or_dead"], HostRuleValueText.ChoicesForClamp(Access));
		Assert.Equal(["warn", "allow", "require"], HostRuleValueText.ChoicesForClamp(Parity));

		// The SET is the census's own — only the order differs from the console's canonical order.
		Assert.Equal(
			HostRuleValueText.Choices(Access).OrderBy(choice => choice, StringComparer.Ordinal),
			HostRuleValueText.ChoicesForClamp(Access).OrderBy(choice => choice, StringComparer.Ordinal));
		Assert.Equal(
			HostRuleValueText.Choices(Parity).OrderBy(choice => choice, StringComparer.Ordinal),
			HostRuleValueText.ChoicesForClamp(Parity).OrderBy(choice => choice, StringComparer.Ordinal));
	}

	[Fact]
	public void TheClampedFirstValues_AreTheParsersOwnConservativeAnswers()
	{
		// The seam the clamp feeds: index 0 is a real choice, and the text parser the plugin runs maps it
		// to the same conservative member the plugin's own fallback returns. A census edit that moved the
		// conservative value would fail here instead of silently widening the rule.
		Assert.Equal("deny", HostRuleValueText.ChoicesForClamp(Access)[0]);
		Assert.True(RemoteBackpackAccessText.TryParse("deny", out var access));
		Assert.Equal(RemoteBackpackAccess.Deny, access);

		Assert.Equal("warn", HostRuleValueText.ChoicesForClamp(Parity)[0]);
		Assert.True(NativeBindingParityText.TryParse("warn", out var parity));
		Assert.Equal(NativeBindingParity.Warn, parity);
	}

	[Fact]
	public void TheBoundAcceptableList_ClampsAnUnrecognisedTextToTheFallback()
	{
		// The external behaviour the fix depends on, driven through BepInEx's own type: an unrecognised
		// text becomes the list's FIRST entry. This is the seam a hand-edited file goes through before any
		// CUO parser runs, so the order ChoicesForClamp returns is the whole fix.
		var accessList = new AcceptableValueList<string>([.. HostRuleValueText.ChoicesForClamp(Access)]);
		var parityList = new AcceptableValueList<string>([.. HostRuleValueText.ChoicesForClamp(Parity)]);

		Assert.Equal("deny", (string)accessList.Clamp("Deny"));
		Assert.Equal("deny", (string)accessList.Clamp("unconcious"));
		Assert.Equal("warn", (string)parityList.Clamp("Require"));
		Assert.Equal("warn", (string)parityList.Clamp("strict"));

		// A recognised value is untouched, including the canonical spelling.
		Assert.Equal("allow", (string)accessList.Clamp("allow"));
		Assert.Equal("deny", (string)accessList.Clamp("deny"));
		Assert.Equal("allow", (string)parityList.Clamp("allow"));
		Assert.Equal("require", (string)parityList.Clamp("require"));
	}

	[Fact]
	public void ARowWithoutAConservativeValue_KeepsTheCanonicalOrder()
	{
		Assert.Null(Flag.ConservativeValue);
		Assert.Null(Number.ConservativeValue);
		Assert.Equal(HostRuleValueText.Choices(Flag), HostRuleValueText.ChoicesForClamp(Flag));
		Assert.Empty(HostRuleValueText.ChoicesForClamp(Number));
	}

	[Fact]
	public void AConservativeValue_IsDeclaredExactlyForTheTwoNamedSets()
	{
		// The census question is asked of EVERY row: a future access/parity row cannot arrive without a
		// declared fallback, and a flag/number row may not acquire one by accident.
		foreach (var row in HostRuleCensus.Rows)
		{
			var needsFallback = row.Kind is HostRuleKind.Access or HostRuleKind.Parity;
			Assert.Equal(needsFallback, row.ConservativeValue is not null);
		}

		Assert.Equal("deny", HostRuleCensus.Of(HostRuleId.RemoteBackpackAccess).ConservativeValue);
		Assert.Equal("warn", HostRuleCensus.Of(HostRuleId.NativeBindingParity).ConservativeValue);

		foreach (var row in HostRuleCensus.Rows.Where(row => row.ConservativeValue is not null))
		{
			Assert.Contains(row.ConservativeValue!, HostRuleValueText.Choices(row));
			Assert.Equal(row.ConservativeValue, HostRuleValueText.ChoicesForClamp(row)[0]);
		}
	}

	[Fact]
	public void ACensusRowWhoseConservativeValueIsNotAChoice_IsRefusedNotSilentlyAccepted()
	{
		var bad = new HostRuleCensus.Row(
			HostRuleId.PvpEnabled,
			HostRuleCensus.Audience.Members,
			HostRuleKind.Boolean,
			"pvp_enabled",
			"admin.rule_pvp",
			ConservativeValue: "yes");

		Assert.Throws<InvalidOperationException>(() => HostRuleValueText.ChoicesForClamp(bad));
	}
}
