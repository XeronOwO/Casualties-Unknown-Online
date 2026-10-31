using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// A mod's own sentences: the surface that lets an answer a player reads reach that player in the player's language, and the namespace guard that keeps two
/// mods' catalogues safe to merge.
/// </summary>
[Trait("Category", "Unit")]
public class ModLocalisationTests
{
	private const ulong HostId = 76561198000000001;
	private const ulong GuestId = 76561198000000002;
	private const ulong LobbyId = 10977524123456789;

	[Fact]
	public void AModsOwnKey_RendersInTheReadersLanguage()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var localisation = host.Services.GetRequiredService<ILocalizationService>();
		var adapter = Adapter("cuo.sample", localisation);

		Assert.True(adapter.RegisterCatalog("en", """{ "cuo.sample.answer": "the answer for {0}" }"""));
		Assert.True(adapter.RegisterCatalog("zh", """{ "cuo.sample.answer": "给 {0} 的回答" }"""));

		Assert.Equal("the answer for you", string.Format(localisation.T("cuo.sample.answer"), "you"));
		Assert.Contains(localisation.SupportedLanguages, language => language == "zh");
	}

	[Fact]
	public void ACatalogueWithAForeignKey_IsRefusedWhole()
	{
		// A half-applied table would leave the mod partly translated with nothing to see; the whole catalogue is refused by name instead.
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var localisation = host.Services.GetRequiredService<ILocalizationService>();
		var adapter = Adapter("cuo.sample", localisation);

		Assert.False(adapter.RegisterCatalog("en", """{ "cuo.sample.answer": "mine", "console.ready": "theirs" }"""));
		Assert.Equal("cuo.sample.answer", localisation.T("cuo.sample.answer"));
	}

	[Fact]
	public void AModCannotRedefineAFrameworkSentence()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var localisation = host.Services.GetRequiredService<ILocalizationService>();
		var ready = localisation.T("console.ready");

		// The adapter's namespace guard refuses this outright (the key is not the mod's), and the service itself never overwrites a built-in entry either.
		Assert.False(Adapter("cuo.sample", localisation).RegisterCatalog("en", """{ "console.ready": "hijacked" }"""));
		Assert.Equal(ready, localisation.T("console.ready"));
	}

	private static ModLocalisationAdapter Adapter(string modId, ILocalizationService localisation) =>
		new(new ModManifest(modId, "Sample", "1.0.0", NetworkMode.Synchronized, null), localisation, NullLogger.Instance);
}
