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

	public IReadOnlyList<ResourceLocationEntry> Entries =>
	[
		new(PlayerId, ModContentKind.Entity, localization.T("content.cu_player")),
	];
}
