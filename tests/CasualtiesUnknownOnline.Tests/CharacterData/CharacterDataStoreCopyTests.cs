using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.CharacterData;

/// <summary>
/// The host's saved-character table is a SAVE CARRIER: every cut writes
/// <c>characters/&lt;playerKey&gt;.json</c> out of it. A member's report is also handed on to the clone
/// presentation, whose fact table mutates the item tree it renders when a carried-sync moves an item in
/// or out. The table therefore stores its OWN copy of a report (<c>CharacterDataStore.SaveReportedCharacterData</c>);
/// this pins that boundary, while <see cref="CharacterDataCloneTests"/> pins the copy itself. The
/// reported defect is simulated exactly: the presentation's mutation is the in-place removal from the
/// message's item list.
/// </summary>
public class CharacterDataStoreCopyTests
{
	[Fact]
	public void AStoredReportIsACopy_ThePresentationCannotMutateTheSaveCarrier()
	{
		using var world = ItemSimWorld.Create();
		var store = world.Host.Services.GetRequiredService<CharacterDataStore>();
		const ulong peer = 2002UL;
		var report = new CharacterDataMsg
		{
			SlotCount = 5,
			Health = new CharacterHealthMsg { BloodVolume = 90f },
			Items = [new CharacterItemMsg { InstanceId = 100UL, ItemId = "bag", SlotIndex = 0, Condition = 0.5f }],
		};

		Assert.True(store.SaveReportedCharacterData(peer, report));
		var stored = Assert.IsType<CharacterDataMsg>(store.GetSavedCharacter(peer));
		Assert.NotSame(report, stored);

		// The clone presentation's carried-sync mutation, in place on the message it was handed.
		report.Items.Clear();
		report.Items.Add(new CharacterItemMsg { InstanceId = 200UL, ItemId = "rope", SlotIndex = 1 });

		Assert.Single(stored.Items);
		Assert.Equal(100UL, stored.Items[0].InstanceId);

		// The documented exception: the vitals leaves are shared by design.
		Assert.Same(report.Health, stored.Health);
	}
}
