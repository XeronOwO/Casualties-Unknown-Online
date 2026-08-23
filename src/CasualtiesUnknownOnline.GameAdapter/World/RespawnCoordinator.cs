using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Host-authoritative next-level respawn (the broaden revive/respawn rules
/// slice). When the host finishes generating the next world layer, every dead
/// player still in the session is respawned from its latest authoritative
/// character snapshot: in-world guests receive a full character-restore
/// message (the same two-frame wipe path as reconnect), menu-side guests are
/// invited back with a targeted WorldJoin, and the host body uses the same
/// local restore queue. The result is shaped by <see cref="RespawnOptions"/>
/// (keep inventory/skills, Permadeath gate).
/// </summary>
internal sealed class RespawnCoordinator(
	ISessionControl session,
	IWorldControl world,
	ICharacterDataControl characterData,
	CharacterDataSync characterDataSync,
	IOptionsMonitor<RespawnOptions> respawnOptions,
	ILogger<RespawnCoordinator> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly ICharacterDataControl _characterData = characterData;
	private readonly CharacterDataSync _characterDataSync = characterDataSync;
	private readonly IOptionsMonitor<RespawnOptions> _respawnOptions = respawnOptions;
	private readonly ILogger<RespawnCoordinator> _log = log;

	private bool _generating; // last frame's IsGenerating — the falling edge is the generation-finished moment
	private bool _revivePending; // one frame after the edge (the same pattern as GeneratedItemAuthority)

	internal void BindToSession() => _session.SessionEnded += OnSessionEnded;

	internal void Unbind()
	{
		_session.SessionEnded -= OnSessionEnded;
		_generating = false;
		_revivePending = false;
	}

	/// <summary>
	/// Pump: detect the generation-finished falling edge and run the respawn one
	/// frame later. The extra frame follows the existing generation-edge pattern
	/// (corpse loot / carried inventory / layer modifiers all wait one frame).
	/// </summary>
	internal void Update()
	{
		var generating = HarmonyTraverse.IsGenerating();
		if (generating)
		{
			_generating = true;
			return;
		}

		if (_generating)
		{
			_generating = false;
			_revivePending = true;
			return;
		}

		if (!_revivePending)
		{
			return;
		}

		_revivePending = false;
		ReviveDeadOnNextLevel();
	}

	private void ReviveDeadOnNextLevel()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var rules = _respawnOptions.CurrentValue;
		if (!RespawnPolicy.CanAutoReviveOnNextLevel(rules))
		{
			return;
		}

		var revived = 0;
		if (TryRevive(_session.LocalSteamId, rules, inWorld: _session.LocalInWorld, isLocal: true))
		{
			revived++;
		}

		foreach (var member in _session.Members.Where(m => m.Handshaken))
		{
			if (TryRevive(member.SteamId, rules, inWorld: member.InWorld, isLocal: false))
			{
				revived++;
			}
		}

		if (revived > 0)
		{
			_log.LogInformation("[Respawn] next-level respawn revived {Count} player(s) (keepInventory={KeepInventory}, keepSkills={KeepSkills}).",
				revived, rules.RespawnKeepInventory, rules.RespawnKeepSkills);
		}
	}

	private bool TryRevive(ulong steamId, RespawnOptions rules, bool inWorld, bool isLocal)
	{
		var source = isLocal
			? _characterData.GetHostCharacterData()
			: _characterData.GetSavedCharacter(steamId);
		if (!RespawnPolicy.IsDead(source))
		{
			return false;
		}

		var respawn = RespawnPolicy.PrepareRespawn(source!, rules.RespawnKeepInventory, rules.RespawnKeepSkills);
		if (isLocal)
		{
			_characterData.SaveHostCharacterData(respawn);
			_characterDataSync.QueueRespawnRestore(respawn);
		}
		else
		{
			_characterData.SaveCharacterData(steamId, respawn);
			// Full restore travels on the existing CharacterData direction: a
			// guest in the world applies the two-frame wipe immediately; a
			// menu-side guest queues it and applies when the targeted join below
			// creates its body.
			_characterData.SendSavedCharacter(steamId);
			if (!inWorld)
			{
				_world.SendWorldJoinTo(steamId);
			}
		}

		_log.LogInformation("[Respawn] revived {Peer} (inWorld={InWorld}, items={Items}, skillsReset={SkillsReset}).",
			steamId, inWorld, respawn.Items.Count, !rules.RespawnKeepSkills);
		return true;
	}

	private void OnSessionEnded()
	{
		_generating = false;
		_revivePending = false;
	}
}
