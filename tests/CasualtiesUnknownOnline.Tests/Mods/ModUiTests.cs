using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The local mod UI surface over the real mod stack: windows are registered
/// per-mod through <see cref="IModContext.Ui"/>, invalid/duplicate registrations
/// are refused, and the plugin-facing <see cref="IModUiControl"/> exposes the
/// registered windows with their draw callbacks intact (the plugin is the only
/// Unity-aware consumer; tests drive the callback through a recording fake).
/// </summary>
public class ModUiTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestUiMod UiMod(TestNode node) =>
		(TestUiMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestUiMod);

	[Fact]
	public void BindRegistersWindow_ContextExposesIt()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		var mod = UiMod(guest);
		Assert.True(mod.Registered);
		Assert.True(mod.Context!.Ui.IsRegistered("status"));
		Assert.Contains("status", mod.Context.Ui.WindowIds);
	}

	[Fact]
	public void DuplicateRegistration_IsRefused()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var ui = UiMod(guest).Context!.Ui;

		Assert.False(ui.Register("status", "Duplicate", _ => { }));
	}

	[Fact]
	public void InvalidRegistration_IsRefused()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var ui = UiMod(guest).Context!.Ui;

		Assert.False(ui.Register("", "Title", _ => { }));
		Assert.False(ui.Register("id", "", _ => { }));
		Assert.False(ui.Register("id", "Title", null!));
	}

	[Fact]
	public void Unregister_RemovesWindow()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var ui = UiMod(guest).Context!.Ui;

		Assert.True(ui.Unregister("status"));
		Assert.False(ui.IsRegistered("status"));
		Assert.Empty(ui.WindowIds);
		Assert.False(ui.Unregister("status"));
	}

	[Fact]
	public void ControlList_ExposesWindowWithModIdAndDrawCallback()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var control = guest.Services.GetRequiredService<IModUiControl>();

		var window = control.Windows.Single(w => w.ModId == "test.ui" && w.Id == "status");
		Assert.Equal("Test Status", window.Title);

		var recorder = new RecordingModUiWindow();
		window.Draw(recorder);

		Assert.Equal(
		[
			"Label:hello",
			"Separator",
			"Button:click",
			"TextField:seed:16",
		], recorder.Calls);
	}

	[Fact]
	public void Unregister_UpdatesThePluginFacingControlList()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var ui = UiMod(guest).Context!.Ui;
		var control = guest.Services.GetRequiredService<IModUiControl>();

		Assert.Contains(control.Windows, w => w.ModId == "test.ui" && w.Id == "status");

		ui.Unregister("status");

		Assert.DoesNotContain(control.Windows, w => w.ModId == "test.ui" && w.Id == "status");
	}

	private sealed class RecordingModUiWindow : IModUiWindow
	{
		public List<string> Calls { get; } = [];

		public void Label(string text) => Calls.Add($"Label:{text}");

		public bool Button(string text)
		{
			Calls.Add($"Button:{text}");
			return false;
		}

		public string TextField(string current, int maxLength)
		{
			Calls.Add($"TextField:{current}:{maxLength}");
			return current;
		}

		public void Separator() => Calls.Add("Separator");
	}
}
