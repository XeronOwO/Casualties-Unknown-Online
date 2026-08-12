using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The outcome of an evidence comparison (<see cref="ItemArbitration.CheckEvidence"/>)
/// — the decision object that replaces in-method side effects: what the host
/// must DO about the divergence (destroy claimed-but-unknown contents, send
/// the authoritative entry as a correction). The comparison is pure; the side
/// effects run in ItemArbitration.ApplyVerdict. The two divergence classes are
/// independent and mutually exclusive (a missing content short-circuits the
/// extra scan, ContentsMatch): extra ids destroy, anything else corrects.
/// </summary>
public sealed class EvidenceVerdict
{
	private EvidenceVerdict(bool matches, List<ulong>? extraContentIds)
	{
		Matches = matches;
		ExtraContentIds = extraContentIds ?? [];
	}

	/// <summary>Evidence absent (a legacy report has nothing to check) or fully matching.</summary>
	public static EvidenceVerdict Match { get; } = new(true, null);

	/// <summary>Top-level state and claimed contents are consistent (extra ids may still destroy).</summary>
	public bool Matches { get; }

	/// <summary>Content ids the guest claimed that the authority lacks — each is destroyed with a
	/// one-shot ItemDestroy (never corrected back: they are not ours).</summary>
	public IReadOnlyList<ulong> ExtraContentIds { get; }

	/// <summary>Top-level state diverged or the guest is missing contents — the whole authoritative
	/// entry goes as one ItemCorrection (the guest's apply materializes and fixes).</summary>
	public bool NeedsCorrection => !Matches;

	internal static EvidenceVerdict From(bool matches, List<ulong>? extraContentIds) => new(matches, extraContentIds);
}
