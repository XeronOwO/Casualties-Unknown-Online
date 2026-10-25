using System;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The cross-player item-use operation's session surface: the request entry points
/// (the guest→host wire and the host's own client), the affected side's outcome
/// report, and the session-scoped reset. The use itself — the entry-aware family,
/// its branch and the authoritative result — is
/// <see cref="PlayerItemUseExecution"/>'s, so the endpoint and the execution read
/// apart; where a use's item state LANDS is <see cref="PlayerItemUseCommit"/>'s.
/// <para>
/// It has no mutable session state beyond the composition it owns — it only reacts
/// to calls and messages.
/// </para>
/// </summary>
internal sealed class PlayerItemUseService : ISessionReset, IDisposable
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly PlayerCharacterAccess _characters;
	private readonly IPlayerInteractionVisibility _visibility;
	private readonly PlayerItemActionOutcomeService _outcomes;
	private readonly PlayerItemUseExecution _execution;
	private readonly ISessionNotices _notices;
	private readonly ILocalizationService _localization;
	private readonly ILogger _log;

	public PlayerItemUseService(
		ISessionControl session,
		PacketSender sender,
		PlayerCharacterAccess characters,
		IItemControl items,
		IPlayerInteractionVisibility visibility,
		ILimbUseSemantics limbUseSemantics,
		IConsumeSemantics consumeSemantics,
		IWearSemantics wearSemantics,
		ISolidFoodSemantics solidFoodSemantics,
		ItemKernelAuthority kernelAuthority,
		PlayerInteractionResultAuthority resultAuthority,
		ISessionNotices notices,
		ILocalizationService localization,
		ILogger log)
	{
		_notices = notices;
		_localization = localization;
		_session = session;
		_sender = sender;
		_characters = characters;
		_visibility = visibility;
		_log = log;

		// One commit side and one affected-side outcome half, shared with the
		// execution: the state a use leaves on the item lives in exactly one place
		// (statement over the same objects twice would be two answers to "who
		// committed this"), and both families whose effect runs on the affected side
		// report through the same admission table.
		var commit = new PlayerItemUseCommit(session, characters, items, kernelAuthority, log);
		_outcomes = new PlayerItemActionOutcomeService(characters, limbUseSemantics, solidFoodSemantics, commit, log);
		_execution = new PlayerItemUseExecution(
			session, characters, items, resultAuthority, commit, _outcomes,
			limbUseSemantics, consumeSemantics, wearSemantics, solidFoodSemantics, localization, log);
	}

	/// <summary>Composition wiring: the affected-side admission table is session-scoped, so the session's end drops it.</summary>
	internal void BindToSession() => _session.SessionEnded += ResetSessionState;

	public void Dispose() => _session.SessionEnded -= ResetSessionState;

	public void ResetSessionState() => _outcomes.ResetSessionState();

	/// <summary>An authoritative cross-player consumable use result arrived — the Game Adapter applies the local participant half.</summary>
	public event Action<PlayerItemUseResultMsg>? UseReceived;

	/// <summary>Online UI entry: the local player uses one carried non-injectable consumable on another player (0 = host auto-select).</summary>
	public void SendUseRequest(ulong targetSteamId, ulong itemInstanceId = 0, int targetLimbIndex = -1, float doseMl = 0f)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (!_visibility.HasLineOfSight(_session.LocalSteamId, targetSteamId))
		{
			_log.LogInformation("[ItemUse] refused locally: {User} cannot see {Target} on this client.", _session.LocalSteamId, targetSteamId);
			return;
		}

		var msg = new PlayerItemUseRequestMsg
		{
			TargetSteamId = targetSteamId,
			ItemInstanceId = itemInstanceId,
			LimbIndex = targetLimbIndex,
			DoseMl = doseMl,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleUseRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerItemUseRequest, msg);
		}
	}

	/// <summary>Host only: a use request arrived — the guest→host wire and the host's own UI share this path.</summary>
	public void HandleUseRequest(ulong sender, PlayerItemUseRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var user = sender;
		var target = msg.TargetSteamId;
		if (user == target || user == 0 || target == 0)
		{
			return;
		}

		var refusal = _execution.TryExecute(user, target, msg.ItemInstanceId, msg.LimbIndex, msg.DoseMl, EntryOf(msg.LimbIndex), requireUserConscious: true);
		if (refusal is { } reason)
		{
			NotifyRefused(user, target, msg.ItemInstanceId, reason);
		}
	}

	/// <summary>
	/// Puts a refused use where the operator can see it (the user's 2026-10-24 ruling): a member's refusal travels
	/// back over the wire as a KEY and its one argument — never as the host's sentence — and the reader's own catalogue
	/// renders it in the reader's language. The host's own refused gesture takes the local path through the same line.
	/// </summary>
	private void NotifyRefused(ulong user, ulong target, ulong itemInstanceId, RefusalReason reason)
	{
		if (user != _session.LocalSteamId)
		{
			_sender.Send(user, NetMsg.PlayerItemUseRefused, new PlayerItemUseRefusedMsg
			{
				TargetSteamId = target,
				ItemInstanceId = itemInstanceId,
				ReasonKey = reason.Key,
				ReasonArg = reason.Argument ?? string.Empty,
			});
			return;
		}

		NotifyUseRefused(reason);
	}

	public void FireUseRefused() => UseRefused?.Invoke();

	/// <summary>
	/// A refusal the operator's OWN client decided: the same line and the same deny sound the host's refusals get, so a
	/// gesture refused before it left the client never reads as one that silently did nothing. Both sides render the
	/// same key, so a refusal reads in the language of whoever is looking at it.
	/// </summary>
	public void NotifyUseRefused(RefusalReason reason)
	{
		_notices.Publish(Render(reason));
		UseRefused?.Invoke();
	}

	/// <summary>The one place a refusal key becomes text: the reading client's catalogue, whole sentence, its own language.</summary>
	private string Render(RefusalReason reason) =>
		string.IsNullOrEmpty(reason.Argument)
			? _localization.Format(reason.Key)
			: _localization.Format(reason.Key, reason.Argument);

	/// <summary>The operator's gesture was refused; the Game Adapter rings the game's deny sound.</summary>
	public event Action? UseRefused;

	/// <summary>
	/// The entry a request's limb selection identifies. A request that stops at the
	/// host is one of exactly two gestures: the wound view's release always names the
	/// limb it landed on — that limb IS the gesture's native call site (decision 246)
	/// — while the inventory use (the world drag, the Online UI's one-shot use) cannot
	/// name one. The held-remote-item path is the third way into this operation and
	/// passes its own entry explicitly, because its own native convention lets an
	/// injection request omit the limb (-1 = the game's own automatic pick).
	/// </summary>
	private static ItemUseEntry EntryOf(int limbIndex) =>
		limbIndex >= 0 ? ItemUseEntry.Limb : ItemUseEntry.Inventory;

	/// <summary>
	/// Host-authoritative use of a remote player's carried item on the
	/// requesting player's own body. This is the "held-remote-item + Tab close +
	/// R medical use" family: the item owner retains ownership (and their local
	/// item state is updated/consumed by the authoritative use result), while the
	/// requester receives the body-side effect. The item owner does not have to
	/// be conscious — the requester is the active user; the requester must be
	/// conscious/alive and able to see the owner.
	/// </summary>
	internal void HandleRemoteHeldItemUse(
		ulong requester,
		ulong itemOwner,
		ulong itemInstanceId,
		int targetLimbIndex,
		float doseMl)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (requester == 0 || itemOwner == 0 || itemOwner == requester)
		{
			_log.LogWarning("[HeldItemUse] refused invalid requester/owner pair ({Requester} -> {Owner}).", requester, itemOwner);
			return;
		}

		if (!_characters.IsInWorld(requester) || !_characters.IsInWorld(itemOwner))
		{
			_log.LogWarning("[HeldItemUse] refused: requester {Requester} or owner {Owner} is not in-world.", requester, itemOwner);
			return;
		}

		// The requester is the person physically using the item on their own
		// body, so they must be conscious/alive. The owner is only the item
		// source and may be unconscious/dead (the common remote-backpack case).
		if (_characters.GetCharacterData(requester)?.Health is not { } requesterHealth
			|| !requesterHealth.Conscious
			|| !requesterHealth.Alive)
		{
			_log.LogInformation("[HeldItemUse] refused: requester {Requester} is not conscious/alive.", requester);
			return;
		}

		var refusal = _execution.TryExecute(itemOwner, requester, itemInstanceId, targetLimbIndex, doseMl, ItemUseEntry.Limb, requireUserConscious: false);
		if (refusal is { } reason)
		{
			NotifyRefused(requester, itemOwner, itemInstanceId, reason);
		}
	}

	/// <summary>Kernel projection path: a use result event arrived — surface it for the Game Adapter.</summary>
	public void FireUseReceived(PlayerItemUseResultMsg msg) => UseReceived?.Invoke(msg);

	/// <summary>
	/// Any role: the local client ran a cross-player use whose effect belongs to this
	/// side — report what the used item became so the host can hand it to the item's
	/// owner. Guest → host on the wire; the host handles its own case locally,
	/// because the host is the affected side there just as a guest is (its admission
	/// came from its own request path). <paramref name="consumed"/> is the limb-tool
	/// family's observation that the run destroyed the item object; the eat reports
	/// false because its report is issued from inside the use call.
	/// <paramref name="becameLimbComponent"/> narrows that further: the object did not
	/// merely end, it became the limb's installed component, so the host relocates the
	/// row onto the limb the admission named instead of ending it.
	/// </summary>
	public void SendItemActionOutcome(ulong itemInstanceId, float condition, bool consumed, bool becameLimbComponent = false, string? replacementItemId = null, float replacementCondition = 0f)
	{
		if (!_session.SessionActive || itemInstanceId == 0)
		{
			return;
		}

		var msg = new PlayerItemActionOutcomeMsg
		{
			ItemInstanceId = itemInstanceId,
			Condition = condition,
			Consumed = consumed,
			BecameLimbComponent = becameLimbComponent,
			ReplacementItemId = replacementItemId ?? string.Empty,
			ReplacementCondition = replacementCondition,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleItemActionOutcome(_session.LocalSteamId, msg);
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.PlayerItemActionOutcome, msg);
	}

	/// <summary>
	/// Host only: the affected side ran a cross-player use — its outcome is committed
	/// onto the item's OWNER and published as the ordinary use result, so the owner's
	/// own item and every peer's clone learn it through the same path every other
	/// family uses. The refused cases and what the item became are
	/// <see cref="PlayerItemActionOutcomeService.HandleOutcome"/>'s, because the
	/// admitted use is what authorizes the write at all.
	/// </summary>
	public void HandleItemActionOutcome(ulong sender, PlayerItemActionOutcomeMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (_outcomes.HandleOutcome(sender, msg) is { } result)
		{
			_execution.PublishUse(result);
		}
	}
}
