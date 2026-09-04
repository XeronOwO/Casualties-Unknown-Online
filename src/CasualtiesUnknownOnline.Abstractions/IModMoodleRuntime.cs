using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The per-mod local moodle-presentation resolver surface. A mod can register
/// one resolver per runtime status id; the GameAdapter's local vanilla
/// moodle-row projection calls it for each active body/limb presence and uses
/// the returned static moodle id as the row's moodle (falling back to the
/// static status/moodle routing when the resolver returns null or is absent).
///
/// This is the CUO-safe replacement for CUCoreLib's
/// <c>MoodleRegistry.RegisterBody/RegisterLimb</c> callbacks: instead of
/// receiving a live <c>Body</c>/<c>Limb</c> game object, the mod receives a
/// plain <see cref="ModStatusMoodleRequest"/> (opaque payload + stable limb
/// slot/name). It is local-only presentation and adds no wire surface.
/// </summary>
public interface IModMoodleRuntime
{
	/// <summary>
	/// Register one moodle resolver for a status id. Returns false for a null
	/// resolver, an invalid/over-long status id, a duplicate registration, or a
	/// per-mod resolver cap. The resolver may be registered before or after the
	/// status is declared through <see cref="IModStatusRuntime"/>.
	/// </summary>
	bool TryRegisterResolver(string statusId, Func<ModStatusMoodleRequest, string?> resolver);

	/// <summary>Remove a previously registered moodle resolver. Returns false when no resolver exists for the id.</summary>
	bool TryUnregisterResolver(string statusId);

	/// <summary>True when this mod has a moodle resolver registered for the status id.</summary>
	bool HasResolver(string statusId);

	/// <summary>All status ids that currently have a moodle resolver (copy — safe to hold).</summary>
	IReadOnlyCollection<string> ResolverStatusIds { get; }

	/// <summary>The number of moodle resolvers registered by this mod.</summary>
	int ResolverCount { get; }
}
