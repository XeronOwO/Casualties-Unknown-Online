using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Content;

/// <summary>
/// CUO's own resource ids in the console's vocabulary: the built-in source
/// contributes exactly the player entity id, with the localized name and with a
/// kind word of its own. That word is deliberately NOT a
/// <see cref="ModContentKind"/> constant — the vocabulary names the kinds a
/// content provider binds, CUO has no entity provider, and a resource entry's
/// kind is display text the completion row renders (ticket
/// <c>docs/backlog/review/mod-content-kind-with-no-provider.md</c>).
/// </summary>
public class BuiltInResourceLocationSourceTests
{
	[Fact]
	public void Entries_CarryThePlayerId_WithTheEntityKindAndTheLocalizedName()
	{
		var entry = Assert.Single(new BuiltInResourceLocationSource(new StubLocalization("玩家")).Entries);

		Assert.Equal(ContentId.Parse("cu:player"), entry.Id);
		Assert.Equal("entity", entry.Kind);
		Assert.Equal("玩家", entry.DisplayName);
	}

	[Fact]
	public void Entries_TakeTheDisplayNameFromTheLocalizationCatalogue()
	{
		var localization = new StubLocalization("Player");

		_ = new BuiltInResourceLocationSource(localization).Entries;

		Assert.Equal(["content.cu_player"], localization.Keys);
	}

	private sealed class StubLocalization(string text) : ILocalizationService
	{
		internal List<string> Keys { get; } = [];

		public string Language => "en";

		public event Action<string>? LanguageChanged
		{
			add { }
			remove { }
		}

		public string T(string key)
		{
			Keys.Add(key);
			return text;
		}

		public string Format(string key, params object?[] args) => T(key);
	}
}
