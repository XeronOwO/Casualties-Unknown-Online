using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// B6: the adapter's native world-fact implementation is the Runtime port's
/// implementation, and it stays constructible from the composition root's own
/// services (a logger) — the plugin registers it once and resolves the port to
/// the SAME instance.
///
/// Scope note: the registration mapping itself lives in the plugin assembly,
/// which is not in this test's compile graph; that half is covered by the
/// deployed run (the save layer resolves the port and names it in its logs).
/// </summary>
[Trait("Category", "Integration")]
public sealed class NativeWorldFactsContractTests
{
	[Fact]
	public void NativeWorldFacts_IsAPublicRuntimePortImplementation()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.World.NativeWorldFacts",
			throwOnError: true);
		Assert.NotNull(type);
		Assert.True(type!.IsPublic, $"{type.FullName} is registered from the plugin assembly, so it must be public");
		Assert.True(
			typeof(INativeWorldFacts).IsAssignableFrom(type),
			$"{type.FullName} must implement {nameof(INativeWorldFacts)}");

		var constructor = Assert.Single(type.GetConstructors());
		var parameter = Assert.Single(constructor.GetParameters());
		Assert.True(
			parameter.ParameterType.IsGenericType
				&& parameter.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>),
			$"the composition root can only construct it from container services; found {parameter.ParameterType}");
	}
}
