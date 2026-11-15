using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameAdapter;

/// <summary>
/// The session's character picker: all three characters are pickable while a
/// session is active, the solo unlock still gates outside one, and a session
/// win never writes the solo character unlock.
/// </summary>
public class CharacterUnlockPolicyTests
{
	[Theory]
	[InlineData(true, false, true)]
	[InlineData(true, true, true)]
	[InlineData(false, false, false)]
	[InlineData(false, true, true)]
	public void Picker_OffersEveryCharacterInSession_AndKeepsTheSoloGateOutside(bool inSession, bool soloUnlocked, bool expected) =>
		Assert.Equal(expected, CharacterUnlockPolicy.CanPick(inSession, soloUnlocked));

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public void Win_WritesTheSoloUnlockOnlyOutsideASession(bool inSession, bool expected) =>
		Assert.Equal(expected, CharacterUnlockPolicy.ShouldWriteWin(inSession));
}
