using System;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Wiring pins for the carried rider's two readings. The DECISIONS live in the
/// Runtime rule (<c>CarryPresentationReadingTests</c> covers its matrices); what a
/// matrix cannot see is WHERE a reading is taken, WHICH view stores the reference
/// it is compared against, and WHICH branch the level comes from — the properties
/// the rider-teleport acceptance run depends on:
///
/// - a drift is only meaningful against the frame that rendered, so it must be
///   read BEFORE the state write overwrites the clone;
/// - the stored offset must belong to THIS frame's placement, so it is captured
///   after the ride pose wrote the root, in both carry views;
/// - a released relation may drop the reference but never the window's reading;
/// - a carry participant's 1 Hz line must be visible in a DEFAULT session, and a
///   reportable reading must be a warning rather than a line somebody has to
///   raise the log level to find.
///
/// They are source pins because those properties are orderings and branches inside
/// the adapter: the pure rule cannot fail on them, and nothing but a session would
/// otherwise notice them going away.
/// </summary>
public class CarryPresentationProbePinTests
{
	private const string RendererFile = "RemotePlayerRenderer.cs";
	private const string ProbeFile = "CarryPresentationProbe.cs";

	[Fact]
	public void DriftReading_IsTakenBeforeTheStreamWrite()
	{
		var update = Section(
			ReadAdapter(RendererFile),
			"internal void Update(Body? localBody)",
			"internal void RefreshLocalCarrierAttach");
		var read = update.IndexOf("MeasurePinDrift(localBody, remote.SteamId, clone, cloneDriver);", StringComparison.Ordinal);
		var stateWrite = update.IndexOf("SessionStatePump.Apply(remote, clone);", StringComparison.Ordinal);
		Assert.True(read >= 0, "RemotePlayerRenderer.Update must take the drift reading every frame");
		Assert.True(
			stateWrite > read,
			"the drift reading must run BEFORE SessionStatePump.Apply: after that write the transform no longer holds what the frame that rendered showed");
	}

	[Fact]
	public void BothCarryViews_StoreTheReferenceAfterTheRidePosePlacedTheClone()
	{
		var attach = Section(
			ReadAdapter(RendererFile),
			"private void ApplyRemoteCarrierAttachAll(",
			"private static Transform GetOrCreateCarryMount(");
		Assert.Equal(2, Occurrences(attach, "CarryPresentationProbe.Store("));
		Assert.Contains("localCarrier: true", attach);
		Assert.Contains("localCarrier: false", attach);

		// The stored offset has to belong to the placement of THIS frame: an
		// offset captured before the ride pose wrote the root describes the
		// previous frame and reads as drift for a rider that never moved.
		var firstPose = attach.IndexOf("CarriedBodyPlacement.ApplyRidePose(", StringComparison.Ordinal);
		var firstStore = attach.IndexOf("CarryPresentationProbe.Store(", StringComparison.Ordinal);
		var secondPose = attach.IndexOf("CarriedBodyPlacement.ApplyRidePose(", firstPose + 1, StringComparison.Ordinal);
		var secondStore = attach.IndexOf("CarryPresentationProbe.Store(", firstStore + 1, StringComparison.Ordinal);
		Assert.True(firstPose >= 0 && firstStore > firstPose, "the local-carrier reference must be stored after the ride pose placed the clone");
		Assert.True(secondPose > firstStore && secondStore > secondPose, "the third-party reference must be stored after its own ride pose placed the clone");
	}

	[Fact]
	public void ReleasedRelation_DropsTheReference_ButKeepsTheWindowsReading()
	{
		// A drift measured in the frames before a release is still the window's
		// reading: the release may drop the reference it was taken against, never
		// the reading itself, or the moments the symptom is most likely are
		// exactly the moments the log stays silent about it.
		Assert.Contains("CarryPresentationProbe.Clear(riderClone);", ReadAdapter(RendererFile));
		var probe = ReadAdapter(ProbeFile);
		Assert.DoesNotContain("PinDriftWindowMax = 0f", probe);
		Assert.DoesNotContain("PinCountInWindow = 0", probe);
	}

	[Fact]
	public void DriftReader_PlacesNothing()
	{
		// The same property the limb probe is held to: a reading may only read.
		// A fix for a non-zero reading has to be a deliberate change, and this pin
		// is what makes it deliberate.
		var reader = Section(
			ReadAdapter(RendererFile),
			"private void MeasurePinDrift(",
			"private Vector2 AnchorFor(");
		Assert.DoesNotContain("transform.position =", reader);
		Assert.DoesNotContain("SetParent(", reader);
	}

