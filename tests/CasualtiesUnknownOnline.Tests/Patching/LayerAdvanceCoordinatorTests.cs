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
/// No live world exists in this host, so the progression step the delegation
/// would grant returns before it touches anything — what these cases pin is the
/// decision and the wire call, not the game-side grant.
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

		public event Action<ulong>? LayerAdvanceRequested;

		public bool TrySendLayerAdvanceRequest()
		{
			if (!sends)
			{
				return false; // no committed run baseline on this side: there is nothing to report
			}

			Sends++;
			return true;
		}

		public void HandleLayerAdvanceRequest(ulong sender, LayerAdvanceRequestMsg msg) => LayerAdvanceRequested?.Invoke(sender);
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

	private static object Resolve(Type dependency, ISessionControl session, ILayerAdvanceControl advance, ILoggerFactory factory) =>
		dependency == typeof(ISessionControl) ? session
		: dependency == typeof(ILayerAdvanceControl) ? advance
		: dependency.IsGenericType && dependency.GetGenericTypeDefinition() == typeof(ILogger<>)
			? Activator.CreateInstance(typeof(Logger<>).MakeGenericType(dependency.GetGenericArguments()[0]), factory)!
			: throw new InvalidOperationException($"LayerAdvanceCoordinator gained a dependency this fixture does not know: {dependency.FullName}.");

	private static bool TryDelegateLocalAdvance(object coordinator) =>
		(bool)Coordinator.GetMethod("TryDelegateLocalAdvance", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
			.Invoke(coordinator, null)!;

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
}
