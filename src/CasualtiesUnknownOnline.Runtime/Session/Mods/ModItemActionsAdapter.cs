using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One mod's write guard over its own slice of the framework-wide item action
/// registry: the permission gate, the ownership rail, the reachability rail and the
/// duplicate rail are decided here, the functions themselves live in
/// <see cref="ModItemEffectStore"/> (their one owner, so the Game Adapter's resolver
/// sees the same set).
///
/// <para>
/// The ownership rail is the load-bearing one. An action is keyed by a CONTENT id,
/// and that id is the only thing binding a function to the data every peer
/// materializes — so a mod may only author the action of an item IT registered as
/// item content. An unowned id would silently author behaviour for content the game
/// or another mod materialized, and authoring over a vanilla item is outside this
/// surface entirely (that is a content override, a capability of its own).
/// </para>
///
/// <para>
/// The reachability rail is the item side's own, and it is a refusal rather than a
/// warning because there is nothing to reach: both native calls sit behind a flag on
/// the definition — <c>Body.UseItem</c> runs <c>useAction</c> behind
/// <c>ItemInfo.usable</c> / <c>usableWithLMB</c>, and the wound view runs
/// <c>useLimbAction</c> behind <c>ItemInfo.usableOnLimb</c> — so an action
/// registered without its flag is code no gesture can ever run. The use half is
/// refused for a definition that already carries a use action of its own (a
/// <see cref="IModItemDefinition.Tool"/> or <see cref="IModItemDefinition.Gun"/>
/// behaviour) for the same reason the entries of a gesture stay isolated: one item,
/// one use action, decided by the definition's own data rather than by which
/// registration happened to run last.
/// </para>
/// </summary>
internal sealed class ModItemActionsAdapter(
	ModManifest manifest,
	ModContentStore contentStore,
	ModItemEffectStore effectStore,
	ILogger log) : IModItemActions
{
	/// <inheritdoc />
	public bool CanRegister => ModPermissionGate.HasPermission(manifest, ModPermission.AuthorEffects);

	/// <inheritdoc />
	public bool TryRegisterUse(string itemId, ModItemAction action) =>
		TryRegister(itemId, action, limbUse: false);

	/// <inheritdoc />
	public bool TryRegisterLimbUse(string itemId, ModItemAction action) =>
		TryRegister(itemId, action, limbUse: true);

	private bool TryRegister(string itemId, ModItemAction action, bool limbUse)
	{
		var half = limbUse ? "limb-use" : "use";

		if (!ModPermissionGate.Try(log, manifest, ModPermission.AuthorEffects))
		{
			return false;
		}

		if (action is null)
		{
			log.LogWarning("[Mods] {ModId} tried to register a null {Half} action for {Id} — refused.",
				manifest.Id, half, itemId);
			return false;
		}

		if (!ModContentPolicy.IsValidId(itemId))
		{
			log.LogWarning("[Mods] {ModId} tried to register a {Half} action for the invalid item id {Id} — refused.",
				manifest.Id, half, itemId);
			return false;
		}

		if (contentStore.Find(manifest.Id, itemId, ModContentKind.Item) is not IModItemDefinition definition)
		{
			log.LogWarning(
				"[Mods] {ModId} tried to register a {Half} action for {Id}, which it has not registered as item content — refused. Register the item first; only its owner may author its action.",
				manifest.Id, half, itemId);
			return false;
		}

		if (!IsReachable(definition, itemId, half, limbUse))
		{
			return false;
		}

		if (!effectStore.TryAdd(manifest.Id, itemId, action, limbUse))
		{
			log.LogWarning("[Mods] {ModId} already registered a {Half} action for {Id} — the second is refused.",
				manifest.Id, half, itemId);
			return false;
		}

		log.LogInformation("[Mods] {ModId} registered the {Half} action of item {Id}.",
			manifest.Id, half, itemId);
		return true;
	}

	/// <summary>
	/// Whether the game's own gate can reach this half of the definition at all — the
	/// two questions the class documentation states, each answered from the
	/// definition's own data.
	/// </summary>
	private bool IsReachable(IModItemDefinition definition, string itemId, string half, bool limbUse)
	{
		if (limbUse)
		{
			if (definition.UsableOnLimb)
			{
				return true;
			}

			log.LogWarning(
				"[Mods] {ModId} registered a {Half} action for {Id}, which does not declare UsableOnLimb — refused: the game's own wound view gates the limb action on that flag, so nothing could ever run it.",
				manifest.Id, half, itemId);
			return false;
		}

		if (definition.Tool is not null || definition.Gun is not null)
		{
			log.LogWarning(
				"[Mods] {ModId} registered a {Half} action for {Id}, whose definition already carries a Tool or Gun use action — refused: one item has one use action, and the definition's own behaviour is the one that stands.",
				manifest.Id, half, itemId);
			return false;
		}

		// `usable` is the NECESSARY flag, not one of two. The game invokes `useAction`
		// at exactly two sites — `Body.UseItem` behind `if (item.Stats.usable)`
		// (Body.cs:2477) and `Body.UseItemInHand` behind
		// `conscious && HoldingItem && usable && usableWithLMB` (Body.cs:2452) — so
		// `usable` is required at both and `usableWithLMB` alone reaches neither. The
		// game's own data agrees: all 38 of its `usableWithLMB = true` items set
		// `usable = true` as well, so the second flag only ever ADDS the left-mouse
		// entry point.
		if (definition.Usable)
		{
			return true;
		}

		log.LogWarning(
			"[Mods] {ModId} registered a {Half} action for {Id}, which does not declare Usable — refused: the game invokes useAction behind that flag at both of its call sites, and UsableWithLmb only adds the left-mouse entry point, so nothing could ever run it.",
			manifest.Id, half, itemId);
		return false;
	}
}
