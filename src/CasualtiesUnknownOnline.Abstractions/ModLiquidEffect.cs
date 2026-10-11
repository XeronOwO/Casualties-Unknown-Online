namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// A mod-authored liquid effect: the function a mod registers for a liquid it
/// registered as content. It stands where the game's own <c>LiquidType.onDrink</c>
/// / <c>LiquidType.onHealthUse</c> delegate stands, and it runs on the client
/// that owns the affected body — the same client the game's own delegate runs on,
/// so a cross-player drink carries it with no new message.
///
/// <para>
/// The callback is invoked from inside the game's own call site. It must not
/// throw: the framework catches and logs, and the action that triggered it
/// (the drink, the topical application, the injection) then completes without the
/// effect — an exception escaping into the game's frame is not an option a mod
/// gets. A tick of a timed body started from here is guarded the same way, so a
/// throwing tick costs that tick rather than the whole schedule. It runs on the
/// main thread, once per native call, and may start a timed body through
/// <see cref="IModEffectBody.StartTimedEffect"/> and keep writing from there.
/// </para>
/// </summary>
/// <param name="context">What the effect landed on: the liquid, the amount, the body, and the limb of the limb path.</param>
[ApiStability(ApiStabilityLevel.Experimental)]
public delegate void ModLiquidEffect(IModLiquidEffectContext context);
