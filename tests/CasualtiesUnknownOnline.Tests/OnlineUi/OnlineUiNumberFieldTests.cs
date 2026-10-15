using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The rule behind the editable value beside a slider (ticket online-ui-paired-controls-and-page-scroll, S2):
/// what text is a number, what number the field's own range allows, and how a value reads back into the box.
///
/// <para>
/// It is pure and settled here for the reason the wrap and the width rules are: the policy a player's typed
/// value is judged by is a decision about the SURFACE, and the runtime that owns the value must not disagree
/// with the field that shows it. The negatives below are the shapes that failed in every slider UI: a
/// half-typed number read as a value, a value outside the range silently kept, and a NaN that walks through
/// every comparison it is put in.
/// </para>
/// </summary>
public sealed class OnlineUiNumberFieldTests
{
	private const float Minimum = 0f;
	private const float Maximum = 3f;

	[Fact]
	public void ANumberInsideTheRangeIsTheValue() => Assert.Equal(0.8f, OnlineUiNumberField.Parse("0.8", Minimum, Maximum));

	/// <summary>The ends of the range are inside it: a rule that refused its own maximum would make the top of
	/// every range unreachable.</summary>
	[Fact]
	public void BothEndsAreAllowed()
	{
		Assert.Equal(Minimum, OnlineUiNumberField.Parse("0", Minimum, Maximum));
		Assert.Equal(Maximum, OnlineUiNumberField.Parse("3", Minimum, Maximum));
		Assert.Equal(Maximum, OnlineUiNumberField.Parse("3.00", Minimum, Maximum));
	}

	[Fact]
	public void AValueOutsideTheRangeIsRefused()
	{
		Assert.Null(OnlineUiNumberField.Parse("3.01", Minimum, Maximum));
		Assert.Null(OnlineUiNumberField.Parse("-0.01", Minimum, Maximum));
		Assert.Null(OnlineUiNumberField.Parse("1000", Minimum, Maximum));
	}

	/// <summary>The half-typed shapes a field sees on the way to a number: the empty text of a selected-all
	/// box, a lone minus, a lone decimal point, and text that is not a number at all. None of them is a value
	/// the player meant, so none of them is committed.</summary>
	[Fact]
	public void TextThatIsNotANumberYetIsRefused()
	{
		Assert.Null(OnlineUiNumberField.Parse("", Minimum, Maximum));
		Assert.Null(OnlineUiNumberField.Parse("   ", Minimum, Maximum));
		Assert.Null(OnlineUiNumberField.Parse("-", Minimum, Maximum));
		Assert.Null(OnlineUiNumberField.Parse(".", Minimum, Maximum));
		Assert.Null(OnlineUiNumberField.Parse("0.8.1", Minimum, Maximum));
		Assert.Null(OnlineUiNumberField.Parse("1,5", Minimum, Maximum));
		Assert.Null(OnlineUiNumberField.Parse("abc", Minimum, Maximum));
	}

	/// <summary>NaN and the infinities are refused whatever the range says, because every comparison against
	/// them is false: a range test alone would let them through and the session would hold a value no slider
	/// and no number can be.</summary>
	[Fact]
	public void ANonFiniteValueIsRefused()
	{
		Assert.Null(OnlineUiNumberField.Parse("NaN", Minimum, Maximum));
		Assert.Null(OnlineUiNumberField.Parse("Infinity", Minimum, Maximum));
		Assert.Null(OnlineUiNumberField.Parse("-Infinity", Minimum, Maximum));
	}

	/// <summary>The text the field holds is the shortest reading that still shows the hundredth the value can
	/// be set to, so the field, the slider's own value and the number the plugin stores are one spelling of
	/// one value.</summary>
	[Fact]
	public void AValueReadsBackInTheFieldsOwnSpelling()
	{
		Assert.Equal("0.8", OnlineUiNumberField.Format(0.8f));
		Assert.Equal("3", OnlineUiNumberField.Format(3f));
		Assert.Equal("0", OnlineUiNumberField.Format(0f));
		Assert.Equal("0.05", OnlineUiNumberField.Format(0.05f));
		Assert.Equal("1.25", OnlineUiNumberField.Format(1.25f));
		Assert.Equal("2", OnlineUiNumberField.Format(2.001f));
	}

	/// <summary>What the field shows parses back to the value it shows: the round trip is what keeps a commit
	/// of the text the player did not change from being refused as a different number.</summary>
	[Fact]
	public void WhatTheFieldShowsParsesBack()
	{
		foreach (var value in new[] { 0f, 0.05f, 0.8f, 1f, 1.25f, 2.5f, 3f })
		{
			Assert.Equal(value, OnlineUiNumberField.Parse(OnlineUiNumberField.Format(value), Minimum, Maximum));
		}
	}

	/// <summary>A range that reaches below zero is a range the spelling has to carry: the minus sign is part
	/// of the number, not noise the field is free to drop.</summary>
	[Fact]
	public void ANegativeRangeKeepsItsSign()
	{
		Assert.Equal(-1.5f, OnlineUiNumberField.Parse("-1.5", -2f, 2f));
		Assert.Equal("-1.5", OnlineUiNumberField.Format(-1.5f));
	}

	/// <summary>The parse is invariant: a machine whose own decimal separator is a comma must still read the
	/// text the field's own spelling produced, because that text travels in the model.</summary>
	[Fact]
	public void TheSpellingIsTheSameOnEveryMachine() =>
		Assert.Equal(0.5f, OnlineUiNumberField.Parse(OnlineUiNumberField.Format(0.5f), Minimum, Maximum));
}
