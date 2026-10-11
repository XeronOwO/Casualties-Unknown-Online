using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One mod's write guard over its own slice of the framework-wide effect registry:
/// the permission gate, the ownership rail, the reachability rail and the duplicate
/// rail are decided here,
/// the functions themselves live in <see cref="ModLiquidEffectStore"/> (their one
/// owner, so the Game Adapter's resolver sees the same set).
///
/// <para>
/// The ownership rail is the load-bearing one. An effect is keyed by a CONTENT id,
/// and that id is the only thing binding a function to the data every peer
/// materializes — so a mod may only author the effect of a liquid IT registered as
/// liquid content. An unowned id would silently author behaviour for content the
/// game or another mod materialized, and authoring over a vanilla liquid is outside
/// this surface entirely (that is a content override, a capability of its own).
/// </para>
/// </summary>
internal sealed class ModLiquidEffectsAdapter(
	ModManifest manifest,
	ModContentStore contentStore,
	ModLiquidEffectStore effectStore,
	ILogger log) : IModLiquidEffects
{
	/// <inheritdoc />
	public bool CanRegister => ModPermissionGate.HasPermission(manifest, ModPermission.AuthorEffects);

	/// <inheritdoc />
	public bool TryRegisterDrink(string liquidId, ModLiquidEffect effect) =>
		TryRegister(liquidId, effect, limbUse: false);

	/// <inheritdoc />
	public bool TryRegisterLimbUse(string liquidId, ModLiquidEffect effect) =>
		TryRegister(liquidId, effect, limbUse: true);

	private bool TryRegister(string liquidId, ModLiquidEffect effect, bool limbUse)
	{
		var half = limbUse ? "limb-use" : "drink";

		if (!ModPermissionGate.Try(log, manifest, ModPermission.AuthorEffects))
		{
			return false;
		}

		if (effect is null)
		{
			log.LogWarning("[Mods] {ModId} tried to register a null {Half} effect for {Id} — refused.",
				manifest.Id, half, liquidId);
			return false;
		}

		if (!ModContentPolicy.IsValidId(liquidId))
		{
			log.LogWarning("[Mods] {ModId} tried to register a {Half} effect for the invalid liquid id {Id} — refused.",
				manifest.Id, half, liquidId);
			return false;
		}

		if (contentStore.Find(manifest.Id, liquidId, ModContentKind.Liquid) is not IModLiquidDefinition definition)
		{
			log.LogWarning(
				"[Mods] {ModId} tried to register a {Half} effect for {Id}, which it has not registered as liquid content — refused. Register the liquid first; only its owner may author its effect.",
				manifest.Id, half, liquidId);
			return false;
		}

		if (!IsReachable(definition, liquidId, half, limbUse))
		{
			return false;
		}

		if (!effectStore.TryAdd(manifest.Id, liquidId, effect, limbUse))
		{
			log.LogWarning("[Mods] {ModId} already registered a {Half} effect for {Id} — the second is refused.",
				manifest.Id, half, liquidId);
			return false;
		}

		log.LogInformation("[Mods] {ModId} registered the {Half} effect of liquid {Id}.",
			manifest.Id, half, liquidId);
		return true;
	}

	/// <summary>
	/// Whether the game's own gate can reach this half of the definition at all. The
	/// DRINK half always can: <c>WaterContainerItem.Drink</c> calls <c>onDrink</c> for
	/// whatever liquid a container holds, behind no flag of the liquid's. The LIMB half
	/// is gated twice over, on the two flags the definition carries —
	/// <c>WaterContainerItem.ApplyToLimb</c> runs <c>onHealthUse</c> behind
	/// <c>liquidType.healthUsable</c> (WaterContainerItem.cs:228-230) and
	/// <c>WaterContainerItem.Inject</c> behind <c>injectable</c> (:254-256), which is
	/// also the pair CUO's own topical and injection appliers check — so a liquid
	/// declaring NEITHER flag reaches no call site at all, and an effect registered for
	/// it would be dead code with a success line.
	/// </summary>
	private bool IsReachable(IModLiquidDefinition definition, string liquidId, string half, bool limbUse)
	{
		if (!limbUse || definition.HealthUsable || definition.Injectable)
		{
			return true;
		}

		log.LogWarning(
			"[Mods] {ModId} registered a {Half} effect for {Id}, which declares neither HealthUsable nor Injectable — refused: the game runs onHealthUse behind those two flags, so nothing could ever run it.",
			manifest.Id, half, liquidId);
		return false;
	}
}
