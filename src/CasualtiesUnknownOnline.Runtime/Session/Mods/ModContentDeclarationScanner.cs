using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The front-end that turns a <see cref="ModContentAttribute"/> declaration into
/// a registration: at discovery it reads the mod's assembly, decides which
/// declarations that mod OWNS, instantiates each of them and hands it to
/// <see cref="IModContent.TryRegister"/> — the code path's own rail, so there is
/// one registry, one permission check and one binder however content arrives.
///
/// Reach, stated rather than implied: only assemblies that REFERENCE the mod
/// surface are read (a class cannot apply <see cref="ModContentAttribute"/>
/// without that reference), and the per-assembly outcome is computed once and
/// cached, so the assembly-level refusals are logged once per assembly instead
/// of once per mod of a multi-mod assembly. What the attribute means for the mod
/// author — kind, ownership, instantiation — is documented there.
///
/// Isolation: the per-declaration work is inside its own guard, so one broken
/// declaration never blocks a sibling, and the census never stops the mod scan.
/// </summary>
internal sealed class ModContentDeclarationScanner(ILogger log)
{
	/// <summary>The assembly a declaration must reference to exist at all.</summary>
	private static readonly string ModSurfaceAssembly = typeof(IModContentDefinition).Assembly.GetName().Name ?? "";

	private readonly ILogger _log = log;

	/// <summary>One ownership plan per assembly, so a multi-mod assembly's census (and its refusals) runs once.</summary>
	private readonly Dictionary<Assembly, Dictionary<Type, List<Type>>> _plans = [];

	/// <summary>
	/// The whole-domain census: reads every assembly that can carry a declaration
	/// and reports the ones nothing can own. It runs once, before the load loop —
	/// without it an assembly that declares content but no mod stays silent,
	/// because no mod of that assembly ever asks for its plan.
	/// </summary>
	public void Census(IEnumerable<Assembly> assemblies)
	{
		foreach (var assembly in assemblies)
		{
			try
			{
				if (CanDeclareContent(assembly))
				{
					_ = DeclarationsOf(assembly);
				}
			}
			catch (Exception e)
			{
				// Best-effort discovery over assemblies this framework does not own:
				// one it cannot read must not take the mod scan down with it.
				_log.LogWarning(e, "[ModContent] {Assembly} could not be read for content declarations — skipped.", NameOf(assembly));
			}
		}
	}

	/// <summary>
	/// Registers every declaration this mod owns, in the assembly's own type
	/// order, and returns how many were registered. Called BEFORE
	/// <see cref="ICuoMod.Bind"/>, so a mod's own bind already sees the content it
	/// declared next to itself.
	/// </summary>
	public int Register(Type modType, IModContent content)
	{
		var plan = DeclarationsOf(modType.Assembly);
		if (!plan.TryGetValue(modType, out var owned))
		{
			return 0;
		}

		var registered = 0;
		foreach (var declaration in owned)
		{
			if (RegisterOne(declaration, content))
			{
				registered++;
			}
		}

		return registered;
	}

