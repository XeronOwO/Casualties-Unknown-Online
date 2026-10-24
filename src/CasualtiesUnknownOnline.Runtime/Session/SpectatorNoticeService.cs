using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The watched player is TOLD: this service turns the arrival of a <see cref="SpectatorNoticeMsg"/> into the
/// localized status line, through the same <see cref="ISessionNotices"/> slot the session-liveness watchdogs use.
///
/// <para>
/// It lives in the Runtime and subscribes to <see cref="IWorldControl"/> in its own <see cref="Initialize"/>, so the
/// world instance it listens to is the one the handler fires on by construction. The plugin only renders what the
/// slot carries; an earlier attempt subscribed from the plugin side and the line never appeared, which is why the
/// wiring lives here.
/// </para>
/// </summary>
internal sealed class SpectatorNoticeService(
	IWorldControl world,
	ISessionControl session,
	ISessionNotices notices,
	ILocalizationService localization,
	ILogger<SpectatorNoticeService> log) : ICuoService
{
	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly ISessionNotices _notices = notices;
	private readonly ILocalizationService _localization = localization;
	private readonly ILogger<SpectatorNoticeService> _log = log;
	private bool _attached;

	public void Initialize()
	{
		if (_attached)
		{
			return;
		}

		_world.SpectatorNoticeReceived += OnNotice;
		_attached = true;
	}

	public void Start()
	{
	}

	public void Update()
	{
	}

	public void Stop() => Detach();

	public void Dispose() => Detach();

	private void Detach()
	{
		if (!_attached)
		{
			return;
		}

		_world.SpectatorNoticeReceived -= OnNotice;
		_attached = false;
	}

	private void OnNotice(ulong sender, SpectatorNoticeMsg msg)
	{
		if (!msg.Watching || msg.TargetSteamId != _session.LocalSteamId)
		{
			return;
		}

		var watcher = _session.Members.FirstOrDefault(member => member.SteamId == msg.WatcherSteamId)?.DisplayName;
		if (string.IsNullOrWhiteSpace(watcher))
		{
			watcher = msg.WatcherSteamId.ToString(CultureInfo.InvariantCulture);
		}

		_notices.Publish(_localization.Format("spectator.watched_notice", watcher));
		_log.LogInformation(
			"[Spectator] the watched player was told (watcher={Watcher}, slot={Slot}, notifications={Type}).",
			msg.WatcherSteamId,
			RuntimeHelpers.GetHashCode(_notices),
			_notices.GetType().FullName);
	}
}
