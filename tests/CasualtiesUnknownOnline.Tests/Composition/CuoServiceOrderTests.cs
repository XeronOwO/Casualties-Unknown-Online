using System;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Composition;

/// <summary>
/// Pins the composition root's registration order. That order is the contract the
/// plugin depends on: <c>GetServices&lt;ICuoService&gt;()</c> hands it the per-frame
/// update order, and the feature composition modules
/// (<c>Runtime/Composition/*</c>) are called in exactly the sequence the registrations
/// stood in when they lived inline in <c>CuoBootstrap</c>. A module split that moved
/// one call would silently reorder the update loop, so the order is asserted here
/// rather than left to convention.
/// </summary>
[Trait("Category", "Integration")]
public class CuoServiceOrderTests
{
	/// <summary>The update order the tree depends on, by implementation type, in registration order.</summary>
	private static readonly string[] ExpectedUpdateOrder =
	[
		"SteamService",
		"SteamTransport",
		"IpDirectTransport",
		"SessionService",
		"NetworkTrafficMonitor",
		"PacketDispatcher",
		"EntitySyncService",
		"EnemySyncService",
		"TutorialClawService",
		"WorldReportFallbackPump",
		"SessionControlConvergence",
		"ProjectionHealthCoordinator",
		"GuestCommandReconciliation",
		"MedicalOperationSessionService",
		"ItemTrafficPump",
		"ModStatusProjectionReadModel",
		"ModService",
		"ModContentBinder"
	];

	/// <summary>The content-source order the resource catalog ranks stages against: the built-in source registers before the mod-contributed one.</summary>
	private static readonly string[] ExpectedResourceSourceOrder =
	[
		"BuiltInResourceLocationSource",
		"ModContentResourceLocationSource"
	];

	[Fact]
	public void TheUpdateOrder_IsThePinnedRegistrationOrder()
	{
		using var services = BuildComposition();
		var order = services.GetServices<ICuoService>().Select(service => service.GetType().Name).ToList();

		Assert.True(
			ExpectedUpdateOrder.SequenceEqual(order, StringComparer.Ordinal),
			"the ICuoService update order moved:" + Environment.NewLine
			+ "expected: " + string.Join(", ", ExpectedUpdateOrder) + Environment.NewLine
			+ "actual:   " + string.Join(", ", order));
		Assert.True(
			order.Count == order.Distinct(StringComparer.Ordinal).Count(),
			"a service implementation is registered as ICuoService more than once: " + string.Join(", ", order));
	}

	[Fact]
	public void TheContentSourceOrderAndHandlerDiscovery_AreStillWired()
	{
		using var services = BuildComposition();
		var order = services.GetServices<IResourceLocationSource>().Select(source => source.GetType().Name).ToList();
		Assert.True(
			ExpectedResourceSourceOrder.SequenceEqual(order, StringComparer.Ordinal),
			"the IResourceLocationSource order moved: " + string.Join(", ", order));
		Assert.NotEmpty(services.GetServices<IPacketHandler>());
	}

	private static ServiceProvider BuildComposition()
	{
		var logDirectory = Path.Combine(Path.GetTempPath(), "cuo-tests", "composition-order-" + Guid.NewGuid().ToString("N"));
		return CuoBootstrap.BuildServiceProvider(
			new ManualLogSource("test"),
			logDirectory,
			extraRegistrations: TestLogging.RemoveFileSink);
	}
}
