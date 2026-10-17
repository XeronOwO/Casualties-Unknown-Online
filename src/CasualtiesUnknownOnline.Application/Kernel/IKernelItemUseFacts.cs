namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The game's own item data, as the ADMISSION seam needs one question answered:
/// may the item this definition names be used natively at all?
///
/// <para>
/// This is the data anchor the native-use report is bounded by. Both native call
/// sites REQUIRE the item's own <c>ItemInfo.usable</c>, though not the same set of
/// flags: <c>Body.UseItem</c> runs its delegate behind <c>usable</c> alone
/// (<c>Body.cs:2477</c>), and <c>Body.UseItemInHand</c> behind
/// <c>usable &amp;&amp; usableWithLMB</c> plus consciousness (<c>Body.cs:2452</c>). So a
/// use report naming an item whose own data says <c>usable = false</c> is a report
/// no client of this game could have made: neither call site would have run a use
/// action. The stricter <c>usableWithLMB</c> is deliberately NOT part of the
/// question — the report does not say which of the two sites ran, and requiring it
/// would refuse a legitimate radial-menu use of an item the LMB click cannot use.
/// The flag is per DEFINITION and the host reads it from its own registry — the
/// receiver's fact, never the report's word — which is what makes it a bound rather
/// than a restatement.
/// </para>
///
/// <para>
/// The port exists because the flag lives in the game's item registry and the
/// seam lives in the Application layer, which may reference no game assembly.
/// The answer is deliberately three-valued: an id this host's registry does not
/// carry (a mod the host does not have, or a registry that has not been built
/// yet) is not evidence that the item is unusable, and the family's rule is that
/// a read which FAILED must not refuse on a table it could not read — it admits
/// as before and the gap is named where the read happened.
/// </para>
/// </summary>
public interface IKernelItemUseFacts
{
	/// <summary>
	/// Whether the game's own data for <paramref name="definitionId"/> permits a
	/// native use of the item. <see cref="ItemUsePermission.Unknown"/> when this
	/// build cannot read the registry or the id is not in it; the seam admits that
	/// answer rather than inventing a refusal from a missing fact.
	/// </summary>
	ItemUsePermission NativeUse(string definitionId);
}
