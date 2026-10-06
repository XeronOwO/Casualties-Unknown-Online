using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;
using Kind = CasualtiesUnknownOnline.Runtime.Session.Items.ContainerLoadClassifier.Kind;
using Source = CasualtiesUnknownOnline.Runtime.Session.Items.DropPendingState.Source;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The container load's carrier rule: which report ONE load produces (ticket
/// <c>container-move-snapshot-only-sync</c>, row A1g). The rule's whole point is
/// that the departure the move registered outvotes the scene capture — the
/// container-to-container move has already detached the item when the load hook
/// runs, so the scene says "it came from the world" for a child that never left
/// the carried inventory.
/// <para>
/// The theory's cases pass the internal enums as their member order
/// (<see cref="NoDeparture"/> / <see cref="FromCarriedInventory"/> /
/// <see cref="FromWorld"/> and the three carriers), because an xUnit theory's
/// arguments must be public types.
/// </para>
/// </summary>
public class ContainerLoadClassifierTests
{
	private const int NoDeparture = -1;
	private const int FromCarriedInventory = 0;
	private const int FromWorld = 1;

	private const int Pickup = 0;
	private const int CarriedContent = 1;
	private const int WorldContainerDrop = 2;

	[Theory]
	// The item left the WORLD and a body-side container took it in: pickup semantics.
	[InlineData(false, FromWorld, true, Pickup)]
	[InlineData(false, FromWorld, false, Pickup)]
	// A move INSIDE the carried inventory — the departure says so, whichever way the
	// scene capture read (the expansion's child was captured as a world item).
	[InlineData(false, FromCarriedInventory, true, CarriedContent)]
	[InlineData(false, FromCarriedInventory, false, CarriedContent)]
	// No departure opened this load: the scene capture is the only fact there is
	// (an item dragged off the ground, or a container's first fill).
	[InlineData(false, NoDeparture, true, Pickup)]
	[InlineData(false, NoDeparture, false, CarriedContent)]
	// A world container took the item in — the TARGET decides, whatever the item
	// came from (the bound drop report carries the container id and its position).
	[InlineData(true, FromWorld, true, WorldContainerDrop)]
	[InlineData(true, FromCarriedInventory, false, WorldContainerDrop)]
	[InlineData(true, NoDeparture, true, WorldContainerDrop)]
	public void Classify_ReadsTheLandingThenTheDepartureThenTheSceneCapture(bool landsInWorldContainer, int departure, bool wasWorldItem, int expected) =>
		Assert.Equal(
			(Kind)expected,
			ContainerLoadClassifier.Classify(landsInWorldContainer, DepartureOf(departure), wasWorldItem));

	[Fact]
	public void Classify_ADepartureOutvotesAWorldSceneCapture()
	{
		// The exact reading of batch 20261006-b: the unload that opened the pair left
		// the child parentless, so the pre-load capture answered "world item" — and the
		// load reported a pickup of the child while the target container's contents
		// changed with no event. The departure the pair itself registered is the fact
		// that says otherwise.
		Assert.Equal(Kind.CarriedContent, ContainerLoadClassifier.Classify(landsInWorldContainer: false, Source.CarriedInventory, wasWorldItem: true));
		Assert.NotEqual(Kind.CarriedContent, ContainerLoadClassifier.Classify(landsInWorldContainer: false, departure: null, wasWorldItem: true));
	}

	private static Source? DepartureOf(int departure) => departure switch
	{
		FromCarriedInventory => Source.CarriedInventory,
		FromWorld => Source.World,
		_ => null,
	};
}
