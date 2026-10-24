using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The end-of-layer panel's visibility on the HOST, decided by the group's state instead of the local body's
/// position (the user's 2026-10-24 ruling: the panel appears when the share of LIVING players that finished the
/// layer reaches <see cref="IHostRules.LayerCompleteLivingShare"/>, whether or not the host's own body is alive).
///
/// <para>
/// The native trigger asks one question — is the LOCAL body below <c>-halfHeight + 3.1</c> — and the ruling needs
/// two facts it cannot carry: whether the HOST's body is among the finished (a dead host has no body to press with)
/// and how much of the LIVING group is down there. So the host counts its own body (only while alive) and every
/// in-world member whose reported vitals say alive, and applies <see cref="LayerPanelVisibility"/>. Members never
/// reach this: their panel's choices are hidden (`LayerPanelChoicePolicy`), and this class is a no-op off the host.
/// </para>
///
/// <para>
/// In solo play (no session) the native check stays the authority and nothing here runs.
/// </para>
/// </summary>
internal sealed class LayerPanelGate(
	GameAdapterDomains domains,
	RemoteVitalsService vitals,
	ILogger<LayerPanelGate> log)
{
	/// <summary>The native floor probe: the panel's own condition (<c>WorldGeneration.cs</c>'s Update).</summary>
	private const float FloorMargin = 3.1f;

	private readonly GameAdapterDomains _domains = domains;
	private readonly RemoteVitalsService _vitals = vitals;
	private readonly ILogger<LayerPanelGate> _log = log;

	/// <summary>Per-frame: on the host in an active session, show or hide the panel by the group's share.</summary>
	internal void Update()
	{
		if (!_domains.Session.SessionActive || _domains.Session.Role != SessionRole.Host)
		{
			return;
		}

		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		var panel = world.savePanel;
		if (panel == null) // Unity object — ==
		{
			return;
		}

		var floor = -(float)(ulong)world.halfHeight + FloorMargin;
		var living = 0;
		var atFloor = 0;

		if (_domains.Run.LocalBody is { } me) // Unity object — ==
		{
			if (me.alive)
			{
				living++;
				if (me.transform.position.y < floor)
				{
					atFloor++;
				}
			}
		}

		var remotes = _domains.Entities.RemotePlayers;
		for (var i = 0; i < remotes.Count; i++)
		{
			var remote = remotes[i];
			if (remote.IsLocal || !_domains.Session.IsRemoteInWorld(remote.SteamId))
			{
				continue;
			}

			// A member whose vitals have not arrived yet is not counted: the report is the only fact the host has
			// about that body being alive, and guessing either way would move the threshold under the host's feet.
			if (!_vitals.TryGet(remote.SteamId, out var vitals) || !vitals.Alive)
			{
				continue;
			}

			living++;
			if (remote.Position.Y < floor)
			{
				atFloor++;
			}
		}

		var show = LayerPanelVisibility.ShouldShow(living, atFloor, _domains.HostRules.LayerCompleteLivingShare);
		if (panel.activeSelf == show)
		{
			return;
		}

		panel.SetActive(show);
		_log.LogInformation("[LayerChoice] the end-of-layer panel is {State}: {AtFloor}/{Living} living body/bodies at the floor, threshold {Threshold:0.##}.",
			show ? "shown" : "hidden", atFloor, living, _domains.HostRules.LayerCompleteLivingShare);
	}
}
