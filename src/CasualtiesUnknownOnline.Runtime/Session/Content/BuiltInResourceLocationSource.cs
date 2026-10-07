using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Localization;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// CUO's own non-game resource ids. The player entity type is the one id the
/// selector vocabulary already used (<c>@a[type=cu:player]</c>); game content
/// ids (<c>cu:&lt;item id&gt;</c>) come from the Game Adapter's vanilla source and
/// mod content ids from <see cref="ModContentResourceLocationSource"/>, so this
/// source stays deliberately tiny.
/// </summary>
public sealed class BuiltInResourceLocationSource(ILocalizationService localization) : IResourceLocationSource
{
	private static readonly ContentId PlayerId = ContentId.Parse($"{ContentId.BuiltInNamespace}:player");

	/// <summary>
	/// The console's kind word for a CUO-owned resource id, deliberately NOT a
	/// <see cref="ModContentKind"/>: that vocabulary names the kinds a mod
	/// registers and a provider materializes, CUO has no entity provider, and a
	/// resource entry's kind is display text no consumer routes on (the
	/// completion row renders it as <c>entity · &lt;name&gt;</c>).
	/// </summary>
	private const string PlayerKind = "entity";

	public IReadOnlyList<ResourceLocationEntry> Entries =>
	[
		new(PlayerId, PlayerKind, localization.T("content.cu_player")),
	];
}