	[Fact]
	public void CarryParticipantLine_IsDefaultVisible_AndOtherClonesStayOnDebug()
	{
		var renderer = ReadAdapter(RendererFile);

		// Who counts as a participant is the Runtime rule's decision, and it is
		// asked with the facts the diagnostic OBSERVED: a constant in any of the
		// five places would make the line lie about the clone it describes.
		Assert.Contains("var atDefaultLevel = CarryPresentationReading.IsCarryParticipant(", renderer);
		Assert.Contains("isLocalRiderClone: isRiderClone,", renderer);
		Assert.Contains("isLocalCarrierClone: isCarrierClone,", renderer);
		Assert.Contains("isRemoteRider: poseDriver != null && poseDriver.IsCarriedRider,", renderer);
		Assert.Contains("isRemoteCarrier: poseDriver != null && poseDriver.IsCarrier,", renderer);
		Assert.Contains("pinnedInWindow: pinnedInWindow);", renderer);
		Assert.Contains("LogCloneLine(atDefaultLevel, steamId, pos, reported, clone, carryTag);", renderer);

		// And the level must be chosen from that flag in the right branch: the
		// Information call belongs to the atDefaultLevel branch and the Debug call
		// to the other one. Comparing their order alone let an inverted branch
		// through, which would send a carry participant to Debug (invisible in a
		// default session, the whole point of the change) and every ordinary clone
		// to Information.
		var logger = Section(renderer, "private void LogCloneLine(", "private void OnRemoteJoined(");
		var atDefault = BracedBlockAfter(logger, "if (atDefaultLevel)");
		var otherwise = BracedBlockAfter(logger, "else");
		Assert.Contains("_log.LogInformation(CloneLine,", atDefault);
		Assert.DoesNotContain("_log.LogDebug(CloneLine,", atDefault);
		Assert.Contains("_log.LogDebug(CloneLine,", otherwise);
		Assert.DoesNotContain("_log.LogInformation(CloneLine,", otherwise);
	}

	[Fact]
	public void Anomalies_AreDecidedByTheRuntimeRule_AndRenderedAsWarnings()
	{
		var probe = ReadAdapter(ProbeFile);
		Assert.Contains("var anomalies = CarryPresentationReading.Anomalies(", probe);
		Assert.Equal(3, Occurrences(probe, "log.LogWarning("));

		// Each report sits inside its own branch of the decision: a warning with
		// no branch behind it fires on a healthy window, and what each branch MEANS
		// is the Runtime matrix's job rather than a substring's.
		foreach (var flag in new[] { "LimbSeparation", "RiderDrift", "NoCarryPin" })
		{
			var report = BracedBlockAfter(probe, $"if (anomalies.{flag})");
			Assert.Contains("log.LogWarning(", report);
		}
	}

	[Fact]
	public void WindowReadings_AreResetWithTheWindow()
	{
		var renderer = ReadAdapter(RendererFile);
		Assert.Contains("poseDriver.LimbSeparationWindowMax = 0f;", renderer);
		Assert.Contains("poseDriver.PinDriftWindowMax = 0f;", renderer);
		Assert.Contains("poseDriver.PinCountInWindow = 0;", renderer);
	}

	[Fact]
	public void ReadingStatement_PrintsBothReadingsWithZeroIncluded()
	{
		var probe = ReadAdapter(ProbeFile);
		Assert.Contains(", limbSeparation=", probe);
		Assert.Contains(", riderDrift=", probe);
		Assert.DoesNotContain("transform.position =", probe);
	}

	private static string ReadAdapter(string fileName) =>
		File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"src",
			"CasualtiesUnknownOnline.GameAdapter",
			"Character",
			fileName));

	private static string Section(string text, string startToken, string endToken)
	{
		var start = text.IndexOf(startToken, StringComparison.Ordinal);
		Assert.True(start >= 0, $"the source no longer declares `{startToken}`");
		var end = text.IndexOf(endToken, start, StringComparison.Ordinal);
		Assert.True(end > start, $"the source no longer declares `{endToken}` after `{startToken}`");
		return text.Substring(start, end - start);
	}

	/// <summary>The braced block that follows a token, so a pin can speak about WHICH branch a call sits in rather than only where it appears.</summary>
	private static string BracedBlockAfter(string text, string token)
	{
		var start = text.IndexOf(token, StringComparison.Ordinal);
		Assert.True(start >= 0, $"the source no longer declares `{token}`");
		var open = text.IndexOf('{', start);
		Assert.True(open > start, $"`{token}` is not followed by a block");
		var depth = 0;
		for (var index = open; index < text.Length; index++)
		{
			if (text[index] == '{')
			{
				depth++;
			}
			else if (text[index] == '}')
			{
				depth--;
				if (depth == 0)
				{
					return text.Substring(open, index - open + 1);
				}
			}
		}

		throw new InvalidOperationException($"`{token}` has no closing brace");
	}

	private static int Occurrences(string text, string token)
	{
		var count = 0;
		for (var index = text.IndexOf(token, StringComparison.Ordinal);
			index >= 0;
			index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		if (directory is null)
		{
			throw new InvalidOperationException("could not locate repository root (CasualtiesUnknownOnline.slnx)");
		}

		return directory.FullName;
	}
}
