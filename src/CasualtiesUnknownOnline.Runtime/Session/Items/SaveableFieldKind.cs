using System;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The saveable-field wire-kind classification — extracted from the adapter's
/// ItemStateCodec (the digest's component-field encoding) so the kind table
/// itself is unit-testable and game-update-guarded: a field whose type maps to
/// kind 0 (unsupported) is silently skipped on capture, and a wrong mapping
/// silently drops a carried item's live state. Pure — depends only on
/// System.Type, never on UnityEngine.
/// </summary>
internal static class SaveableFieldKind
{
	internal const int Unsupported = 0;
	internal const int Float = 1;
	internal const int Int = 2;
	internal const int Bool = 3;
	internal const int String = 4;
	internal const int StringList = 5;
	internal const int Enum = 6; // stored as its underlying int (GunScript.roundInChamber etc. — mutable enum state the digest must carry)

	internal static int Of(Type type)
	{
		if (type == typeof(float))
		{
			return Float;
		}

		if (type == typeof(int))
		{
			return Int;
		}

		if (type == typeof(bool))
		{
			return Bool;
		}

		if (type == typeof(string))
		{
			return String;
		}

		if (type == typeof(System.Collections.Generic.List<string>))
		{
			return StringList;
		}

		if (type.IsEnum)
		{
			return Enum;
		}

		return Unsupported;
	}
}
