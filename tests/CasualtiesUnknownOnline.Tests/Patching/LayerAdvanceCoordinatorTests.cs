using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The end-of-layer choice's adapter decision as BEHAVIOUR: a guest in a live
/// session hands its descent to the host — one request on the control surface,
/// and the caller is told to suppress the local regeneration — while the host,
/// a solo player and a client without a session keep the game's own descent. The
/// adapter is compile-excluded (it binds game/Unity assemblies), so the
/// coordinator is built reflectively through its own constructor with the
/// Runtime interface standing in as the double (the shape
/// <c>StartingSupplyCoordinatorTests</c> established).
/// <para>
/// These cases pin the delegation decision and the wire call, not the member's
/// descent: that runs from the host's edge (<c>LayerDescentTests</c> /
/// <c>LayerDescentPolicyTests</c>).
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class LayerAdvanceCoordinatorTests
{
	private static readonly Type Coordinator = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.World.LayerAdvanceCoordinator",
		throwOnError: true)!;

	private sealed class FakeLayerAdvance(bool sends = true) : ILayerAdvanceControl
	{
		public int Sends { get; private set; }

		/// <summary>The two-layer flag of the last request sent, so the pod's own step is checkable.</summary>
		public bool LastTwice { get; private set; }

		public event Action<ulong, bool>? LayerAdvanceRequested;

		public bool TrySendLayerAdvanceRequest(bool twice)
		{
			if (!sends)
			{
				return false; // no committed run baseline on this side: there is nothing to report
			}

			Sends++;
			LastTwice = twice;
			return true;
		}

		public void HandleLayerAdvanceRequest(ulong sender, LayerAdvanceRequestMsg msg) => LayerAdvanceRequested?.Invoke(sender, msg.Twice);
	}

	private static (object Coordinator, FakeLayerAdvance Advance) Build(SessionRole role, bool sessionActive, bool sends = true)
	{
		var session = new FakeSessionControl { Role = role, SessionActive = sessionActive };
		var advance = new FakeLayerAdvance(sends);
		var factory = LoggerFactory.Create(_ => { });
		var parameters = Constructor().GetParameters();
		var arguments = parameters
			.Select(parameter => Resolve(parameter.ParameterType, session, advance, factory))
			.ToArray();

		return (Constructor().Invoke(arguments), advance);
	}

	private static ConstructorInfo Constructor() =>
		Coordinator.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();

	private static object? Resolve(Type dependency, ISessionControl session, ILayerAdvanceControl advance, ILoggerFactory factory) =>
		dependency == typeof(ISessionControl) ? session
		: dependency == typeof(ILayerAdvanceControl) ? advance
		// The host's descent send is the other direction and needs a live world; these cases exercise the
		// member's own choice, which never reads it.
		: dependency == typeof(IWorldControl) ? null
		: dependency.IsGenericType && dependency.GetGenericTypeDefinition() == typeof(ILogger<>)
			? Activator.CreateInstance(typeof(Logger<>).MakeGenericType(dependency.GetGenericArguments()[0]), factory)!
			: throw new InvalidOperationException($"LayerAdvanceCoordinator gained a dependency this fixture does not know: {dependency.FullName}.");

	private static bool TryDelegateLocalAdvance(object coordinator, bool twice = false) =>
		(bool)Coordinator.GetMethod("TryDelegateLocalAdvance", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
			.Invoke(coordinator, [twice])!;

	[Theory]
	[InlineData(SessionRole.Guest, true, true)]   // the member at the layer's bottom hands the descent to the host
	[InlineData(SessionRole.Host, true, false)]   // the host's own descent IS the session's advance
	[InlineData(SessionRole.None, true, false)]   // solo
	[InlineData(SessionRole.Guest, false, false)] // a guest with no live session (the session ended)
	public void TryDelegateLocalAdvance_HandsTheDescentOverExactlyForAGuestInALiveSession(
		SessionRole role, bool sessionActive, bool expected)
	{
		var (coordinator, advance) = Build(role, sessionActive);

		Assert.Equal(expected, TryDelegateLocalAdvance(coordinator));
		Assert.Equal(expected ? 1 : 0, advance.Sends);
	}

	/// <summary>
	/// A guest in a live session whose choice CANNOT be reported (no committed run baseline to stamp it with)
	/// keeps the game's own descent: suppressing it would leave the member unable to move either the session
	/// or itself.
	/// </summary>
	[Fact]
	public void TryDelegateLocalAdvance_WhenNothingWasReported_KeepsTheGameDescend()
	{
		var (coordinator, advance) = Build(SessionRole.Guest, sessionActive: true, sends: false);

		Assert.False(TryDelegateLocalAdvance(coordinator));
		Assert.Equal(0, advance.Sends);
	}

	/// <summary>
	/// The drill pod's own descent carries TWO layers, and the request must carry that step: the host drives the
	/// pod's two-layer advance rather than the panel's one-layer one.
	/// </summary>
	[Fact]
	public void TryDelegateLocalAdvance_CarriesThePodsTwoLayerStep()
	{
		var (coordinator, advance) = Build(SessionRole.Guest, sessionActive: true);

		Assert.True(TryDelegateLocalAdvance(coordinator, twice: true));
		Assert.Equal(1, advance.Sends);
		Assert.True(advance.LastTwice, "the pod's two-layer step must reach the request");
	}

	/// <summary>The panel's own click stays a one-layer advance in the request.</summary>
	[Fact]
	public void TryDelegateLocalAdvance_ThePanelStaysOneLayer()
	{
		var (coordinator, advance) = Build(SessionRole.Guest, sessionActive: true);

		Assert.True(TryDelegateLocalAdvance(coordinator));
		Assert.False(advance.LastTwice);
	}
}
