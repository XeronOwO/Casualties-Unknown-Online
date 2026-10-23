using System;
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
			RadiationLineStopsAboveTheFloor: true,
			SpectatorFreeFly: true,
			SpectateWhileUnconscious: false,
			SpectateWhileWaitingToJoin: false);

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
}
