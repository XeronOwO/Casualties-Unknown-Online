using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The Online UI action forwards. Keeping the player-interaction/admin button
/// paths out of <see cref="Plugin"/> keeps the BepInEx lifecycle facade focused
/// on loading, hotkeys and Unity callbacks, and gives the action surface a
/// single small owner that only depends on the narrow control interfaces.
/// </summary>
internal sealed class OnlineUiActions(
	SessionService session,
	IHostBanService hostBan,
	IPlayerInteractionControl playerInteraction,
	IGameAdapter? adapter)
{
	private readonly SessionService _session = session;
	private readonly IHostBanService _hostBan = hostBan;
	private readonly IPlayerInteractionControl _playerInteraction = playerInteraction;
	private readonly IGameAdapter? _adapter = adapter;

	public bool TakeItemFromRemote(ulong ownerSteamId, ulong itemInstanceId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		_playerInteraction.SendTakeRequest(ownerSteamId, itemInstanceId);
		return true;
	}

	public bool CarryRemoteFromUi(ulong targetSteamId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		_playerInteraction.SendCarryStartRequest(targetSteamId);
		return true;
	}

	public bool PiggybackRemoteFromUi(ulong targetSteamId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		_playerInteraction.SendPiggybackRequest(targetSteamId);
		return true;
	}

	public bool DropCarryFromUi(ulong carriedSteamId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		_playerInteraction.SendCarryStopRequest(carriedSteamId);
		return true;
	}

	public bool HealRemoteFromUi(ulong targetSteamId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		_playerInteraction.SendHealRequest(targetSteamId, 0);
		return true;
	}

	public bool HealWithItemFromUi(ulong targetSteamId, ulong itemInstanceId)
	{
		if (!_session.SessionActive || itemInstanceId == 0)
		{
			return false;
		}

		_playerInteraction.SendHealRequest(targetSteamId, itemInstanceId);
		return true;
	}

	public bool UseItemOnRemoteFromUi(ulong targetSteamId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		_playerInteraction.SendUseRequest(targetSteamId, 0);
		return true;
	}

	public bool UseItemWithOnRemoteFromUi(ulong targetSteamId, ulong itemInstanceId)
	{
		if (!_session.SessionActive || itemInstanceId == 0)
		{
			return false;
		}

		_playerInteraction.SendUseRequest(targetSteamId, itemInstanceId);
		return true;
	}

	public bool PushRemoteFromUi(ulong targetSteamId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		_playerInteraction.SendPushRequest(targetSteamId);
		return true;
	}

	public bool RecruitPlayerFromUi(ulong targetSteamId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		return _adapter?.TryRequestTraderRecruit(targetSteamId) == true;
	}

	public bool KickMemberFromUi(ulong targetSteamId) => _session.KickMember(targetSteamId, "kicked by host");

	public bool BanMemberFromUi(ulong targetSteamId) => _hostBan.Ban(targetSteamId, "banned by host");

	public bool UnbanMemberFromUi(ulong targetSteamId) => _hostBan.Unban(targetSteamId);

	public bool HasLocalHealItem() => _adapter?.HasLocalHealItem() == true;

	public IReadOnlyList<LocalHealItem> GetLocalHealItems() => _adapter?.GetLocalHealItems() ?? [];

	public bool HasLocalUseItem() => _adapter?.HasLocalUseItem() == true;

	public IReadOnlyList<LocalUseItem> GetLocalUseItems() => _adapter?.GetLocalUseItems() ?? [];
}
