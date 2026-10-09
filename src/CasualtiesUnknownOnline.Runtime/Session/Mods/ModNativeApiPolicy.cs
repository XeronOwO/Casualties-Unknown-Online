namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The pure native-API policy: the operation-id shape rail, and nothing else.
/// There is no value surface left to police — a native operation's result is
/// declared by that operation's own typed projection on
/// <see cref="IModNativeApiProvider"/>, so an unusable value cannot be produced
/// and a raw byte array cannot be passed. The contract's shape states that once,
/// where a runtime scan over an envelope stated it only after the fact.
/// </summary>
public static class ModNativeApiPolicy
{
	/// <summary>Maximum operation-id length.</summary>
	public const int MaxOperationLength = 128;

	/// <summary>
	/// True when the operation id is non-empty, at most <see cref="MaxOperationLength"/>
	/// characters, and uses only lowercase/uppercase ASCII letters, digits,
	/// dot, underscore or hyphen (the stable dotted id shape used by the Mod API).
	/// </summary>
	public static bool IsValidOperation(string operation)
	{
		if (string.IsNullOrEmpty(operation) || operation.Length > MaxOperationLength)
		{
			return false;
		}

		foreach (var c in operation)
		{
			var isAsciiLetterOrDigit = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
			if (!isAsciiLetterOrDigit && c is not ('.' or '_' or '-'))
			{
				return false;
			}
		}

		return true;
	}
}
