using System;
using System.Reflection;
using CasualtiesUnknownOnline.Tests.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The starting-supplies grant's once-per-body record (S4.3). It exists as its own type
/// because getting its rule wrong is a silent, player-visible failure: a body judged twice
/// refills a backpack, and a body judged by VALUE rather than identity would treat two
/// different bodies as one — on Unity objects that comparison means "destroyed", which is
/// exactly the case a scene swap produces.
///
/// Reached by reflection (the Game Adapter is referenced with <c>ExcludeAssets="compile"</c>).
/// </summary>
[Trait("Category", "Integration")]
public class StartingSupplyGrantTrackerTests
{
	[Fact]
	public void WasSupplied_TracksEachBodyOnItsOwn()
	{
		var tracker = Tracker();
		var body = new object();

		Assert.False(WasSupplied(tracker, body));

		MarkSupplied(tracker, body);

		Assert.True(WasSupplied(tracker, body));
		Assert.False(WasSupplied(tracker, new object()), "a different body is a different entry");
	}

	[Fact]
	public void WasSupplied_TwoSeparateInstances_AreTwoBodies()
	{
		// The reference comparer, not Equals: the handles the grant keys on can be any object
		// (a live Body is one), and two of them that HAPPEN to compare equal must still be
		// two bodies — a scene swap hands out new ones.
		var tracker = Tracker();
		var first = new ComparableHandle("body");
		var second = new ComparableHandle("body");

		Assert.Equal(first, second); // the value comparison says they are the same
		MarkSupplied(tracker, first);

		Assert.True(WasSupplied(tracker, first));
		Assert.False(WasSupplied(tracker, second), "identity, never equality — two handles that compare equal are two bodies");
	}

	[Fact]
	public void Clear_ForgetsEveryBody()
	{
		// A run this client owns is taking over: the recorded bodies belong to the world
		// being left. Without this the rule would be "supplied once per process" for a body
		// a scene reload happened to reuse.
		var tracker = Tracker();
		var body = new object();
		MarkSupplied(tracker, body);
		Assert.Equal(1, SuppliedCount(tracker));

		Clear(tracker);

		Assert.Equal(0, SuppliedCount(tracker));
		Assert.False(WasSupplied(tracker, body));
	}

	/// <summary>A handle that compares equal to another with the same value, to pin identity-vs-equality.</summary>
	private sealed record ComparableHandle(string Name);

	private static readonly Type TrackerType = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.StartingSupplyGrantTracker",
		throwOnError: true)!;

	private static object Tracker() => Activator.CreateInstance(TrackerType, nonPublic: true)!;

	private static bool WasSupplied(object tracker, object body) =>
		(bool)Method("WasSupplied").Invoke(tracker, [body])!;

	private static void MarkSupplied(object tracker, object body) =>
		Method("MarkSupplied").Invoke(tracker, [body]);

	private static int SuppliedCount(object tracker) =>
		(int)Property("SuppliedCount").GetValue(tracker)!;

	private static void Clear(object tracker) => Method("Clear").Invoke(tracker, null);

	private static MethodInfo Method(string name) =>
		TrackerType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException($"StartingSupplyGrantTracker.{name} not found.");

	private static PropertyInfo Property(string name) =>
		TrackerType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException($"StartingSupplyGrantTracker.{name} not found.");
}