	/// <summary>
	/// The ownership rule over one assembly's types — the whole of it, so a test
	/// drives every branch without a live assembly and the census stays the only
	/// caller in production. A declaration NESTED inside a mod class is that
	/// mod's; a declaration that is not nested in a mod belongs to the mod the
	/// assembly declares, which therefore has to be the only one; anything else
	/// has no owner and is refused BY NAME rather than guessed at, because a
	/// declaration registered under the wrong mod would carry the wrong namespace,
	/// the wrong owner and the wrong permission check.
	///
	/// The assembly-level refusals are reported here, which is why the result is
	/// cached: a five-mod assembly would otherwise repeat them five times. A
	/// declaration whose owner discovery will never load is refused here too — the
	/// one silent shape the registry does not name itself.
	/// </summary>
	internal static Dictionary<Type, List<Type>> Plan(Assembly assembly, IReadOnlyList<Type> types, ILogger log)
	{
		var mods = types.Where(IsACuoMod).ToList();
		var declarations = types.Where(CarriesTheAttribute).ToList();
		var byOwner = new Dictionary<Type, List<Type>>();
		var stranded = new Dictionary<Type, int>();

		if (mods.Count == 0)
		{
			if (declarations.Count > 0)
			{
				log.LogWarning(
					"[ModContent] {Assembly} declares {Count} [ModContent] class(es) but no [CuoMod] mod — nothing registers them; declare a mod in this assembly, or register them through IModContext.Content.",
					NameOf(assembly), declarations.Count);
			}

			return byOwner;
		}

		foreach (var declaration in declarations)
		{
			if (IsACuoMod(declaration))
			{
				log.LogWarning(
					"[ModContent] {Type} carries [CuoMod] and [ModContent] on the same class — the content half is refused; a mod class is not a declaration, so declare the content in a class of its own.",
					declaration.FullName);
				continue;
			}

			var owner = OwnerOf(declaration, mods);
			if (owner is null)
			{
				log.LogWarning(
					"[ModContent] {Type} is not nested in a mod and {Assembly} declares {Count} mods — refused, because no single mod owns it; nest it inside the mod class that owns it, or register it through IModContext.Content.",
					declaration.FullName, NameOf(assembly), mods.Count);
				continue;
			}

			// An owner discovery will never load cannot register anything, and it is the
			// one case no other line reports: a mod class that fails a MANIFEST rule is
			// skipped by the registry by name, while one that is not public, is abstract
			// or cannot be constructed is not even a candidate there.
			if (!IsDiscoverableMod(owner))
			{
				stranded[owner] = stranded.TryGetValue(owner, out var counted) ? counted + 1 : 1;
				continue;
			}

			if (!byOwner.TryGetValue(owner, out var owned))
			{
				owned = [];
				byOwner.Add(owner, owned);
			}

			owned.Add(declaration);
		}

		foreach (var pair in stranded)
		{
			log.LogWarning(
				"[ModContent] {Count} declaration(s) belong to {Owner}, which discovery never loads — nothing registers them; a mod class must be public, concrete and constructible with no arguments.",
				pair.Value, pair.Key.FullName);
		}

		return byOwner;
	}

	/// <summary>The mod a declaration is owned by: the nearest enclosing mod class, else the assembly's only mod, else nobody.</summary>
	private static Type? OwnerOf(Type declaration, IReadOnlyList<Type> mods)
	{
		for (var enclosing = declaration.DeclaringType; enclosing is not null; enclosing = enclosing.DeclaringType)
		{
			if (mods.Contains(enclosing))
			{
				return enclosing;
			}
		}

		return mods.Count == 1 ? mods[0] : null;
	}

	/// <summary>One declaration: the kind it declares is the contract it implements, and its members are read once so a computed one is executed here rather than inside a provider.</summary>
	private bool RegisterOne(Type declaration, IModContent content)
	{
		if (!declaration.IsClass)
		{
			Refuse(declaration, "is not a class");
			return false;
		}

		if (!declaration.IsPublic && !declaration.IsNestedPublic)
		{
			Refuse(declaration, "is not public, and the framework instantiates a declaration at discovery");
			return false;
		}

		if (declaration.IsAbstract)
		{
			Refuse(declaration, "is abstract, and the framework instantiates a declaration at discovery");
			return false;
		}

		if (declaration.ContainsGenericParameters)
		{
			Refuse(declaration, "is an open generic type");
			return false;
		}

		if (declaration.GetConstructor(Type.EmptyTypes) is null)
		{
			Refuse(declaration, "has no public parameterless constructor");
			return false;
		}

		var contracts = declaration.GetInterfaces().Where(IsAKindContract).ToList();
		if (contracts.Count == 0)
		{
			Refuse(declaration, "implements no kind contract; the kind is the contract a declaration implements (IModItemDefinition and its eight siblings)");
			return false;
		}

		if (contracts.Count > 1)
		{
			Refuse(declaration, $"implements {contracts.Count} kind contracts ({string.Join(", ", contracts.Select(contract => contract.Name))}), and a declaration implements exactly one");
			return false;
		}

		var contract = contracts[0];
		ModContentContract.TryGetKind(contract, out var expectedKind);

		if (!TryCreate(declaration, out var definition))
		{
			return false;
		}

		if (!TryReadEveryMember(declaration, contract, definition))
		{
			return false;
		}

		if (!string.Equals(definition.Kind, expectedKind, StringComparison.Ordinal))
		{
			Refuse(declaration, $"reports kind '{definition.Kind}' but implements {contract.Name}, which fixes kind '{expectedKind}'");
			return false;
		}

		try
		{
			return content.TryRegister(definition);
		}
		catch (Exception e)
		{
			_log.LogWarning(e, "[ModContent] {Type} threw while it was registered — refused; the other declarations still bind.", declaration.FullName);
			return false;
		}
	}

