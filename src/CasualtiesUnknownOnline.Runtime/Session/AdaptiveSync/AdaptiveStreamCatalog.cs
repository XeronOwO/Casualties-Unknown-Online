using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// The built-in adaptive stream profiles. This is intentionally a small,
/// read-only registry: adding a new adaptive stream means adding one profile
/// here, then the stream owner consumes the shared rate service.
/// </summary>
public static class AdaptiveStreamCatalog
{
	private const int MinHz = 1;
	private const int MaxHz = 60;

	private static readonly IReadOnlyDictionary<AdaptiveStreamId, AdaptiveStreamProfile> Profiles =
		new Dictionary<AdaptiveStreamId, AdaptiveStreamProfile>
		{
			[AdaptiveStreamId.PlayerStateBroadcast] = new(
				AdaptiveStreamId.PlayerStateBroadcast,
				"PlayerStateBroadcast",
				AdaptiveStreamDeliveryMode.LatestWins,
				MinHz,
				MaxHz,
				Priority: 1,
				MaxBytesPerSecond: 128 * 1024),
			[AdaptiveStreamId.PlayerStateReport] = new(
				AdaptiveStreamId.PlayerStateReport,
				"PlayerStateReport",
				AdaptiveStreamDeliveryMode.LatestWins,
				MinHz,
				MaxHz,
				Priority: 1,
				MaxBytesPerSecond: 64 * 1024),
			[AdaptiveStreamId.EnemyStateBroadcast] = new(
				AdaptiveStreamId.EnemyStateBroadcast,
				"EnemyStateBroadcast",
				AdaptiveStreamDeliveryMode.LatestWins,
				MinHz,
				MaxHz,
				Priority: 1,
				MaxBytesPerSecond: 256 * 1024),
			[AdaptiveStreamId.TutorialClawBroadcast] = new(
				AdaptiveStreamId.TutorialClawBroadcast,
				"TutorialClawBroadcast",
				AdaptiveStreamDeliveryMode.LatestWins,
				MinHz,
				MaxHz,
				Priority: 2,
				MaxBytesPerSecond: 32 * 1024),
			[AdaptiveStreamId.MedicalInjectionReport] = new(
				AdaptiveStreamId.MedicalInjectionReport,
				"MedicalInjectionReport",
				AdaptiveStreamDeliveryMode.Cumulative,
				MinHz,
				MaxHz,
				Priority: 2,
				MaxBytesPerSecond: 32 * 1024,
				BaseHz: 60),
			[AdaptiveStreamId.ShrapnelPositionReport] = new(
				AdaptiveStreamId.ShrapnelPositionReport,
				"ShrapnelPositionReport",
				AdaptiveStreamDeliveryMode.LatestWins,
				MinHz,
				MaxHz,
				Priority: 2,
				MaxBytesPerSecond: 32 * 1024,
				BaseHz: 60),
		};

	/// <summary>All registered adaptive stream profiles.</summary>
	public static IEnumerable<AdaptiveStreamProfile> All => Profiles.Values;

	/// <summary>Gets a profile by id, or false when the id is not registered.</summary>
	public static bool TryGet(AdaptiveStreamId id, out AdaptiveStreamProfile profile) =>
		Profiles.TryGetValue(id, out profile!);
}
