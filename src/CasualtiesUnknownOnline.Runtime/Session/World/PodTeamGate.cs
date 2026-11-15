using System;
using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The drill pod's own trigger (the native 5 s crouch under the pod, <c>DrillPod.cs:18-33</c>) belongs to the whole
/// living team: every living, connected member must stand inside the pod's own 1.5 x 5 area before the descent may
/// start. Dead and disconnected members are not counted; a member the host cannot place yet counts as missing, so a
/// body that has not loaded can never be mistaken for a body that is away.
/// <para>
/// The numbers are the native ones: <c>Mathf.Abs(body.x - pod.x) &lt; 1.5f</c> and
/// <c>Mathf.Abs(body.y - pod.y) &lt; 5f</c>. This class is pure so the dead, disconnected, unplaced and all-present
/// cases are ordinary tests rather than a live-session surprise; the adapter owns the translation from the session's
/// roster and vitals into <see cref="PodMember"/> rows.
/// </para>
/// </summary>
public static class PodTeamGate
{
	/// <summary>The native pod's horizontal reach (<c>DrillPod.cs:20</c>).</summary>
	public const float HalfWidth = 1.5f;

	/// <summary>The native pod's vertical reach (<c>DrillPod.cs:20</c>).</summary>
	public const float HalfHeight = 5f;

	/// <summary>Whether every living, connected member is inside the pod's own area.</summary>
	public static PodGateResult Evaluate(NetVector2 pod, IReadOnlyList<PodMember> members)
	{
		var living = 0;
		var atPod = 0;
		var missing = new List<string>();

		for (var i = 0; i < members.Count; i++)
		{
			var member = members[i];
			if (!member.InWorld || !member.Alive)
			{
				continue;
			}

			living++;
			if (member.Position is { } position
				&& Math.Abs(position.X - pod.X) < HalfWidth
				&& Math.Abs(position.Y - pod.Y) < HalfHeight)
			{
				atPod++;
				continue;
			}

			missing.Add(member.Name);
		}

		return new PodGateResult(missing.Count == 0 && living > 0, living, atPod, missing);
	}

	/// <summary>The label a refusal or host log uses: the display name when the roster has one, the id otherwise.</summary>
	public static string Label(ulong steamId, string? displayName) =>
		string.IsNullOrWhiteSpace(displayName)
			? steamId.ToString(CultureInfo.InvariantCulture)
			: displayName + " (" + steamId.ToString(CultureInfo.InvariantCulture) + ")";
}
