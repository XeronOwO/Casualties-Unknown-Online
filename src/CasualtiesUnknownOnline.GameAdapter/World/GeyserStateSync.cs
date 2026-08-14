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
/// different type. The host's roll is the authority for GENERATION-time
/// geysers: captured once the generation completed and broadcast on world
/// entry (like the keypad codes); the 60 s cycle re-sends the FULL current
/// set (re-enumerated every time — idempotent same-value SetValue on the
/// guest side; covers the lazy P2P session's ~30 s swallow window and any
/// missed application). RUNTIME-created geysers (the spawn command) carry
/// their type IN the creation message instead (EntitySpawnedMsg.LiquidType,
/// EntitySpawnSync — one message per operation); the periodic re-send here
/// is the fallback for a lost report. The GeyserActivated event carries no
/// liquidType: the spout's type is an initial condition, not an event
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

	internal void BindToSession()
	{
		_world.GeyserStateReceived += OnGeyserStateReceived;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
	}

	internal void Unbind()
	{
		_world.GeyserStateReceived -= OnGeyserStateReceived;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
	}

	/// <summary>A member (re)entered the world — re-broadcast the geyser liquid
	/// types so a reconnect gets them immediately instead of waiting up to 60 s
	/// for the periodic cycle (idempotent — same-value SetValue on the guest).</summary>
	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (inWorld && _session.Role == SessionRole.Host && _session.SessionActive && WorldGeneration.world != null) // Unity object — ==
		{
			SendFullSet();
		}
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
