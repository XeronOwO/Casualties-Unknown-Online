using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CasualtiesUnknownOnline.Tests.ContractTool;

/// <summary>
/// Emits the "two builds" acceptance fixture with Mono.Cecil: the same game
/// assembly at two revisions, plus the adapter assembly that declares the
/// patch-target contracts, all written as METADATA (nothing is loaded or run).
///
/// The pair is deliberately built to carry every classification the ticket
/// names: a renamed method, a return-type move, an unconstrained target that
/// gains an overload, a renamed target parameter Harmony binds by name, a field
/// type and a field visibility move, an enum value move, a removed type with a
/// same-shape successor, an addition, a change outside every contract's orbit,
/// and a target that does not move at all. The same fixture is what the CLI
/// process test runs the real tool against.
/// </summary>
internal static class ContractFixtureAssemblies
{
	internal const string GameAssemblyName = "ContractFixture.Game";

	internal const string AdapterAssemblyName = "ContractFixture.Adapter";

	internal sealed record FixtureSet(string PreviousGame, string CurrentGame, string Adapter, string Directory);

	/// <summary>
	/// Writes the three assemblies under a test-owned temp directory named after
	/// the caller and returns their paths. The directory name is the caller's
	/// because xUnit runs different test classes in parallel: two classes sharing
	/// one directory would delete each other's fixtures mid-run.
	/// </summary>
	internal static FixtureSet Build(string owner)
	{
		var directory = Path.Combine(Path.GetTempPath(), "cuo-contract-tool-tests", owner);
		if (Directory.Exists(directory))
		{
			Directory.Delete(directory, recursive: true);
		}

		var previous = Path.Combine(directory, "previous", GameAssemblyName + ".dll");
		var current = Path.Combine(directory, "current", GameAssemblyName + ".dll");
		var adapter = Path.Combine(directory, "adapter", AdapterAssemblyName + ".dll");
		Directory.CreateDirectory(Path.GetDirectoryName(previous)!);
		Directory.CreateDirectory(Path.GetDirectoryName(current)!);
		Directory.CreateDirectory(Path.GetDirectoryName(adapter)!);

		WriteGame(previous, currentRevision: false);
		WriteGame(current, currentRevision: true);
		WriteAdapter(adapter);
		return new FixtureSet(previous, current, adapter, directory);
	}

	private static void WriteGame(string path, bool currentRevision)
	{
		using var assembly = AssemblyDefinition.CreateAssembly(
			new AssemblyNameDefinition(GameAssemblyName, new Version(1, 0, 0, 0)),
			GameAssemblyName + ".dll",
			ModuleKind.Dll);
		var module = assembly.MainModule;

		var target = AddType(module, "FixtureTarget");
		AddMethod(module, target, "Hooked", module.TypeSystem.Void);
		if (currentRevision)
		{
			// The unconstrained [HarmonyPatch] target gains an overload: Harmony would
			// bind to an arbitrary one, which is the ambiguity the tool must classify.
			AddMethod(module, target, "Hooked", module.TypeSystem.Void, ("amount", module.TypeSystem.Int32));
		}
		AddMethod(module, target, "Carries", module.TypeSystem.Void, (currentRevision ? "count" : "amount", module.TypeSystem.Int32));
		AddMethod(module, target, "Plain", module.TypeSystem.Void);
		AddMethod(module, target, "Shapes", module.TypeSystem.Void, ("names", GenericListOfString(module)), ("values", new ArrayType(module.TypeSystem.Int32)));
		AddMethod(module, target, currentRevision ? "RenamedNow" : "Renamed", module.TypeSystem.Void, ("value", module.TypeSystem.Int32));
		AddMethod(module, target, "Retarget", currentRevision ? module.TypeSystem.Single : module.TypeSystem.Int32);
		target.Fields.Add(new FieldDefinition("Counter", FieldAttributes.Public, currentRevision ? module.TypeSystem.Single : module.TypeSystem.Int32));
		target.Fields.Add(new FieldDefinition("Visible", currentRevision ? FieldAttributes.Private : FieldAttributes.Public, module.TypeSystem.Int32));
		target.Fields.Add(new FieldDefinition("Stable", FieldAttributes.Public, module.TypeSystem.Int32));

		var nested = AddType(module, "Inner", target);
		AddMethod(module, nested, "Nested", module.TypeSystem.Void);

		var other = AddType(module, "FixtureOther");
		if (currentRevision)
		{
			AddMethod(module, other, "Changed", module.TypeSystem.Void, ("extra", module.TypeSystem.Int32));
		}
		else
		{
			AddMethod(module, other, "Changed", module.TypeSystem.Void);
		}

		AddEnum(module, "FixtureMode", currentRevision ? 2 : 1);

		var renamedTypeName = currentRevision ? "FixtureRenamedTypeNow" : "FixtureRenamedType";
		var renamedType = AddType(module, renamedTypeName);
		AddMethod(module, renamedType, "Kept", module.TypeSystem.Void);

		if (currentRevision)
		{
			AddType(module, "FixtureAdded");
		}

		assembly.Write(path);
	}

