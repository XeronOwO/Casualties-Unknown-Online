using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The per-mod resource-completion stage registry. The console's completion
/// vocabulary is a framework contract, so a mod widens it through this surface
/// instead of the catalog: a registered stage is consulted after the four
/// built-in ranks (exact canonical id, id prefix, bare path prefix, display-name
/// prefix), which makes every registration additive.
///
/// The table belongs to one mod by construction (each mod context owns its own
/// adapter), is process-local, and never crosses the wire — the console
/// completes on the client the player is typing on. Registration is expected in
/// <see cref="ICuoMod.Bind"/>, but a stage registered later is consulted by the
/// next completion query, because the catalog reads the table once per query.
///
/// No <see cref="ModPermission"/> flag gates this surface: a stage can only widen
/// what the local player's own console offers. The table lives as long as the
/// process (like a mod's content registrations), and a completion query ranks the
/// stages that were registered when it started — a stage that registers or
/// unregisters during a query affects the next query, not the running one.
///
/// A mod may hold at most 8 registered stages; every refusal (a null stage, a
/// blank id, an id over 128 characters, a duplicate id, the cap) returns false
/// and is logged by the framework.
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public interface IModResourceCompletion
{
	/// <summary>
	/// Register one completion stage under a mod-chosen id. The id is the
	/// handle for <see cref="TryUnregisterMatchStage"/> and
	/// <see cref="IsMatchStageRegistered"/>; it is not shown to the player.
	/// Returns false (with a framework log) for a null stage, a blank id, an id
	/// over 128 characters, an id this mod already registered, or when this mod
	/// already holds the maximum number of stages.
	/// </summary>
	bool TryRegisterMatchStage(string id, IResourceLocationMatchStage stage);

	/// <summary>
	/// Remove a stage this mod registered. Returns false when this mod has no
	/// stage under that id; another mod's stage is never removable from here.
	/// </summary>
	bool TryUnregisterMatchStage(string id);

	/// <summary>True when this mod has a stage registered under this exact id.</summary>
	bool IsMatchStageRegistered(string id);

	/// <summary>The stage ids this mod has registered (a copy — safe to hold).</summary>
	IReadOnlyCollection<string> MatchStageIds { get; }

	/// <summary>The number of stages this mod has registered.</summary>
	int MatchStageCount { get; }
}
