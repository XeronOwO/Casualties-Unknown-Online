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

		var show = TheShareIsMet(out var atFloor, out var living);
		if (panel.activeSelf == show)
		{
			return;
		}

		panel.SetActive(show);
		_log.LogInformation("[LayerChoice] the end-of-layer panel is {State}: {AtFloor}/{Living} living body/bodies at the floor, threshold {Threshold:0.##}.",
			show ? "shown" : "hidden", atFloor, living, _domains.HostRules.LayerCompleteLivingShare);
	}

	/// <summary>
	/// The close half, written IMMEDIATELY after the game's own panel write (<c>WorldGeneration.Update</c>'s
	/// `if (!this.savePanel.activeSelf &amp;&amp; … && localBody.y &lt; floor) SetActive(true)`), because that native
	/// latch re-opens any panel this client closed: it only tests <c>!activeSelf</c> and the LOCAL body's height, so a
	/// close from a later phase was undone on the next frame and the object flipped every frame (read on the machine,
	/// 2026-10-24: 1/2 living bodies at the floor, the gate logging "hidden", and the object still active when the probe
	/// read it). Closing in the native write's own frame is what makes the hide stick.
	/// </summary>
	internal void CloseIfTheShareIsNotMet()
	{
		if (!_domains.Session.SessionActive || _domains.Session.Role != SessionRole.Host)
		{
			return;
		}

		var world = WorldGeneration.world;
		if (world == null || world.savePanel == null) // Unity objects — ==
		{
			return;
		}

		if (!world.savePanel.activeSelf) // Unity object — ==
		{
			return;
		}

		if (!TheShareIsMet(out _, out _))
		{
			world.savePanel.SetActive(false);
			_log.LogInformation("[LayerChoice] the end-of-layer panel is closed in the game's own write frame: the living share has not reached the host rule.");
		}
	}

	/// <summary>Whether the living group's share at the floor has reached the host rule, with the counts for a log line.</summary>
	private bool TheShareIsMet(out int atFloor, out int living)
	{
		atFloor = 0;
		living = 0;

		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return false;
		}

		var floor = -(float)(ulong)world.halfHeight + FloorMargin;

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

		return LayerPanelVisibility.ShouldShow(living, atFloor, _domains.HostRules.LayerCompleteLivingShare);
	}
}
