using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The radiation-line world-state domain (host authority). The game's
/// <c>RadiationLine</c> advances <c>timeGone</c> and applies local-body
/// radiation/eye effects on every side, but the line itself is world state:
/// its <c>active</c> flag and <c>timeGone</c> descent must be the same on all
/// sides for late joiners and for guests whose layer timer or body
/// consciousness differs from the host's. The host broadcasts the absolute
/// state while the line is active (short interval so the local per-frame
/// presentation stays smooth yet re-aligns); an inactive line is broadcast on
/// the transition and on world entry/reconnect via the stored snapshot.
/// Guests keep running their local <c>RadiationLine.Update</c> between
/// resends (that path also drives the local body's radiation effects), but
/// their own independent activation is suppressed in
/// <see cref="Patches.WorldGenerationUpdatePatch"/>.
/// </summary>
internal sealed class RadiationLineSync(
	IWorldControl world,
	ISessionControl session,
	IEntitySyncControl entities,
	ILogger<RadiationLineSync> log)
{
	/// <summary>Host resend interval for an active line. The line moves at
	/// most ~1.5 units/s, so 5 Hz keeps the guest's local-continued state
	/// within a small fraction of a unit while the wire cost stays tiny.</summary>
	private const float BroadcastIntervalSeconds = 0.2f;

	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly IEntitySyncControl _entities = entities;
	private readonly ILogger<RadiationLineSync> _log = log;
	private readonly List<RadiationPlayerProgress> _playerProgress = [];

	/// <summary>Whether the host has published the current generation's state
	/// (a new generation resets this — the first frame after generation
	/// re-publishes the now-inactive line).</summary>
	private bool _hasPublished;

	private bool _lastActive;
	private float _lastBroadcastTime;

	internal void BindToSession() => _world.RadiationLineStateReceived += OnRadiationLineStateReceived;

	internal void Unbind() => _world.RadiationLineStateReceived -= OnRadiationLineStateReceived;

	internal void Update()
	{
		if (_session.Role == SessionRole.Guest)
		{
			return;
		}

		var line = RadiationLine.line;
		if (line == null) // Unity object — ==
		{
			_hasPublished = false;
			return;
		}

		if (WorldGeneration.world == null || HarmonyTraverse.IsGenerating()) // Unity object — ==
		{
			_hasPublished = false;
			return;
		}

		TryActivateForStragglers();

		var active = line.active;
		var timeGone = ReadTimeGone(line);

		if (!_session.SessionActive || _session.Role != SessionRole.Host)
		{
			// Solo/menu: keep the local line's current state as the world-entry
			// snapshot source so a later solo→lobby conversion can hand it to a
			// joining/reconnecting guest without depending on the first live
			// host broadcast frame. No wire send.
			_world.SetRadiationLineState(new RadiationLineStateMsg { Active = active, TimeGone = timeGone });
			_hasPublished = false;
			return;
		}

		var due = !_hasPublished
			|| active != _lastActive
			|| (active && Time.unscaledTime - _lastBroadcastTime >= BroadcastIntervalSeconds);
		if (!due)
		{
			return;
		}

		Publish(active, timeGone);
	}

	/// <summary>
	/// Host-side co-op straggler pressure: if the vanilla layer timer has not
	/// started the line yet, start it when at least one living player has
	/// reached the layer bottom and another living player is still above it.
	/// The line remains active once started (vanilla one-way semantics); the
	/// body radiation/eye effects stay local per side (local-compute mandate)
	/// and are already driven by each side's <c>RadiationLine.Update</c>.
	/// </summary>
	private void TryActivateForStragglers()
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Host)
		{
			return;
		}

		var line = RadiationLine.line;
		if (line == null || line.active) // Unity object — ==
		{
			return;
		}

		var world = WorldGeneration.world;
		if (world == null || HarmonyTraverse.IsGenerating()) // Unity object — ==
		{
			return;
		}

		var bottomY = -(float)world.halfHeight + 3.1f;
		_playerProgress.Clear();
		AddProgress(_entities.LocalPlayer);
		foreach (var remote in _entities.RemotePlayers)
		{
			AddProgress(remote);
		}

		if (!RadiationStragglerPolicy.ShouldActivateLine(_playerProgress, bottomY))
		{
			return;
		}

		line.Activate();
		_log.LogInformation(
			"[RadiationLine] host activated the line for straggler pressure (players={Players}, bottomY={BottomY:F1}).",
			_playerProgress.Count, bottomY);
	}

	private void AddProgress(PlayerEntity player)
	{
		if (player is null)
		{
			return;
		}

		_playerProgress.Add(new RadiationPlayerProgress(player.Position.Y, player.Alive));
	}

	private void Publish(bool active, float timeGone)
	{
		_world.BroadcastRadiationLineState(new RadiationLineStateMsg
		{
			Active = active,
			TimeGone = timeGone,
		});
		_hasPublished = true;
		_lastActive = active;
		_lastBroadcastTime = Time.unscaledTime;
		_log.LogDebug("[RadiationLine] host published active={Active}, timeGone={TimeGone:F2}.", active, timeGone);
	}

	private void OnRadiationLineStateReceived(RadiationLineStateMsg msg)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		var line = RadiationLine.line;
		if (line == null) // Unity object — ==
		{
			return;
		}

		if (msg.Active)
		{
			line.active = true;
			WriteTimeGone(line, msg.TimeGone);
			_log.LogDebug("[RadiationLine] guest applied host state active=true, timeGone={TimeGone:F2}.", msg.TimeGone);
		}
		else
		{
			if (WorldGeneration.world != null) // Unity object — ==
			{
				line.Deactivate();
			}
			else
			{
				line.active = false;
				WriteTimeGone(line, 0f);
			}

			_log.LogDebug("[RadiationLine] guest applied host state active=false.");
		}
	}

	/// <summary>The line's descent is a private field (RadiationLine.cs) — read
	/// through Traverse with the exact float type.</summary>
	private static float ReadTimeGone(RadiationLine line) =>
		Traverse.Create(line).Field("timeGone").GetValue<float>();

	private static void WriteTimeGone(RadiationLine line, float value) =>
		Traverse.Create(line).Field("timeGone").SetValue(value);
}
