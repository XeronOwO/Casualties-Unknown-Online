using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Regression guard for the Mapster dynamic-map failure introduced by
/// <c>CharacterLimbMsg.Components</c>: Mapster tries to use
/// <c>UnityEngine.Component.GetComponents&lt;T&gt;()</c> as the source for the
/// new Components collection, which fails at runtime with
/// "Method T[] GetComponents[T]() is a generic method definition" and aborts
/// <c>GameAdapter.Update</c> before the start-gate release restores movement.
/// The adapter config must ignore Components (and the capture-only fields) so
/// the map compiles; this test calls the real config and maps a real game
/// <see cref="Limb"/> object through Mapster.
/// </summary>
public sealed class CharacterDataMapperRegressionTests
{
	private static readonly Assembly MapsterAssembly = LoadMapster();
	private static readonly Type MapperType = MapsterAssembly.GetType("MapsterMapper.Mapper", throwOnError: true)!;
	private static readonly Type LimbType = GameAssemblyHost.Game.GetType("Limb", throwOnError: true)!;

	[Fact]
	public void Configure_AllowsLimbToCharacterLimbMsgMapping()
	{
		var mapper = Activator.CreateInstance(MapperType)!;
		InvokeConfigure();

		var limb = FormatterServices.GetUninitializedObject(LimbType);
		var map = MapperType.GetMethods()
			.Single(m => m.Name == "Map"
				&& m.IsGenericMethodDefinition
				&& m.GetParameters().Length == 1
				&& m.GetParameters()[0].ParameterType == typeof(object));
		var result = map.MakeGenericMethod(typeof(CharacterLimbMsg)).Invoke(mapper, [limb]);

		Assert.NotNull(result);
		Assert.Equal(typeof(CharacterLimbMsg), result!.GetType());
	}

	[Fact]
	public void Configure_AllowsCharacterLimbMsgToLimbMapping()
	{
		var mapper = Activator.CreateInstance(MapperType)!;
		InvokeConfigure();

		var limb = FormatterServices.GetUninitializedObject(LimbType);
		var map = MapperType.GetMethods()
			.Single(m => m.Name == "Map"
				&& m.IsGenericMethodDefinition
				&& m.GetParameters().Length == 2
				&& m.GetParameters()[0].ParameterType.IsGenericParameter
				&& m.GetParameters()[1].ParameterType.IsGenericParameter);
		var result = map.MakeGenericMethod(typeof(CharacterLimbMsg), LimbType)
			.Invoke(mapper, [new CharacterLimbMsg(), limb]);

		Assert.NotNull(result);
		Assert.Equal(LimbType, result!.GetType());
	}

	private static void InvokeConfigure()
	{
		var mapper = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Character.CharacterDataMapper",
			throwOnError: true)!;
		mapper.GetMethod("Configure", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
			.Invoke(null, null);
	}

	private static Assembly LoadMapster()
	{
		var loaded = AppDomain.CurrentDomain.GetAssemblies()
			.FirstOrDefault(a => a.GetName().Name == "Mapster");
		if (loaded != null)
		{
			return loaded;
		}

		var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mapster.dll");
		if (!File.Exists(path))
		{
			throw new InvalidOperationException("Mapster.dll missing beside the test output.");
		}

		return Assembly.LoadFrom(path);
	}
}
