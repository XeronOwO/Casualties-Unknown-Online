namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// A mod-authored item action: the function a mod registers for an item it
/// registered as content. It stands where the game's own <c>ItemInfo.useAction</c>
/// / <c>ItemInfo.useLimbAction</c> delegate stands, and it runs on the client whose
/// body the action was performed on — the same client the game's own delegate runs
/// on, so a cross-player limb use carries it with no new message.
///
/// <para>
/// The callback is invoked from inside the game's own call site. It must not
/// throw: the framework catches and logs, and the action that triggered it then
/// completes without the effect — an exception escaping into the game's frame is
/// not an option a mod gets. It runs on the main thread, once per native call, with
/// the item the native call handed over and, on the limb path, the limb it named.
/// </para>
///
/// <para>
/// The same delegate serves both halves; which one ran is what
/// <see cref="IModItemActionContext.Limb"/> answers — null on the use path, the
/// treated limb on the limb path — exactly as the game's own two delegates differ.
/// </para>
/// </summary>
/// <param name="context">What the action landed on: the item, the body, and the limb of the limb path.</param>
[ApiStability(ApiStabilityLevel.Experimental)]
public delegate void ModItemAction(IModItemActionContext context);
