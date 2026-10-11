using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Host/solo: the ids a restored archive re-issued — the ONE input every id-space
/// counter resumes from (<see cref="ItemIdCoordinator.ResumeRestoredIds"/>).
///
/// The save layer is the only holder of the complete set, which is why the rule is
/// handed the whole of it at one call instead of reading one carrier and hoping the
/// other is a subset. An archive writes instance ids in TWO places:
/// <list type="bullet">
/// <item>the kernel checkpoint's item rows — world, carried, contained and worn items,
/// i.e. everything the KERNEL knows (this is what the cut's <c>items.json</c> holds),</item>
/// <item><c>characters/&lt;playerKey&gt;.json</c> — the carried items of every character
/// snapshot, which the live 1 Hz report writes <c>CharacterItemMsg.InstanceId</c> into
/// WITHOUT the kernel learning anything: the kernel learns a guest's carried ids through
/// the separate carried-registration cadence, so inside that window an id exists in the
/// character file alone.</item>
/// </list>
///
/// Either carrier alone leaves a counter unresumed, and an unresumed counter is how a
/// continued world re-issues an id the restored objects already wear (one id, two
/// objects). The subset relation between the two is therefore NOT assumed — it does not
/// hold, and this port exists to stop the rule from depending on it.
/// </summary>
public interface IRestoredItemIdSeed
{
	/// <summary>
	/// Host/solo: hand the restore's complete id set over — the checkpoint's rows and every
	/// character's carried ids, nested container contents included. Raising a counter is
	/// idempotent (raise-only), so a superseding restore may hand a larger set over without
	/// any bookkeeping on either side.
	/// </summary>
	void SeedRestoredItemIds(IReadOnlyList<ulong> instanceIds);
}
