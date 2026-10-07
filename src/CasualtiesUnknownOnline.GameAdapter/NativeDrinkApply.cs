using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Applies a host-authoritative drained dose to the LOCAL body through the game's
/// own drink path. Part B of <c>mod-cross-player-native-semantics</c>: the host
/// decides that the operation is allowed and owns the resource, and the AFFECTED
/// side runs the effect, so the per-ml formula comes from the game's own
/// <c>Liquids.Registry</c> entry instead of a CUO table.
/// <para>
/// The body is <c>WaterContainerItem.Drink</c>'s per-stack loop
/// (WaterContainerItem.cs:198-215) minus its <c>CalculateDrain</c>/<c>Drain</c>
/// pair and minus the trailing clip: the drain already happened on the host's
/// authoritative item, and the amounts here are its result. The liquid's own
/// <c>onDrink</c> delegate is verbatim, which is where the timed
/// <c>CoUtils.DoTimedOp</c> bodies (antirad's radiation tick, naltrexone's
/// antagonist ramp), the component doses (sleeping pills, antidepressants) and
/// the per-call random rolls start — on the body they land on, exactly as the
/// native drink makes them.
/// </para>
/// <para>
/// The call runs inside the item-use sound scope on purpose: several liquids play
/// their own clip from inside <c>onDrink</c> (braingrow, antidepressants,
/// antibiotics, antirad and sleeping pills play <c>"pills"</c>, streptokinase
/// <c>"drink"</c>), and <c>CharacterSoundPolicy</c> classifies exactly those under
/// <c>CharacterItemUse</c> as the consume family — so the patient's client is the
/// side that owns them, exactly as it would be for a local drink. Only
/// <c>Drink</c>'s OWN trailing <c>Sound.Play(sound, …)</c> is not replayed: the
/// clip is a delegate argument CUO does not carry, and no cross-player drink clip
/// played before this chain was migrated either.
/// </para>
/// <para>
/// One deliberate deviation, the same one <see cref="NativeTopicalApply"/> and
/// <see cref="NativeInjectionApply"/> make: native <c>Drink</c> calls
/// <c>onDrink</c> without a null check, so a liquid that carries no delegate
/// throws there. A mod liquid can be registered while the mod API has no way to
/// give it an <c>onDrink</c> delegate, so this path logs and skips instead of
/// taking the patient's client down with it.
/// </para>
/// </summary>
internal static class NativeDrinkApply
{
	/// <summary>
	/// Apply one drained dose to the local body; returns how many stacks the
	/// game's registry answered for, so the caller knows whether the body actually
	/// moved.
	/// </summary>
	internal static int Apply(Body body, IReadOnlyList<LiquidStackMsg> dose, ILogger log)
	{
		if (dose.Count == 0)
		{
			return 0;
		}

		var handled = 0;
		using var scope = CallContext.Enter(CallContext.Origin.CharacterItemUse);
		foreach (var stack in dose)
		{
			if (stack.Amount <= 0f)
			{
				continue;
			}

			if (!Liquids.Registry.TryGetValue(stack.LiquidId, out var liquid))
			{
				log.LogWarning("[ItemUse] drink dose skipped: liquid {Liquid} is not in the game's own registry.", stack.LiquidId);
				continue;
			}

			handled++;
			if (liquid.onDrink is null)
			{
				log.LogWarning("[ItemUse] drink dose skipped: liquid {Liquid} carries no onDrink delegate — native Drink would throw here.", stack.LiquidId);
				continue;
			}

			liquid.onDrink(stack.Amount, body);
		}

		return handled;
	}
}
