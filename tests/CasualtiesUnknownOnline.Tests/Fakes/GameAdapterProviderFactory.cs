using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// Builds one Game Adapter content provider reflectively — the suites that drive
/// the providers' build sites never compile-reference the adapter (it binds the game
/// assemblies), so every one of them has to reflect over a constructor whose
/// parameter list is the provider's own business.
///
/// <para>
/// The logger and any argument a test supplies itself come first, in declaration
/// order; everything AFTER them is supplied by parameter TYPE from the framework's
/// own seams. A parameter this factory cannot supply fails loudly and names the type,
/// which is the point: a new provider dependency must be a deliberate line here
/// rather than a construction that throws
/// <c>MissingMethodException</c> in every suite at once.
/// </para>
/// </summary>
internal static class GameAdapterProviderFactory
{
	/// <summary>Builds the provider, supplying its trailing framework seams from <see cref="ByParameterType"/>.</summary>
	internal static object Create(Type providerType, object logger, params object[] leadingArguments) =>
		Activator.CreateInstance(providerType, [.. Arguments(providerType, logger, leadingArguments)])!;

	/// <summary>The full argument list one provider's single constructor takes, in its own order.</summary>
	internal static IReadOnlyList<object> Arguments(Type providerType, object logger, params object[] leadingArguments)
	{
		var constructor = providerType.GetConstructors().Single();
		var arguments = new List<object> { logger };
		arguments.AddRange(leadingArguments);

		foreach (var parameter in constructor.GetParameters().Skip(arguments.Count))
		{
			arguments.Add(ByParameterType(parameter.ParameterType)
				?? throw new InvalidOperationException(
					$"{providerType.Name} takes a {parameter.ParameterType.Name} parameter that GameAdapterProviderFactory cannot supply."));
		}

		return arguments;
	}

	/// <summary>The framework seams a provider takes beside its own arguments, or null when this factory knows none for the type.</summary>
	private static object? ByParameterType(Type parameterType) =>
		parameterType == typeof(ModLiquidEffectStore) ? new ModLiquidEffectStore()
		: parameterType == typeof(ModItemEffectStore) ? new ModItemEffectStore()
		: parameterType == typeof(ISessionControl) ? new FakeSessionControl()
		: null;
}
