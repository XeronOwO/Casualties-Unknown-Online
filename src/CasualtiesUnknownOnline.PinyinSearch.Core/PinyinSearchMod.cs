using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.PinyinSearch.Core;

/// <summary>
/// The CUO half of the mod. With CUO installed the same matcher the crafting box
/// uses also completes the console's resource-location argument, registered
/// through the framework's published stage seam
/// (<c>IModContext.ResourceCompletion</c>) instead of teaching the catalog about
/// pinyin. With CUO uninstalled this type is never touched: it is the only place
/// besides <see cref="PinyinSearchStage"/> that resolves an Abstractions type,
/// and no crafting path reaches it.
///
/// The declared native binding names the game code the mod patches — the Tier 2
/// declaration of <c>docs/api/advanced-modification-policy.md</c> §1.1, which
/// buys visibility, never stability. The same sentence lives in the mod's
/// documentation; a host's discovery log is where the claim is checked
/// (<c>[Mods] discovered … binds …</c>).
/// </summary>
[CuoMod("cuo.pinyinsearch", "Pinyin Search", "0.1.0", NetworkMode = NetworkMode.ClientOnly,
	NativeBinding = "PlayerCamera.RefreshRecipeList + Recipe.simpleName getter",
	Description = "Pinyin matching for the game's crafting search box, and for the console's "
		+ "resource-id completion when CUO is installed.")]
public sealed class PinyinSearchMod : ICuoMod
{
	private IModContext? _context;

	/// <summary>
	/// Registers the completion stage. A refusal is logged by the framework and
	/// reported here as well: the crafting half is unaffected either way, so a
	/// silent refusal would look like "pinyin completion works" until the player
	/// types.
	/// </summary>
	public void Bind(IModContext context)
	{
		_context = context;
		if (context.ResourceCompletion.TryRegisterMatchStage("pinyin", new PinyinSearchStage()))
		{
			context.Logger.LogInformation(
				"[Pinyin] console completion stage registered (enabled: {Enabled}).", PinyinSearchConfig.IsEnabled);
			return;
		}

		context.Logger.LogWarning(
			"[Pinyin] the console completion stage was refused; the crafting search box is unaffected.");
	}

	public void Initialize() => _context?.Logger.LogInformation("[Pinyin] initialized.");

	public void Start()
	{
	}

	public void Update()
	{
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}
}
