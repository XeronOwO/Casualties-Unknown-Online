using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The Online UI bridge to the native backpack view: resolves a remote
/// SteamId to the live render clone and opens the game's radial inventory
/// focused on that clone. The actual UI patches read
/// <see cref="RemoteBackpackView"/>; this coordinator is the only layer that
/// knows how to find a remote Body from the session.
///
/// <para>
/// It also applies the host's <see cref="RemoteBackpackAccess"/> rule to the
/// PRESENTATION: deny refuses the open outright, and the middle value opens a
/// conscious owner's pack view only — the same "look, do not touch" state the game
/// itself gives an unconscious player looking at their own pack. The host enforces
/// the same rule on every operation; this side only keeps the player from raising a
/// gesture the host would refuse, and says why in one red line.
/// </para>
/// </summary>
internal sealed class RemoteBackpackCoordinator(
	ISessionControl session,
	RemotePlayerRenderer renderer,
	IPlayerInteractionVisibility visibility,
	IHostRules hostRules,
	AnnouncedHostRules announced,
	RemoteVitalsService vitals,
	RemoteBackpackNotice notice,
	ILocalizationService localization,
	ILogger<RemoteBackpackCoordinator> log)
{
	private readonly ISessionControl _session = session;
	private readonly RemotePlayerRenderer _renderer = renderer;
	private readonly IPlayerInteractionVisibility _visibility = visibility;
	private readonly IHostRules _hostRules = hostRules;
	private readonly AnnouncedHostRules _announced = announced;
	private readonly RemoteVitalsService _vitals = vitals;
	private readonly RemoteBackpackNotice _notice = notice;
	private readonly ILocalizationService _localization = localization;
	private readonly ILogger<RemoteBackpackCoordinator> _log = log;

	internal bool Open(ulong steamId, string displayName)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return false;
		}

		var access = AccessInForce();
		if (access is { } rule && !RemoteBackpackAccessPolicy.MayOpen(rule))
		{
			_log.LogInformation("[BackpackView] refused open for {SteamId}: host rule RemoteBackpackAccess=deny.", steamId);
			_notice.Show(_localization.T("remote_backpack.denied"));
			return false;
		}

		if (!_visibility.HasLineOfSight(_session.LocalSteamId, steamId))
		{
			_log.LogInformation("[BackpackView] refused open for {SteamId}: no line of sight.", steamId);
			return false;
		}

		if (!_renderer.TryGetRemoteBody(steamId, out var body) || body == null) // Unity object — ==
		{
			_log.LogWarning("[BackpackView] cannot open remote backpack for {SteamId}: no render clone yet.", steamId);
			return false;
		}

		// The remote backpack and remote medical views are distinct native
		// surfaces; only one can own the interaction focus at a time. If the
		// remote medical panel is currently open, close it before switching.
		if (RemoteMedicalView.IsOpen)
		{
			_log.LogInformation("[BackpackView] closing remote medical view before opening backpack for {SteamId}.", steamId);
			RemoteMedicalView.Close();
		}

		var readOnly = access is { } openRule && IsViewOnly(steamId, openRule);
		RemoteBackpackView.Open(body, steamId, displayName, readOnly);
		_log.LogInformation("[BackpackView] opened native backpack view for {SteamId} ({Name}) — {Mode}.", steamId, displayName, readOnly ? "view only" : "operable");
		if (readOnly)
		{
			_notice.Show(_localization.T("remote_backpack.view_only"));
		}

		return true;
	}

	internal void Update()
	{
		RemoteBackpackView.UpdatePendingContainerRefresh();
		ApplyAccessRule();
		RemoteBackpackView.ClearIfStale();
	}

	/// <summary>
	/// Keeps an OPEN view honest when the rule or the owner's state changes mid-session:
	/// deny closes it (the host announced the new rule for gestures in the world the
	/// member is already in), and the middle value switches the view between operable and
	/// view-only as the owner goes down or comes round. A no-op on every ordinary frame.
	/// </summary>
	private void ApplyAccessRule()
	{
		if (!RemoteBackpackView.IsOpen)
		{
			return;
		}

		var steamId = RemoteBackpackView.FocusedSteamId;
		var access = AccessInForce();
		if (access is { } rule && !RemoteBackpackAccessPolicy.MayOpen(rule))
		{
			_log.LogInformation("[BackpackView] closing {SteamId}'s backpack: the host rule became RemoteBackpackAccess=deny.", steamId);
			_notice.Show(_localization.T("remote_backpack.denied"));
			RemoteBackpackView.Close();
			return;
		}

		var readOnly = access is { } viewRule && IsViewOnly(steamId, viewRule);
		if (readOnly == RemoteBackpackView.IsReadOnly)
		{
			return;
		}

		RemoteBackpackView.SetReadOnly(readOnly);
		_log.LogInformation("[BackpackView] {SteamId}'s backpack is now {Mode}.", steamId, readOnly ? "view only" : "operable");
		if (readOnly)
		{
			_notice.Show(_localization.T("remote_backpack.view_only"));
		}
	}

	/// <summary>
	/// The middle value makes a conscious owner view-only. An owner whose vitals this
	/// client has not received yet counts as unknown, and unknown fails closed: the
	/// client can only refuse an operation, never be the reason one is allowed.
	/// </summary>
	private bool IsViewOnly(ulong steamId, RemoteBackpackAccess access)
	{
		var known = _vitals.TryGet(steamId, out var vitals);
		return RemoteBackpackAccessPolicy.IsViewOnly(access, known, known && vitals.Conscious, known && vitals.Alive);
	}

	/// <summary>
	/// The rule this client may refuse on: the host's own configuration on the host, and on a
	/// member ONLY the announced set. A member that has received no announcement answers null
	/// and every local check is skipped (decision 263): its own config is not the host's
	/// policy, so it may never be the reason a view is refused or declared view-only — the
	/// host's verdict decides, and it answers the actor when it refuses.
	/// </summary>
	private RemoteBackpackAccess? AccessInForce() =>
		_session.Role == SessionRole.Host ? _hostRules.RemoteBackpackAccess : _announced.Current?.RemoteBackpackAccess;
}
