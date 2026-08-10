using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The geysers' liquid types as a creation-time initial condition (#128).
/// GeyserScript.Start rolls the type from the PUBLIC random stream
/// (GeyserScript.cs:12) — NOT the isolated generation stream (Start runs in
/// the wrapped coroutine's yield gaps, WorldGenRandomIsolation seals the
/// generator's OWN consumption only) — so every side's copy can roll a
/// different type. The host's roll is the authority, covered on every path:
/// - generation-time geysers: captured once the generation completed and
///   broadcast (world entry, like the keypad codes);
/// - runtime creations (the spawn command, a scripted create — #128 follow-up,
///   user 2026-08-10): detected on the host's copy (OnEntityInstantiated,
///   RemoteApply replay copies INCLUDED — the host's copy IS the authority),
///   read once Start ran, broadcast as a single-entry snapshot;
/// - the 60 s cycle re-sends the FULL current set (re-enumerated every time —
///   any creation after the last send is included; idempotent same-value
///   SetValue on the guest side) — it also covers the lazy P2P session's
///   ~30 s swallow window and a guest-side application guard drop.
/// With the type bound at creation time, the GeyserActivated event carries no
/// liquidType anymore: the spout's type is an initial condition, not an event
/// payload.
/// </summary>
internal sealed class GeyserStateSync(IWorldControl world, ISessionControl session, ILogger<GeyserStateSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly ILogger<GeyserStateSync> _log = log;

	/// <summary>Set once the current generation completed — the first host-frame broadcasts the full set.</summary>
	private bool _sentOnce;
	private float _lastResend;

	/// <summary>Runtime-created geysers awaiting their Start (value tuples — no Unity references held).</summary>
	private readonly List<(Vector2 Pos, int AtFrame)> _pending = [];

	internal void BindToSession() => _world.GeyserStateReceived += OnGeyserStateReceived;

	internal void Unbind() => _world.GeyserStateReceived -= OnGeyserStateReceived;

	/// <summary>Patch-bridge entry: a world entity started. A geyser starting
	/// OUTSIDE generation is a runtime creation (the spawn command) — its type
	/// rolls at its own Start, per side; the HOST's roll is the authority, so
	/// the host records the copy (its RemoteApply replay copy of a guest's
	/// spawn included — the marker must NOT exclude it: the host's copy is
	/// exactly the value this class reads). Read one frame later, when Start
	/// has run.</summary>
	internal void OnEntityInstantiated(BuildingEntity entity)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		if (entity.GetComponentInChildren<GeyserScript>() == null) // Unity object — ==
		{
			return;
		}

		_pending.Add((entity.transform.position, Time.frameCount));
	}

	internal void Update()
	{
		if (_session.Role == SessionRole.Guest)
		{
			return; // guests only apply
		}

		var world = WorldGeneration.world;
		if (world == null || HarmonyTraverse.IsGenerating()) // Unity object — ==; a new world/layer is generating — the old layer's geysers are gone
		{
			_sentOnce = false;
			_pending.Clear();
			return;
		}

		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			if (!_sentOnce)
			{
				_sentOnce = true;
				SendFullSet();
			}

			if (Time.unscaledTime - _lastResend > 60f)
			{
				_lastResend = Time.unscaledTime;
				SendFullSet(); // re-enumerated every time — creations after the last send are included
			}
		}

		FlushPending();
	}

	/// <summary>A recorded creation is read once Start has run (a frame after
	/// the instantiation): broadcast its type as a single-entry snapshot
	/// (position-keyed; the guest's application matches by position, idempotent).</summary>
	private void FlushPending()
	{
		if (_pending.Count == 0)
		{
			return;
		}

		foreach (var (pos, atFrame) in _pending)
		{
			if (Time.frameCount - atFrame < 2)
			{
				continue;
			}

			var geyser = TrapEffectApplier.FindTrap<GeyserScript>(pos);
			if (geyser == null) // Unity object — == (destroyed, or not yet created — the 60 s cycle covers it)
			{
				continue;
			}

			var p = geyser.transform.position;
			_world.SendGeyserStateSnapshot([new GeyserStateEntryMsg
			{
				Position = new NetVector2Msg(p.x, p.y),
				LiquidType = Traverse.Create(geyser).Field("liquidType").GetValue<byte>(), // byte — exact type (a GetValue<int> cast throws InvalidCastException)
			}]);
			_log.LogInformation("[GeyserSnapshot] runtime geyser at ({X:F1},{Y:F1}) — type broadcast.", p.x, p.y);
		}

		_pending.Clear();
	}

	private void SendFullSet()
	{
		var geysers = Enumerate();
		if (geysers.Count > 0)
		{
			_world.SendGeyserStateSnapshot(geysers);
		}
	}

	private static List<GeyserStateEntryMsg> Enumerate()
	{
		var geysers = new List<GeyserStateEntryMsg>();
		foreach (var geyser in Object.FindObjectsOfType<GeyserScript>())
		{
			var pos = geyser.transform.position;
			geysers.Add(new GeyserStateEntryMsg
			{
				Position = new NetVector2Msg(pos.x, pos.y),
				LiquidType = Traverse.Create(geyser).Field("liquidType").GetValue<byte>(), // byte — exact type (a GetValue<int> cast throws InvalidCastException)
			});
		}

		return geysers;
	}

	/// <summary>Guest side: the host's authoritative liquid types arrived — write
	/// each onto the local GeyserScript (position-keyed: deterministic world
	/// entities sit at the same place on both sides; the &lt;3 m radius tolerates
	/// the rumble jitter). Guarded: a snapshot landing while the local
	/// generation still runs cannot match entities yet — the 60 s cycle
	/// re-sends.</summary>
	internal void OnGeyserStateReceived(IReadOnlyList<GeyserStateEntryMsg> geysers)
	{
		if (geysers.Count == 0)
		{
			return;
		}

		var world = WorldGeneration.world;
		if (world == null || HarmonyTraverse.IsGenerating()) // Unity object — ==
		{
			_log.LogInformation("[GeyserSnapshot] arrived during generation — deferred to the next cycle.");
			return;
		}

		var applied = 0;
		foreach (var geyser in Object.FindObjectsOfType<GeyserScript>())
		{
			var pos = geyser.transform.position;
			var match = geysers.FirstOrDefault(g =>
				Vector2.Distance(new Vector2(g.Position.X, g.Position.Y), new Vector2(pos.x, pos.y)) < 3f);
			if (match is null)
			{
				continue;
			}

			Traverse.Create(geyser).Field("liquidType").SetValue(match.LiquidType); // byte — exact type (a SetValue(int) cast throws ArgumentException)
			applied++;
		}

		_log.LogInformation("[GeyserSnapshot] applied {Applied} host liquid type(s).", applied);
	}
}
