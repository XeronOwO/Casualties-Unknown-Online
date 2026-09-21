using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Shape gate for the adapter seam (review/adapter-capability-ports.md).
/// <see cref="IGameAdapter"/> is the COMPOSITION of the capability ports and
/// declares no member of its own, so a consumer resolves the port whose
/// capability it uses and a version adapter implements per capability. Each entry
/// below pins one port and the members it may declare: a member added back onto
/// the aggregate, a member moved between ports, a port dropped from the
/// composition, or a port left unregistered in the plugin's composition root
/// fails here instead of regrowing the old wide surface silently.
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
		(typeof(ILocalHealItemQuery), ["GetLocalHealItems", "HasLocalHealItem"]),
		(typeof(ITraderRecruitRequest), ["TryRequestTraderRecruit"]),
		(typeof(INativeInputBlocker), ["SetOnlineUiEscapeSurfaceVisible", "SetOnlineUiModal", "SetOnlineUiScopedBlocks"]),
		(typeof(IRemoteInventoryPresentation), ["OpenRemoteBackpack"]),
		(typeof(IRemoteMedicalPresentation), ["OpenRemoteMedical"]),
		(typeof(IPlayerAnchorQuery), ["TryGetRemoteHeadPosition"]),
	];

	/// <summary>Where the ports must be registered from the one adapter singleton (checked as source, because the plugin project is not referenced by the tests).</summary>
	private static readonly string RegistrarPath = Path.GetFullPath(Path.Combine(
		AppContext.BaseDirectory,
		"..", "..", "..", "..", "..",
		"src", "CasualtiesUnknownOnline.Plugin", "PluginDependencyRegistrar.cs"));

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

	[Fact]
	public void Composition_CarriesExactlyFourteenMembers() =>
		Assert.Equal(14, Ports.Sum(entry => DeclaredMembers(entry.Port).Length));

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

	[Fact]
	public void EveryPort_IsRegisteredExactlyOnceFromTheAdapterSingleton()
	{
		Assert.True(File.Exists(RegistrarPath), $"composition root not found at {RegistrarPath}");

		var source = File.ReadAllText(RegistrarPath);
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
			[$"AddSingleton<{portName}>(p => p.GetRequiredService<GameAdapterImpl>())"],
			StringSplitOptions.None).Length - 1;

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
