using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Localization;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Localization;

public sealed class LocalizationServiceTests
{
	[Fact]
	public void DefaultsToEnglish()
	{
		var service = new LocalizationService(new MutableOptionsMonitor<LocalizationOptions>(new LocalizationOptions()));

		Assert.Equal("Home", service.T("tab.home"));
		Assert.Equal("en", service.Language);
	}

	[Fact]
	public void ChineseLanguageReturnsChineseText()
	{
		var monitor = new MutableOptionsMonitor<LocalizationOptions>(new LocalizationOptions());
		var service = new LocalizationService(monitor);
		monitor.Set(new LocalizationOptions { Language = "zh" });

		Assert.Equal("首页", service.T("tab.home"));
		Assert.Equal("zh", service.Language);
	}

	[Fact]
	public void ChineseRegionalCodeNormalizesToZh()
	{
		var monitor = new MutableOptionsMonitor<LocalizationOptions>(new LocalizationOptions());
		var service = new LocalizationService(monitor);
		monitor.Set(new LocalizationOptions { Language = "zh-CN" });

		Assert.Equal("zh", service.Language);
		Assert.Equal("首页", service.T("tab.home"));
	}

	[Fact]
	public void UnknownLanguageFallsBackToEnglish()
	{
		var monitor = new MutableOptionsMonitor<LocalizationOptions>(new LocalizationOptions());
		var service = new LocalizationService(monitor);
		monitor.Set(new LocalizationOptions { Language = "fr" });

		Assert.Equal("en", service.Language);
		Assert.Equal("Home", service.T("tab.home"));
	}

	[Fact]
	public void MissingKeyReturnsKeyItself()
	{
		var service = new LocalizationService(new MutableOptionsMonitor<LocalizationOptions>(new LocalizationOptions()));

		Assert.Equal("no.such.key", service.T("no.such.key"));
	}

	[Fact]
	public void FormatUsesLocalizedFormatString()
	{
		var monitor = new MutableOptionsMonitor<LocalizationOptions>(new LocalizationOptions());
		var service = new LocalizationService(monitor);
		monitor.Set(new LocalizationOptions { Language = "zh" });

		Assert.Equal("大厅：123", service.Format("home.lobby", 123));
	}

	[Fact]
	public void LanguageChangedFiresOnConfigHotReload()
	{
		var monitor = new MutableOptionsMonitor<LocalizationOptions>(new LocalizationOptions());
		var service = new LocalizationService(monitor);
		string? changed = null;
		service.LanguageChanged += language => changed = language;

		monitor.Set(new LocalizationOptions { Language = "zh" });

		Assert.Equal("zh", changed);
	}

	[Fact]
	public void LegacyInventoryExpansionKeysAreRemoved()
	{
		Assert.False(LocalizationCatalog.English.ContainsKey("member.view_items"));
		Assert.False(LocalizationCatalog.English.ContainsKey("member.hide_items"));
		Assert.False(LocalizationCatalog.Chinese.ContainsKey("member.view_items"));
		Assert.False(LocalizationCatalog.Chinese.ContainsKey("member.hide_items"));
		Assert.True(LocalizationCatalog.English.ContainsKey("member.open_backpack"));
		Assert.True(LocalizationCatalog.Chinese.ContainsKey("member.open_backpack"));
	}
}
