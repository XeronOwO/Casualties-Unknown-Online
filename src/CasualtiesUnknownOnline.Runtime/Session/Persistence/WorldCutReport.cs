using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The account of one cut attempt: what the armed request resolved to, which
/// world it wrote, and — the part §6's "no silent loss" rule needs — which
/// in-flight classes were NOT carried. The player-facing surface (the command
/// console) and the log both render <see cref="Describe"/>.
/// </summary>
/// <param name="Result">What the attempt resolved to.</param>
/// <param name="Reason">The trigger the request carried.</param>
/// <param name="WorldId">The world the cut wrote into ("" when there was none).</param>
/// <param name="Summary">What happened, in the words the console shows.</param>
/// <param name="DroppedStates">In-flight classes the cut did not carry, one policy description each.</param>
public sealed record WorldCutReport(
	WorldCutResult Result,
	WorldCutReason Reason,
	string WorldId,
	string Summary,
	IReadOnlyList<string> DroppedStates)
{
	/// <summary>A cut that wrote a snapshot.</summary>
	public bool Captured => Result == WorldCutResult.Captured;

	/// <summary>
	/// True = the player asked for this cut (a console command, or the host's
	/// deliberate return to the menu), so the command console answers it. A layer
	/// advance or an interval autosave is a system trigger: it goes to the log and
	/// does not push a line into the player's console.
	/// </summary>
	public bool PlayerInitiated => Reason is WorldCutReason.Command or WorldCutReason.MenuReturn;

	/// <summary>The one-line account of this attempt, for the log and the command console.</summary>
	public string Describe()
	{
		var dropped = DroppedStates.Count == 0 ? string.Empty : $"; NOT carried: {string.Join(", ", DroppedStates)}";
		var reason = SaveArchiveFormat.CutReasonName(Reason);
		return Result switch
		{
			WorldCutResult.Deferred => $"the {reason} cut is waiting for in-flight state to resolve{dropped}",
			WorldCutResult.Captured => $"cut written ({Summary}){dropped}",
			WorldCutResult.Refused => $"cut refused ({Summary}){dropped}",
			_ => throw new ArgumentOutOfRangeException(nameof(Result), Result, "Unknown cut result."),
		};
	}
}
