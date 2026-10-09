using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The framework's own encoding of a value: every kind round-trips, the budgets
/// refuse a value that cannot travel with a refusal that NAMES the path inside
/// the model, and a malformed payload is refused rather than recursed into.
/// </summary>
public class ModValueCodecTests
{
	private const int Rail = ModChannel.MaxPayloadBytes;

	private static byte[] Encode(ModValue value)
	{
		Assert.True(ModValueCodec.TryEncode(value, Rail, out var encoded, out var refusal), refusal);
		return encoded;
	}

	private static void RoundTrips(ModValue value)
	{
		var encoded = Encode(value);
		Assert.True(ModValueCodec.TryDecode(encoded, Rail, out var decoded, out var refusal), refusal);
		Assert.Equal(value, decoded);
	}

	[Fact]
	public void EveryKind_RoundTrips()
	{
		RoundTrips(ModValue.Boolean(true));
		RoundTrips(ModValue.Boolean(false));
		RoundTrips(ModValue.Integer(0));
		RoundTrips(ModValue.Integer(long.MinValue));
		RoundTrips(ModValue.Integer(long.MaxValue));
		RoundTrips(ModValue.Number(-0.125));
		RoundTrips(ModValue.Number(double.Epsilon));
		RoundTrips(ModValue.Text(string.Empty));
		RoundTrips(ModValue.Text("text with \"quotes\", \ttabs and ☃ an emoji"));
		RoundTrips(ModValue.Binary(new byte[0]));
		RoundTrips(ModValue.Binary(new byte[] { 0, 1, 2, 255 }));
	}

	[Fact]
	public void Containers_RoundTripIncludingNestingAndEmptyOnes()
	{
		RoundTrips(ModValue.List());
		RoundTrips(ModValue.Map());
		RoundTrips(ModValue.List(ModValue.Integer(1), ModValue.Text("two"), ModValue.Boolean(false)));
		RoundTrips(ModValue.Map(("a", ModValue.Integer(1)), ("b", ModValue.List(ModValue.Map(("deep", ModValue.Text("yes")))))));
	}

	[Fact]
	public void ACarriedValue_SurvivesAModStyleRoundTrip()
	{
		var value = ModValue.Map(
			("steam", ModValue.Integer(76561198000000001L)),
			("amount", ModValue.Number(0.5)),
			("tags", ModValue.List(ModValue.Text("cu:one"), ModValue.Text("cu:two"))),
			("blob", ModValue.Binary(new byte[] { 9, 8, 7 })));

		RoundTrips(value);
	}

	// ---- Budgets: refusing is a named path, not a crash ----

	[Fact]
	public void AValueOverTheSurfacesOwnRail_IsRefusedByNameAndSize()
	{
		Assert.False(ModValueCodec.TryEncode(ModValues.OverCap(), Rail, out _, out var refusal));
		Assert.Contains("the cap is", refusal);
		Assert.StartsWith("$", refusal);
	}

	[Fact]
	public void ADepthBeyondTheBudget_IsRefusedWithThePathThatHitIt()
	{
		var value = ModValue.Integer(1);
		for (var i = 0; i <= ModValueCodec.MaxDepth; i++)
		{
			value = ModValue.List(value);
		}

		Assert.False(ModValueCodec.TryEncode(value, Rail, out _, out var refusal));
		Assert.Contains("nests deeper", refusal);
		Assert.Contains("[0]", refusal);
	}

	[Fact]
	public void AContainerBeyondTheEntryBudget_IsRefused()
	{
		var items = new ModValue[ModValueCodec.MaxEntries + 1];
		for (var i = 0; i < items.Length; i++)
		{
			items[i] = ModValue.Integer(i);
		}

		Assert.False(ModValueCodec.TryEncode(ModValue.List(items), Rail, out _, out var refusal));
		Assert.Contains("element cap", refusal);
	}

	[Fact]
	public void ATextBeyondItsBudget_IsRefusedWithThePathInsideTheModel()
	{
		var value = ModValue.Map(("targets", ModValue.List(ModValue.Text(new string('x', ModValueCodec.MaxTextBytes + 1)))));

		Assert.False(ModValueCodec.TryEncode(value, Rail, out _, out var refusal));
		Assert.Contains("$.targets[0]", refusal);
		Assert.Contains("the cap is", refusal);
	}

	[Fact]
	public void ANonFiniteNumber_IsRefused()
	{
		Assert.False(ModValueCodec.TryEncode(ModValue.Number(double.NaN), Rail, out _, out var nan));
		Assert.Contains("must be finite", nan);
		Assert.False(ModValueCodec.TryEncode(ModValue.Number(double.PositiveInfinity), Rail, out _, out var infinity));
		Assert.Contains("must be finite", infinity);
	}

