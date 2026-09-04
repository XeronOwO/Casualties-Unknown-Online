using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using UnityEngine;
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
	private readonly HashSet<string> _warnedRuntimeResolvers = [];
	private readonly Dictionary<string, Sprite[]> _animationFrames = [];

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
			var limb = GetLimb(body, presence.Scope, presence.LimbSlot);
			var runtimeMoodleId = ResolveRuntimeMoodleId(presence, limb);
			var useRuntimeMoodle = !string.IsNullOrWhiteSpace(runtimeMoodleId);
			string moodleId;
			ModStatusDefinition? statusDefinition = null;
			if (useRuntimeMoodle)
			{
				moodleId = runtimeMoodleId!;
			}
			else
			{
				if (!_statusContent.TryGetDefinition(presence.StatusId, out var found))
				{
					continue;
				}

				statusDefinition = found;
				moodleId = statusDefinition.ResolveMoodleId(limb?.name);
				if (string.IsNullOrWhiteSpace(moodleId))
				{
					continue;
				}
			}

			if (!_moodleContent.TryGetDefinition(moodleId, out var moodle))
			{
				WarnOnce(moodleId, "bound moodle definition is not registered");
				continue;
			}

			if (moodle.Important != importantRow)
			{
				continue;
			}

			var showPerLimb = presence.Scope == ModStatusScope.Limb
				&& (useRuntimeMoodle || statusDefinition?.ShowsPerLimbMoodles == true);
			if (!added.Add(BuildDedupeKey(moodleId, presence.Scope, presence.LimbSlot, showPerLimb, limb?.name)))
			{
				continue;
			}

			var iconKey = moodle.IconId;
			if (moodle.IconAnimation is { } iconAnimation)
			{
				if (!_animationFrames.TryGetValue(moodleId, out var frames))
				{
					frames = LoadAnimationFrames(moodleId, iconAnimation);
					if (frames.Length > 0)
					{
						_animationFrames[moodleId] = frames;
					}
				}

				if (frames.Length > 0)
				{
					iconKey = "cuo.moodle." + moodleId;
					manager.icons[iconKey] = frames[0];
					MoodleAnimationRegistry.Register(
						iconKey,
						frames,
						iconAnimation.FramesPerSecond,
						iconAnimation.Loop);
				}
				else
				{
					WarnOnce(moodleId, "moodle icon animation frames could not be resolved");
				}
			}

			if (!manager.icons.ContainsKey(iconKey))
			{
				WarnOnce(moodleId, $"icon '{iconKey}' is not available in the vanilla moodle icon set");
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
				iconKey,
				FormatLimbDisplayName(moodle, limb, showPerLimb),
				FormatLimbDescription(moodle, limb, showPerLimb),
				moodle.Critical,
				moodle.ChippedOnly);
		}
	}

	private static Limb? GetLimb(Body body, ModStatusScope scope, int limbSlot)
	{
		if (scope != ModStatusScope.Limb || body.limbs is null || limbSlot < 0 || limbSlot >= body.limbs.Length)
		{
			return null;
		}

		var limb = body.limbs[limbSlot];
		return limb == null ? null : limb; // Unity object — ==
	}

	private string? ResolveRuntimeMoodleId(ModStatusStore.StatusPresence presence, Limb? limb)
	{
		if (!_statusStore.TryGetMoodleResolver(presence.ModId, presence.StatusId, out var resolver)
			|| resolver is null)
		{
			return null;
		}

		byte[]? payload;
		if (presence.Scope == ModStatusScope.Body)
		{
			if (!_statusStore.TryGetBodyValue(presence.ModId, presence.StatusId, _session.LocalSteamId, out payload))
			{
				return null;
			}
		}
		else
		{
			if (!_statusStore.TryGetLimbValue(
				presence.ModId,
				presence.StatusId,
				_session.LocalSteamId,
				presence.LimbSlot,
				out payload))
			{
				return null;
			}
		}

		var request = new ModStatusMoodleRequest
		{
			ModId = presence.ModId,
			StatusId = presence.StatusId,
			PlayerSteamId = _session.LocalSteamId,
			Scope = presence.Scope,
			LimbSlot = presence.LimbSlot,
			LimbName = GetLimbName(limb),
			Payload = payload ?? []
		};

		try
		{
			var resolved = resolver(request);
			return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
		}
		catch (Exception e)
		{
			if (_warnedRuntimeResolvers.Add(presence.ModId + "|" + presence.StatusId))
			{
				_log.LogError(
					e,
					"[StatusMoodle] runtime moodle resolver for {ModId}/{StatusId} threw — falling back to static routing.",
					presence.ModId,
					presence.StatusId);
			}

			return null;
		}
	}

	private static string? GetLimbName(Limb? limb)
	{
		if (limb == null) // Unity object — ==
		{
			return null;
		}

		return !string.IsNullOrWhiteSpace(limb.shortName) ? limb.shortName : limb.name;
	}

	private static string BuildDedupeKey(
		string moodleId,
		ModStatusScope scope,
		int limbSlot,
		bool showPerLimb,
		string? limbName)
	{
		if (scope == ModStatusScope.Limb && showPerLimb)
		{
			return moodleId + "|limb|" + limbSlot + "|" + (limbName ?? "");
		}

		return moodleId;
	}

	private static string FormatLimbDisplayName(ModMoodleDefinition moodle, Limb? limb, bool showPerLimb)
	{
		if (!showPerLimb || limb == null) // Unity object — ==
		{
			return moodle.DisplayName;
		}

		var limbName = !string.IsNullOrWhiteSpace(limb.shortName) ? limb.shortName : limb.name;
		return moodle.FormatLimbDisplayName(limbName);
	}

	private static string FormatLimbDescription(ModMoodleDefinition moodle, Limb? limb, bool showPerLimb)
	{
		if (!showPerLimb || limb == null) // Unity object — ==
		{
			return moodle.Description;
		}

		var limbName = !string.IsNullOrWhiteSpace(limb.shortName) ? limb.shortName : limb.name;
		return moodle.FormatLimbDescription(limbName);
	}

	private Sprite[] LoadAnimationFrames(string moodleId, ModMoodleAnimation animation)
	{
		if (animation.FramePaths is not { Count: > 0 } framePaths)
		{
			return [];
		}

		var frames = new List<Sprite>(framePaths.Count);
		foreach (var path in framePaths)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				_log.LogWarning(
					"[StatusMoodle] skipping empty animation frame for moodle {MoodleId}.",
					moodleId);
				continue;
			}

			var sprite = Resources.Load<Sprite>(path);
			if (sprite == null) // Unity object — ==
			{
				_log.LogWarning(
					"[StatusMoodle] cannot resolve animation frame for moodle {MoodleId}: {Path}.",
					moodleId, path);
				continue;
			}

			frames.Add(sprite);
		}

		return [.. frames];
	}

	private void WarnOnce(string moodleId, string reason)
	{
		if (_warnedMoodles.Add(moodleId))
		{
			_log.LogWarning("[StatusMoodle] {MoodleId} skipped: {Reason}.", moodleId, reason);
		}
	}
}
