using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// In-memory stand-in for the adapter's native world-fact tables (keypad codes,
/// geyser liquid types and the game's own partial-damage list) — the optional
/// half of the world-fact seam. It records its calls so a save suite can prove
/// which half of the restore the Runtime owns and which half it hands over, and
/// it carries the same pending lifecycle as the real handover: an apply arms a
/// pending set that exactly one take (or one cancel) ends.
/// </summary>
internal sealed class FakeNativeWorldFacts : INativeWorldFacts
{
	private readonly List<KeypadEntryMsg> _keypads = [];
	private readonly List<GeyserStateEntryMsg> _geysers = [];
	private readonly List<BlockDamageEntryMsg> _damages = [];
	private bool _pending;

	internal List<string> Calls { get; } = [];

	internal IReadOnlyList<KeypadEntryMsg> Keypads => _keypads;

	internal IReadOnlyList<GeyserStateEntryMsg> Geysers => _geysers;

	/// <summary>The game's own partial-damage table as this fake holds it.</summary>
	internal IReadOnlyList<BlockDamageEntryMsg> Damages => _damages;

	internal void SeedKeypad(float x, float y, string code) =>
		_keypads.Add(new KeypadEntryMsg { Position = new NetVector2Msg(x, y), Code = code });

	internal void SeedGeyser(float x, float y, byte liquidType) =>
		_geysers.Add(new GeyserStateEntryMsg { Position = new NetVector2Msg(x, y), LiquidType = liquidType });

	internal void SeedBlockDamage(int x, int y, float damage) =>
		_damages.Add(new BlockDamageEntryMsg { X = x, Y = y, Damage = damage });

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

	public IReadOnlyList<BlockDamageEntryMsg> CaptureBlockDamages()
	{
		Calls.Add("capture-block-damages");
		return [.. _damages];
	}

	public void ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes)
	{
		Calls.Add("apply-keypads");
		// Copy first: the production handover stores a snapshot of the rows, and a
		// caller may legitimately hand over this fake's own current table.
		var snapshot = new List<KeypadEntryMsg>(codes);
		_keypads.Clear();
		_keypads.AddRange(snapshot);
		_pending = true;
	}

	public void ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers)
	{
		Calls.Add("apply-geysers");
		var snapshot = new List<GeyserStateEntryMsg>(geysers);
		_geysers.Clear();
		_geysers.AddRange(snapshot);
		_pending = true;
	}

	public void ApplyBlockDamages(IReadOnlyList<BlockDamageEntryMsg> damages)
	{
		Calls.Add("apply-block-damages");
		var snapshot = new List<BlockDamageEntryMsg>(damages);
		_damages.Clear();
		_damages.AddRange(snapshot);
		_pending = true;
	}

	public bool HasPendingRestore => _pending;

	public NativeWorldFactRestore ReadPendingRestore()
	{
		Calls.Add("read-pending");
		return _pending
			? new NativeWorldFactRestore([.. _keypads], [.. _geysers], [.. _damages])
			: NativeWorldFactRestore.Empty;
	}

	public void CommitPendingRestore()
	{
		if (!_pending)
		{
			// Mirrors the production handover: a commit with nothing pending is a
			// no-op that records nothing (only a fully-applied replay commits).
			return;
		}

		Calls.Add("commit-pending");
		_pending = false;
	}

	public void CancelPendingRestore()
	{
		if (!_pending)
		{
			// Mirrors the production handover: a cancel with nothing pending is a
			// no-op that records nothing (every run start calls it).
			return;
		}

		Calls.Add("cancel-pending");
		_pending = false;
	}
}