	[Fact]
	public void TextThatIsNotValidUtf16_IsRefused()
	{
		Assert.False(ModValueCodec.TryEncode(ModValue.Text("\ud800"), Rail, out _, out var refusal));
		Assert.Contains("UTF-16", refusal);
	}

	[Fact]
	public void ABinaryLeafBeyondItsBudget_IsRefused()
	{
		Assert.False(ModValueCodec.TryEncode(
			ModValue.Binary(new byte[ModValueCodec.MaxBinaryBytes + 1]), Rail, out _, out var refusal));
		Assert.Contains("binary leaf", refusal);
	}

	[Fact]
	public void AFieldNameBeyondItsBudget_IsRefused()
	{
		var value = ModValue.Map((new string('k', ModValueCodec.MaxKeyBytes + 1), ModValue.Integer(1)));

		Assert.False(ModValueCodec.TryEncode(value, Rail, out _, out var refusal));
		Assert.Contains("field name", refusal);
	}

	// ---- Decode: a malformed payload is refused, never recursed into ----

	[Fact]
	public void AnEmptyOrTruncatedPayload_IsRefused()
	{
		Assert.False(ModValueCodec.TryDecode([], Rail, out _, out var empty));
		Assert.StartsWith("$", empty);

		var encoded = Encode(ModValue.Text("hello"));
		Assert.False(ModValueCodec.TryDecode(encoded.Take(3).ToArray(), Rail, out _, out var cutLength));
		Assert.Contains("ends", cutLength);
		Assert.False(ModValueCodec.TryDecode(encoded.Take(encoded.Length - 1).ToArray(), Rail, out _, out var cutBody));
		Assert.Contains("bytes left in the payload", cutBody);
	}

	[Fact]
	public void AnEmptyFieldNameInAPayload_IsRefusedRatherThanThrown()
	{
		// A map with one entry whose name is empty. The MODEL refuses an empty
		// name, so the DECODER must refuse the payload instead of letting that
		// refusal leave as an exception through a `Try` method.
		var payload = new byte[] { 0x07, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
		Assert.False(ModValueCodec.TryDecode(payload, Rail, out _, out var refusal));
		Assert.StartsWith("$[0]", refusal);
		Assert.Contains("must not be empty", refusal);
	}

	[Fact]
	public void ANegativeOrOversizedEntryCount_IsRefused()
	{
		Assert.False(ModValueCodec.TryDecode([0x06, 0xff, 0xff, 0xff, 0xff], Rail, out _, out var negative));
		Assert.Contains("entry cap", negative);

		Assert.False(ModValueCodec.TryDecode([0x07, 0x01, 0x04, 0x00, 0x00], Rail, out _, out var oversized));
		Assert.Contains("entry cap", oversized);
	}

	[Fact]
	public void TrailingBytes_AreRefused()
	{
		var encoded = Encode(ModValue.Integer(1)).Concat(new byte[] { 0 }).ToArray();

		Assert.False(ModValueCodec.TryDecode(encoded, Rail, out _, out var refusal));
		Assert.Contains("after the value", refusal);
	}

	[Fact]
	public void AnUnknownTag_IsRefusedByName()
	{
		Assert.False(ModValueCodec.TryDecode([0x7f], Rail, out _, out var refusal));
		Assert.Contains("not a value tag", refusal);
	}

	[Fact]
	public void ADeclaredLengthBeyondThePayload_IsRefusedBeforeAnythingIsAllocated()
	{
		// A text tag whose length claims 16 MiB with four bytes behind it.
		var payload = new byte[] { 0x04, 0x00, 0x00, 0x00, 0x01, 0x41 };

		Assert.False(ModValueCodec.TryDecode(payload, Rail, out _, out var refusal));
		Assert.Contains("bytes left in the payload", refusal);
	}

	[Fact]
	public void ADuplicateFieldInAPayload_IsRefused()
	{
		var first = Encode(ModValue.Map(("a", ModValue.Integer(1))));
		var second = Encode(ModValue.Map(("a", ModValue.Integer(2))));
		var payload = new List<byte> { 0x07, 0x02, 0x00, 0x00, 0x00 };
		payload.AddRange(first.Skip(5));
		payload.AddRange(second.Skip(5));

		Assert.False(ModValueCodec.TryDecode([.. payload], Rail, out _, out var refusal));
		Assert.Contains("twice", refusal);
	}

	[Fact]
	public void APayloadOverTheRail_IsRefusedBeforeItIsWalked()
	{
		Assert.False(ModValueCodec.TryDecode(new byte[Rail + 1], Rail, out _, out var refusal));
		Assert.Contains("the payload is", refusal);
	}
}
