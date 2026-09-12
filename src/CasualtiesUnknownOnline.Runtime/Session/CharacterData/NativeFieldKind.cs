namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>Which native character field a <see cref="NativeFieldWrite"/> is about — the three live values a restore writes back.</summary>
internal enum NativeFieldKind
{
	/// <summary>The body's happiness history (<c>Body.lastHappiness</c>).</summary>
	HappinessHistory,

	/// <summary>The camera's per-run calorie counter (<c>PlayerCamera.caloriesConsumed</c>).</summary>
	CaloriesConsumed,

	/// <summary>The wound window's height/age/id/version (<c>WoundView.cInfo</c>).</summary>
	CharacterInfo,
}
