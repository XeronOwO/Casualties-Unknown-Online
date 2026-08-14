using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The saveable-field wire-kind table (extracted from ItemStateCodec): a
/// field whose type maps to Unsupported is silently skipped on capture — the
/// kind table IS the carried-state surface. Every supported kind and the
/// unsupported boundary are locked here; the adapter's enum handling (kind 6
/// stored as the underlying int) is the GunScript.roundInChamber fix.
/// </summary>
public class SaveableFieldKindTests
{
	[Theory]
	[InlineData(typeof(float), SaveableFieldKind.Float)]
	[InlineData(typeof(int), SaveableFieldKind.Int)]
	[InlineData(typeof(bool), SaveableFieldKind.Bool)]
	[InlineData(typeof(string), SaveableFieldKind.String)]
	[InlineData(typeof(List<string>), SaveableFieldKind.StringList)]
	public void SupportedTypes_MapToTheirKind(Type type, int expectedKind)
	{
		Assert.True(SaveableFieldKind.Of(type) == expectedKind,
			$"{type.Name} must map to kind {expectedKind}, got {SaveableFieldKind.Of(type)}");
	}

	[Fact]
	public void Enums_MapToTheUnderlyingIntKind()
	{
		// The GunScript.roundInChamber fix: mutable enum state must ride the
		// digest (stored as the underlying int, Enum.ToObject on restore) —
		// before kind 6 the enum was silently dropped.
		Assert.True(SaveableFieldKind.Of(typeof(DayOfWeek)) == SaveableFieldKind.Enum,
			$"an enum must map to kind {SaveableFieldKind.Enum}, got {SaveableFieldKind.Of(typeof(DayOfWeek))}");
	}

	[Theory]
	[InlineData(typeof(byte))]
	[InlineData(typeof(double))]
	[InlineData(typeof(object))]
	[InlineData(typeof(int[]))]
	[InlineData(typeof(List<int>))]
	[InlineData(typeof(int?))]
	public void UnsupportedTypes_MapToUnsupported(Type type)
	{
		// Unity references, custom types, numeric widths the game never
		// serializes — a field of any of these is skipped on capture, so the
		// mapping must never silently admit one (a wrong admit would carry a
		// garbage read).
		Assert.True(SaveableFieldKind.Of(type) == SaveableFieldKind.Unsupported,
			$"{type} must map to Unsupported, got {SaveableFieldKind.Of(type)}");
	}
}
