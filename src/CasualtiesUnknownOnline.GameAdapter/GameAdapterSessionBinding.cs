using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Session wiring for the Game Adapter: binds/unbinds every deep sync domain
/// to the runtime session and applies the adapter-side session-ended resets.
/// Previously this lived in the GameAdapter coordinator partials; as a real
/// top-level class it keeps the facade free of the event/subscription surface.
/// </summary>
internal sealed class GameAdapterSessionBinding(GameAdapterDomains domains, PlayerInteractionApply playerInteraction, PlayerPushApply pushApply)
{
	public void Bind()
	{
		domains.CharacterDataSync.BindToSession();
		domains.Renderer.BindToSession();
		domains.ItemApplication.BindToSession();
		domains.ItemReconcile.BindToSession();
		domains.ItemWorldSync.BindToSession();
		domains.ItemPositionFollow.BindToSession();
		domains.WorldEventSync.BindToSession();
		domains.EntityEventSync.BindToSession();
		domains.DynamiteExplosionSync.BindToSession();
		domains.EntitySpawnSync.BindToSession();
		domains.GeyserStateSync.BindToSession();
		domains.RadiationLineSync.BindToSession();
		domains.FluidSync.BindToSession();
		domains.TradeSync.BindToSession();
		domains.TraderSwingSync.BindToSession();
		domains.TraderRecruit.BindToSession();
		domains.Respawn.BindToSession();
		domains.SpeechSync.BindToSession();
		domains.RecipeUnlockApply.BindToSession();
		domains.EnemySync.BindToSession();
		domains.EnemyProximity.BindToSession();
		domains.CharacterSoundSync.BindToSession();
		domains.CharacterAttackAnimSync.BindToSession();
		domains.CharacterLandingVisualSync.BindToSession();
		domains.CharacterRagdollSync.BindToSession();
		domains.WorldBloodSync.BindToSession();
		domains.TutorialClawSync.BindToSession();
		domains.WorldTimeSync.BindToSession();
		domains.Run.BindToSession();
		domains.Session.SessionEnded += OnSessionEnded;
		domains.GenItemApplication.BindToSession();
		domains.TrapLayoutApplication.BindToSession();
		domains.LayerModifierSync.BindToSession();
		domains.Items.ItemCarriedSyncReceived += OnItemCarriedSync; // the owner's clone re-renders the moment a carried fact changes
		domains.Items.ItemDropped += OnCarriedItemDropped; // a carried item leaving into the world leaves the fact table (recursive)
		domains.Items.ItemIdWatermarkReceived += OnItemIdWatermark; // the host granted the id counter — resume from watermark + 1
		domains.Items.CarriedInventoryReceived += OnCarriedInventory; // a guest's starting supplies with self-assigned ids — seed the fact table (clone render + divergence baseline)
		domains.PlayerInteraction.TransferReceived += playerInteraction.OnPlayerInventoryTransfer; // cross-player take: apply the local body mutation and re-report
		domains.PlayerInteraction.CarryStateChanged += playerInteraction.OnCarryStateChanged; // cross-player carry: set/clear the local carried-body driver
		domains.PlayerInteraction.HealReceived += playerInteraction.OnPlayerHealReceived; // cross-player heal: consume the local item and/or apply the target's post-heal state
		domains.PlayerInteraction.UseReceived += playerInteraction.OnPlayerItemUseReceived; // cross-player consumable use: consume/update the user's item and/or apply the target's post-use state
		domains.PlayerInteraction.PushReceived += pushApply.Apply; // cross-player push: apply local target ragdoll/pusher cost and play the push sound
	}

	public void Unbind()
	{
		domains.CharacterDataSync.Unbind();
		domains.Renderer.Unbind();
		domains.ItemApplication.Unbind();
		domains.ItemReconcile.Unbind();
		domains.ItemWorldSync.Unbind();
		domains.ItemWorldSync.ResetPending(); // session ended — a pending drop cannot resolve anymore
		domains.BlockBreakSync.ResetPending(); // a pending break's drops are gone with the world
		domains.ItemPositionFollow.Unbind();
		domains.WorldEventSync.Unbind();
		domains.EntityEventSync.Unbind();
		domains.DynamiteExplosionSync.Unbind();
		domains.EntitySpawnSync.Unbind();
		domains.GeyserStateSync.Unbind();
		domains.RadiationLineSync.Unbind();
		domains.FluidSync.Unbind();
		domains.TradeSync.Unbind();
		domains.TraderSwingSync.Unbind();
		domains.TraderRecruit.Unbind();
		domains.Respawn.Unbind();
		domains.SpeechSync.Unbind();
		domains.RecipeUnlockApply.Unbind();
		domains.EnemySync.Unbind();
		domains.EnemyProximity.Unbind();
		domains.CharacterSoundSync.Unbind();
		domains.CharacterAttackAnimSync.Unbind();
		domains.CharacterLandingVisualSync.Unbind();
		domains.CharacterRagdollSync.Unbind();
		domains.WorldBloodSync.Unbind();
		domains.TutorialClawSync.Unbind();
		domains.CraftingSync.ResetPending(); // the destroy claims die with the scene
		domains.Run.Unbind();
		domains.Session.SessionEnded -= OnSessionEnded;
		domains.GenItemApplication.Unbind();
		domains.TrapLayoutApplication.Unbind();
		domains.LayerModifierSync.Unbind();
		domains.Items.ItemCarriedSyncReceived -= OnItemCarriedSync;
		domains.Items.ItemDropped -= OnCarriedItemDropped;
		domains.Items.ItemIdWatermarkReceived -= OnItemIdWatermark;
		domains.Items.CarriedInventoryReceived -= OnCarriedInventory;
		domains.PlayerInteraction.TransferReceived -= playerInteraction.OnPlayerInventoryTransfer;
		domains.PlayerInteraction.CarryStateChanged -= playerInteraction.OnCarryStateChanged;
		domains.PlayerInteraction.HealReceived -= playerInteraction.OnPlayerHealReceived;
		domains.PlayerInteraction.UseReceived -= playerInteraction.OnPlayerItemUseReceived;
		domains.PlayerInteraction.PushReceived -= pushApply.Apply;
	}

	private void OnSessionEnded()
	{
		domains.CharacterDataSync.ResetSessionState();
		domains.ItemWorldSync.ResetPending();
		domains.BlockBreakSync.ResetPending();
		domains.CraftingSync.ResetPending();
		domains.TraderRecruit.Reset();
		domains.HeaterCookSync.Reset();
		domains.CharacterRagdollSync.Reset();
		domains.Gate.ResetSessionState();
		domains.Renderer.DestroyAllClones();
	}

	private void OnItemCarriedSync(ulong owner, CharacterItemMsg item, bool slotKnown) =>
		domains.CharacterDataSync.ApplyCarriedSync(owner, item, slotKnown);

	private void OnItemIdWatermark(ulong counter) => domains.ItemIds.SetWatermark(counter);

	private void OnCarriedInventory(ulong owner, IReadOnlyList<CharacterItemMsg> items) =>
		domains.CharacterDataSync.ApplyCarriedInventory(owner, items);

	private void OnCarriedItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos) =>
		domains.CharacterDataSync.RemoveCarriedItem(itemId);
}