	private static void WriteAdapter(string path)
	{
		using var assembly = AssemblyDefinition.CreateAssembly(
			new AssemblyNameDefinition(AdapterAssemblyName, new Version(1, 0, 0, 0)),
			AdapterAssemblyName + ".dll",
			ModuleKind.Dll);
		var module = assembly.MainModule;
		var game = new AssemblyNameReference(GameAssemblyName, new Version(1, 0, 0, 0));
		module.AssemblyReferences.Add(game);

		AddPatch(module, game, "HookedPatch", "Hooked", null, []);
		AddPatch(module, game, "CarriesPatch", "Carries", "System.Int32", [("amount", module.TypeSystem.Int32)]);
		AddPatch(module, game, "PlainPatch", "Plain", null, []);

		assembly.Write(path);
	}

	/// <summary>One patch class: a [HarmonyPatch(target, method[, argumentTypes])] attribute plus a static Postfix whose parameter names are what Harmony binds by name.</summary>
	private static void AddPatch(
		ModuleDefinition module,
		AssemblyNameReference game,
		string patchClassName,
		string methodName,
		string? argumentType,
		(string Name, TypeReference Type)[] postfixParameters)
	{
		var patchClass = new TypeDefinition("ContractFixture", patchClassName, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object);
		module.Types.Add(patchClass);

		var targetType = new TypeReference("ContractFixture", "FixtureTarget", module, null) { Scope = game };
		var systemType = new TypeReference("System", "Type", module, null) { Scope = module.TypeSystem.CoreLibrary };
		var harmony = new AssemblyNameReference("0Harmony", new Version(2, 9, 0, 0));
		module.AssemblyReferences.Add(harmony);
		var attributeType = new TypeReference("HarmonyLib", "HarmonyPatch", module, null) { Scope = harmony };

		var constructor = new MethodReference(".ctor", module.TypeSystem.Void, attributeType) { HasThis = true };
		constructor.Parameters.Add(new ParameterDefinition(systemType));
		constructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
		var attribute = new CustomAttribute(constructor);
		attribute.ConstructorArguments.Add(new CustomAttributeArgument(systemType, targetType));
		attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, methodName));
		if (argumentType is not null)
		{
			var argumentTypes = new ArrayType(systemType);
			constructor.Parameters.Add(new ParameterDefinition(argumentTypes));
			attribute.ConstructorArguments.Add(new CustomAttributeArgument(
				argumentTypes,
				new[] { new CustomAttributeArgument(systemType, module.TypeSystem.Int32) }));
		}

		patchClass.CustomAttributes.Add(attribute);

		var postfix = new MethodDefinition("Postfix", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
		foreach (var (name, type) in postfixParameters)
		{
			postfix.Parameters.Add(new ParameterDefinition(name, ParameterAttributes.None, type));
		}

		postfix.Body = new MethodBody(postfix);
		postfix.Body.GetILProcessor().Emit(OpCodes.Ret);
		patchClass.Methods.Add(postfix);
	}

	private static TypeDefinition AddType(ModuleDefinition module, string name, TypeDefinition? declaring = null)
	{
		var type = new TypeDefinition(
			"ContractFixture",
			name,
			declaring is null ? TypeAttributes.Public | TypeAttributes.Class : TypeAttributes.NestedPublic | TypeAttributes.Class,
			module.TypeSystem.Object);
		if (declaring is null)
		{
			module.Types.Add(type);
		}
		else
		{
			declaring.NestedTypes.Add(type);
		}

		return type;
	}

	private static void AddEnum(ModuleDefinition module, string name, int secondValue)
	{
		var enumType = new TypeDefinition(
			"ContractFixture",
			name,
			TypeAttributes.Public | TypeAttributes.Sealed,
			new TypeReference("System", "Enum", module, null) { Scope = module.TypeSystem.CoreLibrary });
		enumType.Fields.Add(new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, module.TypeSystem.Int32));
		enumType.Fields.Add(new FieldDefinition("First", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, enumType) { Constant = 0 });
		enumType.Fields.Add(new FieldDefinition("Second", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, enumType) { Constant = secondValue });
		module.Types.Add(enumType);
	}

	private static MethodDefinition AddMethod(
		ModuleDefinition module,
		TypeDefinition type,
		string name,
		TypeReference returns,
		params (string Name, TypeReference Type)[] parameters)
	{
		var method = new MethodDefinition(name, MethodAttributes.Public | MethodAttributes.Static, returns);
		foreach (var (parameterName, parameterType) in parameters)
		{
			method.Parameters.Add(new ParameterDefinition(parameterName, ParameterAttributes.None, parameterType));
		}

		method.Body = new MethodBody(method);
		method.Body.GetILProcessor().Emit(OpCodes.Ret);
		type.Methods.Add(method);
		return method;
	}

	private static TypeReference GenericListOfString(ModuleDefinition module)
	{
		var definition = new TypeReference("System.Collections.Generic", "List`1", module, null) { Scope = module.TypeSystem.CoreLibrary };
		var instance = new GenericInstanceType(definition);
		instance.GenericArguments.Add(module.TypeSystem.String);
		return instance;
	}
}
