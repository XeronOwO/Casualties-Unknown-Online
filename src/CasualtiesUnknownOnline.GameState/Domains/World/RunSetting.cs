namespace CasualtiesUnknownOnline.GameState.Domains.World;

/// <summary>
/// One typed run-setting value owned by the World/Run kernel domain. Using a
/// typed list instead of <c>Dictionary&lt;string, object&gt;</c> keeps the
/// deterministic domain free of runtime type checks and keeps save/wire mapping
/// explicit.
/// </summary>
public sealed record RunSetting(
	string Key,
	RunSettingKind Kind,
	int IntValue = 0,
	float FloatValue = 0f,
	bool BoolValue = false,
	string StringValue = "");
