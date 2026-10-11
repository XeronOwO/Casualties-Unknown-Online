using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One mod's write guard over its own slice of the framework-wide effect registry:
/// the permission gate, the ownership rail and the duplicate rail are decided here,
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

		if (!contentStore.IsRegisteredAs(manifest.Id, liquidId, ModContentKind.Liquid))
		{
			log.LogWarning(
				"[Mods] {ModId} tried to register a {Half} effect for {Id}, which it has not registered as liquid content — refused. Register the liquid first; only its owner may author its effect.",
				manifest.Id, half, liquidId);
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
}
