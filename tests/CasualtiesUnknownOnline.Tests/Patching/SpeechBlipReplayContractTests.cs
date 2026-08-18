using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The speech-blip audio pass is intentionally event-free: speech blips are not
/// a dedicated CharacterSoundMsg. A player's own blips are native (Talker.Update,
/// Talker.cs:380-414), and a remote speaker's blips are reproduced locally by the
/// SAME native path because SpeechSync.Replay writes the final bubble text into
/// the peer's clone/trader Talker and its Update types it out (playing "speech"/
/// "speechbad"/talkSoundCustom per letter). These tests lock that replay surface:
/// the Talker.Talk suppression shape (clones/guest traders never start their own
/// divergent bubbles) and the SpeechSync.Replay entry used to feed the native
/// typing/audio path. The Runtime half is covered by SpeechChannelTests, and the
/// Talker fields are locked by GameFieldContractTests.
/// </summary>
public class SpeechBlipReplayContractTests
{
	private static readonly Type TalkerPatch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.TalkerPatch",
		throwOnError: true)!;

	private static readonly Type SpeechSync = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.World.SpeechSync",
		throwOnError: true)!;

	private static IEnumerable BuildContracts()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		return (IEnumerable)build.Invoke(null, null)!;
	}

	[Fact]
	public void PatchInventory_DeclaresTheTalkerTalkContract()
	{
		var hasTalk = BuildContracts().Cast<object>().Any(c =>
		{
			var type = c.GetType();
			if ((type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string) != "Talker"
				|| (type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string) != "Talk")
			{
				return false;
			}

			var parameters = type.GetProperty("ParameterTypes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c);
			return parameters is IList names && names.Count == 4;
		});
		Assert.True(hasTalk, "PatchInventory must declare the Talker.Talk(List<string>, Limb, bool, bool) patch contract.");
	}

	[Fact]
	public void TalkPatch_PrefixAndPostfixCarryTheCurrentStringState()
	{
		var talkPatch = TalkerPatch.GetNestedType("TalkPatch", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("TalkerPatch.TalkPatch not found.");

		var prefix = talkPatch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("TalkPatch.Prefix not found.");
		var prefixParameters = prefix.GetParameters();
		Assert.True(prefixParameters.Length == 2
			&& prefixParameters[0].Name == "__instance"
			&& prefixParameters[0].ParameterType.FullName == "Talker"
			&& prefixParameters[1].Name == "__state"
			&& prefixParameters[1].ParameterType == typeof(string).MakeByRefType(),
			$"TalkPatch.Prefix must be (Talker __instance, out string __state), got {prefixParameters.Length} parameter(s)");

		var postfix = talkPatch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("TalkPatch.Postfix not found.");
		var postfixParameters = postfix.GetParameters();
		Assert.True(postfixParameters.Length == 2
			&& postfixParameters[0].Name == "__instance"
			&& postfixParameters[0].ParameterType.FullName == "Talker"
			&& postfixParameters[1].Name == "__state"
			&& postfixParameters[1].ParameterType == typeof(string),
			$"TalkPatch.Postfix must be (Talker __instance, string __state), got {postfixParameters.Length} parameter(s)");
	}

	[Fact]
	public void SpeechSyncReplay_FeedsTheNativeTypingAndAudioPath()
	{
		var replay = SpeechSync.GetMethod("Replay", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("SpeechSync.Replay not found.");
		var parameters = replay.GetParameters();
		Assert.True(parameters.Length == 2
			&& parameters[0].Name == "talker"
			&& parameters[0].ParameterType.FullName == "Talker"
			&& parameters[1].Name == "text"
			&& parameters[1].ParameterType == typeof(string),
			$"SpeechSync.Replay must be (Talker talker, string text), got {parameters.Length} parameter(s)");
	}
}
