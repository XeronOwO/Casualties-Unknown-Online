using System;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed value model itself: construction copies what it is handed, a value
/// is immutable and structurally comparable, a wrong kind never reads as a
/// default, and the rendering is bounded so a log line cannot scale with a
/// payload.
/// </summary>
public class ModValueTests
{
	[Fact]
	public void ScalarKinds_ReadBackOnlyThroughTheirOwnAccessor()
	{
		Assert.True(ModValue.Boolean(true).TryGetBoolean(out var boolean) && boolean);
		Assert.True(ModValue.Integer(42).TryGetInteger(out var integer) && integer == 42);
		Assert.True(ModValue.Number(1.5).TryGetNumber(out var number) && number == 1.5);
		Assert.True(ModValue.Text("hello").TryGetText(out var text) && text == "hello");
		Assert.True(ModValue.Binary(new byte[] { 1, 2, 3 }).TryGetBinary(out var binary) && binary.Length == 3);

		Assert.False(ModValue.Integer(42).TryGetBoolean(out _));
		Assert.False(ModValue.Number(1.5).TryGetInteger(out _));
		Assert.False(ModValue.Text("hello").TryGetBoolean(out _));
		Assert.False(ModValue.Boolean(true).TryGetText(out _));
	}

	[Fact]
	public void AnInteger_ReadsAsANumber_ButANumberDoesNotReadAsAnInteger()
	{
		Assert.True(ModValue.Integer(7).TryGetNumber(out var number));
		Assert.Equal(7d, number);
		Assert.False(ModValue.Number(7).TryGetInteger(out _));
	}

	[Fact]
	public void Containers_ReadBackTheirMembersAndNothingElse()
	{
		var list = ModValue.List(ModValue.Integer(1), ModValue.Text("two"));
		var map = ModValue.Map(("id", ModValue.Text("cu:thing")), ("count", ModValue.Integer(2)));

		Assert.Equal(2, list.Items!.Count);
		Assert.Null(list.Fields);
		Assert.Equal(2, map.Fields!.Count);
		Assert.Null(map.Items);
		Assert.True(map.TryGetField("count", out var count) && count.TryGetInteger(out var value) && value == 2);
		Assert.False(map.TryGetField("missing", out _));
	}

	[Fact]
	public void Construction_CopiesWhatItIsHanded()
	{
		var bytes = new byte[] { 1, 2, 3 };
		var value = ModValue.Binary(bytes);

		bytes[0] = 9;
		Assert.True(value.TryGetBinary(out var read));
		Assert.Equal([1, 2, 3], read.ToArray());
	}

	[Fact]
	public void Equality_IsStructural_AndAMapIgnoresFieldOrder()
	{
		Assert.Equal(ModValue.Integer(1), ModValue.Integer(1));
		Assert.NotEqual(ModValue.Integer(1), ModValue.Number(1));
		Assert.NotEqual(ModValue.Integer(1), ModValue.Text("1"));
		Assert.Equal(
			ModValue.Map(("a", ModValue.Integer(1)), ("b", ModValue.Text("x"))),
			ModValue.Map(("b", ModValue.Text("x")), ("a", ModValue.Integer(1))));
		Assert.Equal(
			ModValue.List(ModValue.Integer(1), ModValue.Boolean(false)),
			ModValue.List(ModValue.Integer(1), ModValue.Boolean(false)));
		Assert.NotEqual(
			ModValue.List(ModValue.Integer(1)),
			ModValue.List(ModValue.Integer(1), ModValue.Integer(1)));
	}

	[Fact]
	public void ANullArgumentIsAProgrammingError()
	{
		Assert.Throws<ArgumentNullException>(() => ModValue.Text(null!));
		Assert.Throws<ArgumentNullException>(() => ModValue.List(ModValue.Integer(1), null!));
		Assert.Throws<ArgumentException>(() => ModValue.Map(("", ModValue.Integer(1))));
		Assert.Throws<ArgumentException>(() => ModValue.Map(("a", ModValue.Integer(1)), ("a", ModValue.Integer(2))));
	}

	[Fact]
	public void ToString_IsBoundedAndLogSafe()
	{
		Assert.Equal("{\"id\": \"cu:thing\", \"count\": 2}", ModValue.Map(("id", ModValue.Text("cu:thing")), ("count", ModValue.Integer(2))).ToString());
		Assert.Equal("true", ModValue.Boolean(true).ToString());
		Assert.Equal("<3 bytes>", ModValue.Binary(new byte[] { 1, 2, 3 }).ToString());

		var huge = ModValue.Text(new string('x', 4096));
		Assert.True(huge.ToString().Length <= 256, "the rendering must not scale with the payload");
	}
}
