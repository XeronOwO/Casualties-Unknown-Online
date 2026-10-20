namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Which arrival report a CANCELLED departure needs. Pure — no Unity type, no scene read — because
/// the departures are cancelled in the Game Adapter (the re-pick and the radial wear both do it
/// there) and the adapter has no unit harness; the decision is testable here and the adapter only
/// executes it.
///
/// <para>
/// The fact that makes a report necessary at all: the kernel records a container's children as the
/// CONTAINER's rows (<c>ItemLocation.Contained(owner, parent)</c> — <c>DecideSyncContainer</c>,
/// <c>ItemArbitration.AdoptCarriedContents</c>), while the report a cancelled departure sends on the
/// normal path is the slot move's, which is DATA ONLY (<c>ItemUpdateState</c>). So an item released
/// out of a container keeps its old contained row until the next container report for that parent
/// arrives — and that report does not name it, so <c>ItemDomainModule.DecideSyncContainer</c> takes
/// it to <c>Terminal (ReplacedBy)</c>, which no later command may revive.
/// </para>
///
/// <para>
/// WHICH report depends on the home, because the two settle differently when the host refuses. A
/// body slot's item was already <c>Carried</c> in the record: nothing to correct, and a second
/// relocation report would be a fact with two owners. A container the body holds leaves the item
/// inside that container's record, and the item is now in the member's hands — the TAKE-OUT states
/// that, and a refusal settles by re-stating the carried set (the item must never be unparented into
/// the world for a report that was never about a world item). A container in the WORLD leaves a world
/// row behind (a drop into a container carries the container id), and taking an item out of one is
/// ordinary looting: the PICKUP states it, and a refusal keeps that family's answer — the local copy
/// goes back to the world, which is where a refused world item belongs.
/// </para>
/// </summary>
internal static class ContainerArrivalPolicy
{
	/// <summary>The report a cancelled departure's arrival needs, if any.</summary>
	internal enum Report
	{
		/// <summary>The item was carried already (a body slot or a limb): its row needs no move.</summary>
		None,

		/// <summary>The item came out of a world container: state it as the pickup it is.</summary>
		Pickup,

		/// <summary>The item came out of a container the body holds: state it as the take-out it is.</summary>
		TakeOut,
	}

	/// <summary>
	/// The report for one cancelled departure, read off the home it left. Both of a body's homes are
	/// covered: the re-pick out of a bag and the radial wear straight out of one cancel the same
	/// departure and need the same statement.
	/// </summary>
	internal static Report For(DropPendingState.Source departure) => departure switch
	{
		DropPendingState.Source.World => Report.Pickup,
		DropPendingState.Source.CarriedContainer => Report.TakeOut,
		_ => Report.None,
	};
}
