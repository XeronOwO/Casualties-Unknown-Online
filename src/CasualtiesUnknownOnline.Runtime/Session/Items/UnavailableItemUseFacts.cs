using CasualtiesUnknownOnline.Application.Kernel;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The native-use data anchor without a Game Adapter: this build cannot read the
/// game's item registry, so the seam is told it holds no fact about any definition
/// and the use report is admitted exactly as it was before the rule existed.
///
/// <para>
/// The answer is <see cref="ItemUsePermission.Unknown"/>, NOT a refusal, and it is
/// the family's documented direction for a read that failed: a bound may only
/// refuse what the receiver can establish, and "this build has no game data" is not
/// evidence that an item is unusable. What keeps the absence diagnosable instead of
/// silent is the construction line below — a production composition that reaches
/// this class is mis-wired, and the adapter replaces the registration in the same
/// container.
/// </para>
/// </summary>
public sealed class UnavailableItemUseFacts(ILogger<UnavailableItemUseFacts> log) : IKernelItemUseFacts
{
	private readonly ILogger<UnavailableItemUseFacts> _log = log;
	private bool _announced;

	/// <summary>No registry: no definition has a fact, so every use report is admitted unchecked. Said once, at Information, because it is a build property rather than a report.</summary>
	public ItemUsePermission NativeUse(string definitionId)
	{
		if (!_announced)
		{
			_announced = true;
			_log.LogInformation("This build has no Game Adapter, so the game's item data cannot be read: native-use reports are admitted unchecked (first definition '{Definition}').", definitionId);
		}

		return ItemUsePermission.Unknown;
	}
}
