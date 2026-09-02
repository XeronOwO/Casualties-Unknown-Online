using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.ModStatus;

/// <summary>
/// The GameAdapter vanilla moodle-row projection for mod statuses
/// (mod-status domain phase 3, local UI seam). It reads the runtime mod status
/// presences for the local player, resolves the linked static
/// <see cref="ModMoodleDefinition"/> descriptors, and feeds them to the vanilla
/// <see cref="MoodleManager"/> through its public <c>AddMoodle</c> surface.
///
/// This is a local presentation seam: no wire message, no game/Unity type in
/// Abstractions, and no reflection-based moodle registry. Important (main-row)
/// moodles are added in the <c>AddAllMoodles</c> prefix, before the native
/// method switches to the side row; non-important (side-row) moodles are added
/// in the postfix after the native side moodles.
/// </summary>
internal sealed class ModStatusMoodleProjection(
	ModStatusStore statusStore,
	ISessionControl session,
	GameAdapterStatusContentProvider statusContent,
	GameAdapterMoodleContentProvider moodleContent,
	ILogger<ModStatusMoodleProjection> log)
{
	private readonly ModStatusStore _statusStore = statusStore;
	private readonly ISessionControl _session = session;
	private readonly GameAdapterStatusContentProvider _statusContent = statusContent;
	private readonly GameAdapterMoodleContentProvider _moodleContent = moodleContent;
	private readonly ILogger _log = log;
	private readonly HashSet<string> _warnedMoodles = [];

	internal void ApplyModMoodles(MoodleManager manager, bool importantRow)
	{
		if (manager == null) // Unity object — ==
		{
			return;
		}

		var body = HarmonyLib.Traverse.Create(manager).Field("body").GetValue<Body>();
		if (body == null || !body.alive) // Unity object — ==
		{
			return;
		}

		HashSet<string> added = [];
		foreach (var presence in _statusStore.GetStatusPresences(_session.LocalSteamId))
		{
			if (!_statusContent.TryGetDefinition(presence.StatusId, out var statusDefinition)
				|| string.IsNullOrWhiteSpace(statusDefinition.MoodleId))
			{
				continue;
			}

			var moodleId = statusDefinition.MoodleId;
			if (!_moodleContent.TryGetDefinition(moodleId, out var moodle))
			{
				WarnOnce(moodleId, "bound moodle definition is not registered");
				continue;
			}

			if (moodle.Important != importantRow)
			{
				continue;
			}

			if (!added.Add(moodleId))
			{
				continue;
			}

			if (!manager.icons.ContainsKey(moodle.IconId))
			{
				WarnOnce(moodleId, $"icon '{moodle.IconId}' is not available in the vanilla moodle icon set");
				continue;
			}

			if (manager.backgroundIcons == null
				|| moodle.Intensity < 0
				|| moodle.Intensity >= manager.backgroundIcons.Length)
			{
				WarnOnce(moodleId, $"intensity {moodle.Intensity} is outside the vanilla background icon range");
				continue;
			}

			manager.AddMoodle(
				moodle.Intensity,
				moodle.IconId,
				moodle.DisplayName,
				moodle.Description,
				moodle.Critical,
				moodle.ChippedOnly);
		}
	}

	private void WarnOnce(string moodleId, string reason)
	{
		if (_warnedMoodles.Add(moodleId))
		{
			_log.LogWarning("[StatusMoodle] {MoodleId} skipped: {Reason}.", moodleId, reason);
		}
	}
}
