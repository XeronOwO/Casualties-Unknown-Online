namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>Where <c>/summon</c> puts the object it creates — the native console's own position vocabulary.</summary>
public enum WorldSummonAnchor
{
	/// <summary>The world point under the POINTER (<c>Camera.main.ScreenToWorldPoint(Input.mousePosition)</c>) — the native command's default.</summary>
	Cursor,

	/// <summary>The local body's own position (<c>PlayerCamera.main.body.transform.position</c>).</summary>
	Player,

	/// <summary>A uniform point in the layer rectangle (<c>±halfWidth</c>, <c>±halfHeight</c>).</summary>
	Random,
}
