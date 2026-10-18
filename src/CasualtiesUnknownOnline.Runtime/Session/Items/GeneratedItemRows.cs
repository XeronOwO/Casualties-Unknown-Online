using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// A GENERATED world container's contents as kernel rows. The generation publish names them
/// (<c>GeneratedItemAuthority</c> allocates their ids before the capture), so the parent's captured
/// <c>Contents</c> carry them and the same container-sync command a member's report maps to creates one
/// contained row each — the parent's data and its children commit as one batch, and the guests' replay
/// kernels learn the same rows. Without the rows a report about a child (a give above all) would name an
/// id the host never judged.
///
/// <para>
/// It is its own type rather than a method on <see cref="ItemService"/> or
/// <see cref="ItemKernelAuthority"/> because both sit at the 600-line architecture gate and this is one
/// responsibility: the generation publish's write, apart from the item domain's.
/// </para>
/// </summary>
internal sealed class GeneratedItemRows(ItemKernelAuthority authority, ILogger log)
{
	private readonly ItemKernelAuthority _authority = authority;
	private readonly ILogger _log = log;

	/// <summary>Write the contained rows of one published generation entry (nothing to do when it carries no contents).</summary>
	internal void Register(ulong actor, ulong itemId, CharacterItemMsg item)
	{
		if (item.Contents.Count == 0)
		{
			return;
		}

		if (item.InstanceId != itemId)
		{
			// The capture's identity must be the entry's: a mismatch would make the sync name a parent this
			// kernel does not hold and spawn a fresh row under the wrong id.
			_log.LogWarning("[GenItems] item {ItemId} is captured as instance {InstanceId} — its contained rows are not written.",
				itemId, item.InstanceId);
			return;
		}

		if (!_authority.TrySyncContainerFacts(actor, item, out _, out _))
		{
			_log.LogWarning("[GenItems] the contained rows of item {ItemId} were refused by the kernel — a report about them will name an unjudged item.",
				itemId);
		}
	}
}
