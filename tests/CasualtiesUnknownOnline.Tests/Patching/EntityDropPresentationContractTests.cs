using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Contract guard for the entity-destruction drop presentation path: the
/// Game Adapter must expose a remote-apply entry that materializes/enriches
/// destructive-trap/building-death drops from the full initial state carried
/// by the event, independent of the kernel's authoritative item projection.
/// </summary>
public class EntityDropPresentationContractTests
{
	[Fact]
	public void ItemApplication_ExposesTrapDropPresentation()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Items.ItemApplication",
			throwOnError: true)!;

		var method = type.GetMethod(
			"ApplyTrapDropPresentation",
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("ItemApplication.ApplyTrapDropPresentation not found.");

		var parameter = Assert.Single(method.GetParameters());
		Assert.True(parameter.ParameterType.IsGenericType);
		Assert.Equal("IReadOnlyList`1", parameter.ParameterType.Name);
		Assert.Equal("TrapDropEntryMsg", parameter.ParameterType.GetGenericArguments()[0].Name);
	}
}
