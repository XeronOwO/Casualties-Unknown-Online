using CasualtiesUnknownOnline.Runtime.Session.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Shape gate for the adapter seam (done/adapter-capability-ports.md).
/// <see cref="IGameAdapter"/> is the COMPOSITION of the capability ports and
/// declares no member of its own, so a consumer resolves the port whose
/// capability it uses and a version adapter implements per capability. Each entry
/// below pins one port and the members it may declare: a member added back onto
/// the aggregate, a member moved between ports, a port dropped from the
/// composition, or a port left unregistered in the adapter's composition
/// (GameAdapterComposition) fails here instead of regrowing the old wide surface
/// silently.
/// </summary>
public class AdapterCapabilityPortShapeTests
{
	private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

	/// <summary>The composition: every port the aggregate lists, with the members that port declares.</summary>
	private static readonly (Type Port, string[] Members)[] Ports =
	[
		(typeof(IGameIntegrationLifecycle), ["OnApplicationQuit"]),
		(typeof(IAdapterCapabilityQuery), ["CapabilityReport"]),
		(typeof(IWorldPresenceQuery), ["IsInWorldOrGenerating"]),
		(typeof(IStartGateState), ["IsWaitingForReady", "WaitingText"]),
		(typeof(IDragUseCueState), ["Showing", "TargetSteamId"]),
		(typeof(ILocalHealItemQuery), ["GetLocalHealItems", "HasLocalHealItem"]),
		(typeof(ITraderRecruitRequest), ["TryRequestTraderRecruit"]),
		(typeof(INativeInputBlocker), ["IsTheGamesOwnTextInputFocused", "SetOnlineUiEscapeSurfaceVisible", "SetOnlineUiModal"]),
		(typeof(IRemoteInventoryPresentation), ["OpenRemoteBackpack"]),
		(typeof(IRemoteMedicalPresentation), ["OpenRemoteMedical"]),
		(typeof(IPlayerAnchorQuery), ["TryGetRemoteHeadPosition"]),
		(typeof(IJoinFlowPresentation), ["PrepareForDirectJoin"]),
		(typeof(ICarryPresentationPump), ["PinCarriedPresentation"]),
		(typeof(IOnlineUiNativeFactsQuery), ["Capture"]),
		(typeof(IOnlineUiSurface), ["Push", "TryDequeueIntent"]),
		(typeof(ISpectatorControl), ["IsWatching", "StopWatching", "TrySetFreeFly", "TryWatch", "WatchingSteamId"]),
	];

	/// <summary>Where the ports must be registered from the one adapter singleton (checked as source, because the tests load the adapter reflectively and cannot resolve its container).</summary>
	private static readonly string CompositionPath = Path.GetFullPath(Path.Combine(
		AppContext.BaseDirectory,
		"..", "..", "..", "..", "..",
		"src", "CasualtiesUnknownOnline.GameAdapter", "GameAdapterComposition.cs"));

	public static IEnumerable<object[]> PortCensus() =>
		Ports.Select(entry => new object[] { entry.Port.Name, Census(entry.Members) });

	[Fact]
	public void Aggregate_DeclaresNoMemberOfItsOwn() =>
		Assert.Empty(DeclaredMembers(typeof(IGameAdapter)));

	[Theory]
	[MemberData(nameof(PortCensus))]
	public void Port_DeclaresExactlyItsPinnedMembers(string portName, string expected)
	{
		var port = Ports.Single(entry => entry.Port.Name == portName).Port;

		Assert.Equal(expected, Census(DeclaredMembers(port)));
	}

	[Fact]
	public void Aggregate_ComposesExactlyThePinnedPorts()
	{
		var composed = typeof(IGameAdapter).GetInterfaces();
		var pinned = Ports.Select(entry => entry.Port).Append(typeof(IDisposable));

		Assert.Equal(
			Census(pinned.Select(type => type.Name)),
			Census(composed.Select(type => type.Name)));
	}

	/// <summary>
	/// The composition's member count, pinned so a port cannot be widened without a decision. The
	/// number lives in the assertion rather than in this name: naming it here meant renaming the test
	/// every time a port was added, which made the review of a new port noisier than the port itself.
	/// </summary>
	[Fact]
	public void Composition_CarriesExactlyThePinnedMemberCount() =>
		Assert.Equal(26, Ports.Sum(entry => DeclaredMembers(entry.Port).Length));

