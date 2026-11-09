using System;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Runtime.Configuration;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The backup ceiling's home is the config file: the window writes it through this, and the bound
/// options monitor reloads it, so the value the player picks survives a restart. The write is read
/// back — an entry whose own range clamps the value answers with the clamped number, and the window
/// must not show a ceiling the file does not hold.
/// </summary>
internal sealed class SaveConfigRetentionWriter(ConfigEntry<int> entry) : IBackupRetentionWriter
{
	public bool TryWrite(int keep, out string? refusal)
	{
		refusal = null;
		try
		{
			entry.Value = keep;
		}
		catch (Exception ex)
		{
			refusal = ex.Message;
			return false;
		}

		if (entry.Value != keep)
		{
			refusal = $"the config kept {entry.Value} instead of {keep}";
			return false;
		}

		return true;
	}
}
