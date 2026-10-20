using System;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;
using Report = CasualtiesUnknownOnline.Runtime.Session.Items.ContainerArrivalPolicy.Report;
using Source = CasualtiesUnknownOnline.Runtime.Session.Items.DropPendingState.Source;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// Which arrival report a cancelled departure needs. The rule is one distinction — did the item come
/// out of a body slot (already carried in the record), out of a container the body holds (recorded as
/// that container's child) or out of a container in the world (recorded as a world row) — and it lives
/// in the Runtime as a pure function because the callers that draw it sit in the Unity adapter, which
/// has no unit harness.
/// <para>
/// The two container homes answer DIFFERENT reports, and that is the ticket's second half: a take-out's
/// refusal settles by re-stating the carried set (the item is in the member's hands), while a pickup's
/// keeps the world rollback (an item looted out of a ground crate is a world item the host may refuse,
/// and the local copy belongs back in the world).
/// </para>
/// </summary>
public class ContainerArrivalPolicyTests
{
	// The theory passes the internal enums as member ORDINALS, because an xUnit theory's arguments
	// must be public types (the sibling classifier suite states the same reason).
	private const int CarriedInventory = 0;
	private const int CarriedContainer = 1;
	private const int World = 2;

	private const int None = 0;
	private const int Pickup = 1;
	private const int TakeOut = 2;

	[Theory]
	[InlineData(CarriedContainer, TakeOut)]
	[InlineData(World, Pickup)]
	[InlineData(CarriedInventory, None)]
	public void For_ReadsTheHomeTheItemLeft(int departure, int expected) =>
		Assert.Equal((Report)expected, ContainerArrivalPolicy.For((Source)departure));

	/// <summary>
	/// The WIRING half, pinned because the two call sites live in the Unity adapter, which has no unit
	/// harness: the re-pick's cancelled departure (<c>PickupSync.OnPickedUp</c>) and the radial wear's
	/// (<c>ItemSlotSync.ReportCarried</c>) must both ask this policy and send BOTH kinds through it — a
	/// mis-wire that dropped the call, or wired only the take-out, would leave the wear (or the looting
	/// of a ground crate) with the data-only report and every other test in this suite still green.
	/// This is a source-shape pin, not runtime evidence: the runtime half is the sim-world suite
	/// (<c>ContainerTakeOutTests</c>) plus the ticket's acceptance row, and this pin exists so a deletion
	/// cannot pass silently in the meantime.
	/// </summary>
	[Fact]
	public void TheTwoDepartureSites_SendThePolicysReport()
	{
		foreach (var file in new[] { "PickupSync.cs", "ItemSlotSync.cs" })
		{
			var source = ReadAdapterFile(file);
			Assert.Contains("ContainerArrivalPolicy.For(departure)", source);
			Assert.Contains("case ContainerArrivalPolicy.Report.TakeOut:", source);
			Assert.Contains("_items.SendItemContainerTakeOut(", source);
			Assert.Contains("case ContainerArrivalPolicy.Report.Pickup:", source);
			Assert.Contains("_items.SendItemPickedUp(", source);
		}
	}

	private static string ReadAdapterFile(string name)
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		Assert.True(directory is not null, "the repository root (CasualtiesUnknownOnline.slnx) was not found above the test binaries");
		return File.ReadAllText(Path.Combine(directory!.FullName, "src", "CasualtiesUnknownOnline.GameAdapter", "Items", name));
	}
}
