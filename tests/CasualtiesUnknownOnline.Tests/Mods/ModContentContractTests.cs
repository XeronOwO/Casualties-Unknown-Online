using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The kind vocabulary, the nine kind contracts and the framework's ready-made
/// implementations are ONE list, and the scan reads the middle one through
/// <see cref="ModContentContract"/>.
///
/// Both halves are DISCOVERED rather than listed: the constants are the public
/// string fields of <see cref="ModContentKind"/> and the contracts are the
/// interfaces of the mod surface that extend
/// <see cref="IModContentDefinition"/>. A tenth contract without a kind, a
/// second contract for the same kind, or a data class whose <c>Kind</c> disagrees
/// with its contract each fails here instead of becoming a kind that registers and
/// never materializes.
/// </summary>
public class ModContentContractTests
{
	[Fact]
	public void EveryKindConstant_HasExactlyOneContract_AndOneReadyMadeImplementation()
	{
		var constants = KindConstants();
		var contracts = KindContracts();

		Assert.Equal(9, constants.Count);
		Assert.Equal(constants.Count, contracts.Count);

		var kinds = new List<string>();
		foreach (var contract in contracts)
		{
			Assert.True(ModContentContract.TryGetKind(contract, out var kind), $"{contract.Name} is not one of the kind contracts");
			kinds.Add(kind);

			var implementation = typeof(IModContentDefinition).Assembly.GetTypes()
				.Single(type => type.IsClass && !type.IsAbstract && contract.IsAssignableFrom(type));
			var instance = (IModContentDefinition)Activator.CreateInstance(implementation)!;
			Assert.Equal(kind, instance.Kind);
		}

		Assert.Equal(
			constants.OrderBy(kind => kind, StringComparer.Ordinal),
			kinds.OrderBy(kind => kind, StringComparer.Ordinal));
	}

	[Fact]
	public void ATypeThatIsNotAKindContract_IsNotResolved()
	{
		Assert.False(ModContentContract.TryGetKind(typeof(IModContentDefinition), out _));
		Assert.False(ModContentContract.TryGetKind(typeof(IModContent), out _));
		Assert.False(ModContentContract.TryGetKind(typeof(string), out _));
	}

	private static List<string> KindConstants() =>
		[.. typeof(ModContentKind).GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(field => field.IsLiteral && field.FieldType == typeof(string))
			.Select(field => (string)field.GetRawConstantValue()!)];

	private static List<Type> KindContracts() =>
		[.. typeof(IModContentDefinition).Assembly.GetTypes()
			.Where(type => type.IsInterface
				&& type != typeof(IModContentDefinition)
				&& typeof(IModContentDefinition).IsAssignableFrom(type))];
}
