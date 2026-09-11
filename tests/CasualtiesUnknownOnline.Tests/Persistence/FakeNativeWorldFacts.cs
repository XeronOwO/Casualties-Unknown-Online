using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// In-memory stand-in for the adapter's native world-fact tables (keypad codes
/// and geyser liquid types) — the optional half of the world-fact seam. It
/// records its calls so a save suite can prove which half of the restore the
/// Runtime owns and which half it hands over.
/// </summary>
internal sealed class FakeNativeWorldFacts : INativeWorldFacts
{
	private readonly List<KeypadEntryMsg> _keypads = [];
	private readonly List<GeyserStateEntryMsg> _geysers = [];

	internal List<string> Calls { get; } = [];

	internal IReadOnlyList<KeypadEntryMsg> Keypads => _keypads;

	internal IReadOnlyList<GeyserStateEntryMsg> Geysers => _geysers;

	internal void SeedKeypad(float x, float y, string code) =>
		_keypads.Add(new KeypadEntryMsg { Position = new NetVector2Msg(x, y), Code = code });

	internal void SeedGeyser(float x, float y, byte liquidType) =>
		_geysers.Add(new GeyserStateEntryMsg { Position = new NetVector2Msg(x, y), LiquidType = liquidType });

	public IReadOnlyList<KeypadEntryMsg> CaptureKeypadCodes()
	{
		Calls.Add("capture-keypads");
		return [.. _keypads];
	}

	public IReadOnlyList<GeyserStateEntryMsg> CaptureGeysers()
	{
		Calls.Add("capture-geysers");
		return [.. _geysers];
	}

	public void ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes)
	{
		Calls.Add("apply-keypads");
		_keypads.Clear();
		_keypads.AddRange(codes);
	}

	public void ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers)
	{
		Calls.Add("apply-geysers");
		_geysers.Clear();
		_geysers.AddRange(geysers);
	}
}