	[Fact]
	public void NoMemberName_IsSharedByTwoPorts()
	{
		var shared = Ports
			.SelectMany(entry => DeclaredMembers(entry.Port).Select(member => (Port: entry.Port.Name, Member: member)))
			.GroupBy(pair => pair.Member)
			.Where(group => group.Count() > 1)
			.Select(group => $"{group.Key}: {string.Join(" + ", group.Select(pair => pair.Port))}")
			.ToArray();

		Assert.Empty(shared);
	}

	/// <summary>
	/// The console <c>/summon</c> verb's scene seam is the THIRD standalone adapter seam, for the same hard reason:
	/// the console service is built at startup and consumes it, while the adapter aggregate's own graph depends on
	/// the controls the seam's neighbours live in — routing it through <c>GameAdapter</c> would put the aggregate
	/// inside the console's construction, the shape the give seam already tripped in batch <c>20261017-q</c>.
	/// </summary>
	[Fact]
	public void WorldSummonSeam_IsStandalone()
	{
		Assert.True(File.Exists(CompositionPath), $"composition root not found at {CompositionPath}");

		var code = string.Join("\n", File.ReadAllLines(CompositionPath)
			.Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

		Assert.Equal(0, CountCodeOccurrences(code, "AddSingleton<IWorldSummon>"));
		Assert.Equal(1, CountCodeOccurrences(code, "Replace(ServiceDescriptor.Singleton<IWorldSummon>"));
		Assert.Equal(1, CountCodeOccurrences(code, "GetRequiredService<WorldSummoner>())"));
		Assert.Equal(1, CountCodeOccurrences(code, "AddSingleton<WorldSummoner>()"));
	}

	/// <summary>
	/// The <c>/give</c> verb's creation seam is NOT a port on the adapter singleton, and
	/// the reason is a hard one: <c>PlayerInteractionService</c> consumes the port and
	/// <c>GameAdapter</c> depends on <c>IPlayerInteractionControl</c> (it subscribes to
	/// the interaction events), so routing the port through the aggregate closes a
	/// constructor cycle. On 2026-10-17 the deployed build did exactly that and the
	/// plugin never started — "CUO startup failed: A circular dependency was detected
	/// during CUO service resolution: IGameIntegrationLifecycle -> GameAdapter ->
	/// IPlayerInteractionControl -> PlayerInteractionService -> IItemGrantSource ->
	/// GameAdapter" (batch 20261017-q). The seam stands alone over the ONE allocator:
	/// a second <c>ItemIdAllocator</c> would mint a second counter, which is why the
	/// allocator is the container's and the seam resolves it rather than building one.
	/// </summary>
	[Fact]
	public void GiveCreationSeam_IsStandaloneOverTheOneAllocator()
	{
		Assert.True(File.Exists(CompositionPath), $"composition root not found at {CompositionPath}");

		var code = string.Join("\n", File.ReadAllLines(CompositionPath)
			.Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

		// The seeded default must be REPLACED by the standalone seam: an assertion on the
		// seam's own registration alone would stay green while the port silently resolved
		// the Runtime's UnavailableItemGrantSource (a verb that refuses everything).
		Assert.Equal(0, CountCodeOccurrences(code, "AddSingleton<IItemGrantSource>"));
		Assert.Equal(1, CountCodeOccurrences(code, "Replace(ServiceDescriptor.Singleton<IItemGrantSource>"));
		Assert.Equal(1, CountCodeOccurrences(code, "GetRequiredService<ItemGrantSource>())"));
		Assert.Equal(1, CountCodeOccurrences(code, "AddSingleton<ItemGrantSource>()"));
		Assert.Equal(1, CountCodeOccurrences(code, "AddSingleton<ItemIdAllocator>()"));
	}

	/// <summary>
	/// The container half of the give ladder is the SECOND standalone seam, for the same hard reason: the take
	/// service that consumes it is built inside <c>PlayerInteractionService</c>, which the aggregate's own
	/// constructor depends on, so routing it through <c>GameAdapter</c> would close the same cycle the give seam
	/// already tripped in batch <c>20261017-q</c>. The pin exists so a later change that registers it on the
	/// aggregate fails a test instead of the startup.
	/// </summary>
	[Fact]
	public void GiveContainerFitSeam_IsStandalone()
	{
		Assert.True(File.Exists(CompositionPath), $"composition root not found at {CompositionPath}");

		var code = string.Join("\n", File.ReadAllLines(CompositionPath)
			.Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

		Assert.Equal(0, CountCodeOccurrences(code, "AddSingleton<IItemContainerFit>"));
		Assert.Equal(1, CountCodeOccurrences(code, "Replace(ServiceDescriptor.Singleton<IItemContainerFit>"));
		Assert.Equal(1, CountCodeOccurrences(code, "GetRequiredService<ItemContainerFit>())"));
		Assert.Equal(1, CountCodeOccurrences(code, "AddSingleton<ItemContainerFit>()"));
	}

	[Fact]
	public void ItemStateLanguageSeam_ReplacesTheRuntimeDefault()
	{
		Assert.True(File.Exists(CompositionPath), $"composition root not found at {CompositionPath}");

		var code = string.Join("\n", File.ReadAllLines(CompositionPath)
			.Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

		// The Runtime seeds UnavailableItemStateLanguage; the adapter must REPLACE it. A
		// dropped Replace leaves every suite green (the test host registers the fake) while
		// a deployed /data answers "this build cannot express an item state".
		Assert.Equal(1, CountCodeOccurrences(code, "Replace(ServiceDescriptor.Singleton<IItemStateLanguage>"));
	}

	[Fact]
	public void EveryPort_IsRegisteredExactlyOnceFromTheAdapterSingleton()
	{
		Assert.True(File.Exists(CompositionPath), $"composition root not found at {CompositionPath}");

		var source = File.ReadAllText(CompositionPath);
		var problems = Ports
			.Select(entry => (Name: entry.Port.Name, Count: CountRegistrations(source, entry.Port.Name)))
			.Where(pair => pair.Count != 1)
			.Select(pair => $"{pair.Name}: registered {pair.Count} time(s) from the adapter singleton")
			.ToArray();

		Assert.Empty(problems);
	}

	/// <summary>
	/// Zero registrations leaves a port unresolvable and two make the last descriptor
	/// win, so the pin counts occurrences instead of asserting presence.
	/// </summary>
	private static int CountRegistrations(string source, string portName) =>
		source.Split(
			[$"AddSingleton<{portName}>(p => p.GetRequiredService<GameAdapter>())"],
			StringSplitOptions.None).Length - 1;

	/// <summary>
	/// Counts a literal in code lines only. A commented-out line must not satisfy a pin: the
	/// registration a reader deletes is exactly the one a comment could keep alive here.
	/// </summary>
	private static int CountCodeOccurrences(string code, string text) =>
		code.Split([text], StringSplitOptions.None).Length - 1;

	/// <summary>
	/// The aggregate check's own matcher contract: a composition that declares a
	/// member of its own must be flagged, a clean composition must not — for every
	/// member kind the census reads, an event included (its accessors are
	/// special-name methods and are filtered, so only <c>GetEvents</c> sees it). The
	/// samples are declared here rather than read from the tree, so this test cannot
	/// be satisfied by the aggregate happening to be empty.
	/// </summary>
	[Fact]
	public void AggregateCheck_FlagsAMemberDeclaredOnTheComposition()
	{
		Assert.Empty(DeclaredMembers(typeof(ICleanAggregateSample)));
		Assert.Equal(
			["Changed", "RegrownMember"],
			DeclaredMembers(typeof(IRegrownAggregateSample)).OrderBy(name => name, StringComparer.Ordinal));
	}

	/// <summary>
	/// A member is its method, property or event name — a property is not reported
	/// as its accessor, so the census reads like the interface a consumer sees.
	/// </summary>
	private static string[] DeclaredMembers(Type type) =>
	[
		.. type.GetMethods(Declared).Where(method => !method.IsSpecialName).Select(method => method.Name),
		.. type.GetProperties(Declared).Select(property => property.Name),
		.. type.GetEvents(Declared).Select(@event => @event.Name),
	];

	private static string Census(IEnumerable<string> members) =>
		string.Join(",", members.OrderBy(name => name, StringComparer.Ordinal));

	private interface IPortSample
	{
		void PortMember();
	}

	private interface ICleanAggregateSample : IPortSample
	{
	}

	private interface IRegrownAggregateSample : IPortSample
	{
		event EventHandler Changed;

		void RegrownMember();
	}
}