	/// <summary>
	/// Instantiates the declaration inside its own guard. A constructor — or the
	/// static initializer the first touch of the type runs — that throws refuses THAT
	/// declaration by name, exactly like a member getter does; without this guard the
	/// exception would leave <see cref="Register"/>, land in the per-mod discovery
	/// catch and take the whole mod down with it, <c>Bind</c> included.
	/// </summary>
	private bool TryCreate(Type declaration, out IModContentDefinition definition)
	{
		try
		{
			if (Activator.CreateInstance(declaration) is IModContentDefinition instance)
			{
				definition = instance;
				return true;
			}
		}
		catch (Exception e)
		{
			_log.LogWarning(e, "[ModContent] {Type} threw while it was constructed — refused; the other declarations still bind.", declaration.FullName);
			definition = null!;
			return false;
		}

		// Unreachable while the contract check above stands: a class that implements a
		// contract extending IModContentDefinition IS one.
		Refuse(declaration, "implements the kind contract but produced no IModContentDefinition");
		definition = null!;
		return false;
	}

	/// <summary>
	/// Reads every member of the contract, so a member that COMPUTES its value runs
	/// here: a getter that throws is refused by name with its siblings untouched,
	/// instead of throwing inside a provider later, where nothing per-declaration
	/// can catch it.
	/// </summary>
	private bool TryReadEveryMember(Type declaration, Type contract, IModContentDefinition definition)
	{
		foreach (var member in Members(contract))
		{
			try
			{
				_ = member.GetValue(definition);
			}
			catch (Exception e)
			{
				_log.LogWarning(e, "[ModContent] {Type}.{Member} threw while the declaration was read — refused; the other declarations still bind.",
					declaration.FullName, member.Name);
				return false;
			}
		}

		return true;
	}

	/// <summary>The contract's own members plus the three address members, which an interface does not inherit into its own reflection view.</summary>
	private static IEnumerable<PropertyInfo> Members(Type contract) =>
		contract.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Concat(typeof(IModContentDefinition).GetProperties(BindingFlags.Public | BindingFlags.Instance))
			.GroupBy(member => member.Name, StringComparer.Ordinal)
			.Select(group => group.First());

	private void Refuse(Type declaration, string reason) =>
		_log.LogWarning("[ModContent] {Type} {Reason} — refused.", declaration.FullName, reason);

	private Dictionary<Type, List<Type>> DeclarationsOf(Assembly assembly)
	{
		if (_plans.TryGetValue(assembly, out var plan))
		{
			return plan;
		}

		plan = Plan(assembly, AssemblyTypes.Loadable(assembly), _log);
		_plans.Add(assembly, plan);
		return plan;
	}

	private static bool CanDeclareContent(Assembly assembly) =>
		string.Equals(NameOf(assembly), ModSurfaceAssembly, StringComparison.Ordinal)
		|| assembly.GetReferencedAssemblies().Any(reference => string.Equals(reference.Name, ModSurfaceAssembly, StringComparison.Ordinal));

	private static bool CarriesTheAttribute(Type type) => type.IsDefined(typeof(ModContentAttribute), inherit: false);

	private static bool IsACuoMod(Type type) =>
		typeof(ICuoMod).IsAssignableFrom(type) && type.IsDefined(typeof(CuoModAttribute), inherit: false);

	/// <summary>
	/// True when mod discovery is even allowed to CONSIDER this type — the structural
	/// facts <see cref="ModRegistry"/> filters on before it validates a manifest. It is
	/// deliberately that subset and no more: a `[CuoMod]` class that fails a manifest
	/// rule (a bad version, a namespace clash, a missing dependency) is skipped by the
	/// registry WITH a line naming it, while one that is not public, is abstract or
	/// cannot be constructed is not a candidate there at all and would be silent.
	/// </summary>
	private static bool IsDiscoverableMod(Type type) =>
		IsACuoMod(type)
		&& type.IsClass
		&& !type.IsAbstract
		&& (type.IsPublic || type.IsNestedPublic)
		&& type.GetConstructor(Type.EmptyTypes) is not null;

	private static bool IsAKindContract(Type type) => ModContentContract.TryGetKind(type, out _);

	private static string NameOf(Assembly assembly) => assembly.GetName().Name ?? assembly.FullName ?? "";
}
