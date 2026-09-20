using System.Collections.Generic;
using System.Text;

namespace CasualtiesUnknownOnline.Tests.ContractTool;

/// <summary>
/// Bridges a reflection <c>Type.FullName</c> — the spelling the runtime's
/// <c>PatchInventory</c> contracts carry, with assembly-qualified generic
/// arguments — to the canonical form the snapshot stores (see
/// <c>TypeNameFormat</c>). This is the only place the two spellings meet, and the
/// parity gate is the only caller: the snapshot format itself never has to know
/// what reflection would have said.
/// </summary>
internal static class ReflectionTypeName
{
	internal static string Canonical(string fullName)
	{
		var index = 0;
		return Parse(fullName, ref index);
	}

	internal static string Canonical(IEnumerable<string> fullNames)
	{
		var names = new List<string>();
		foreach (var fullName in fullNames)
		{
			names.Add(Canonical(fullName));
		}

		return string.Join(", ", names);
	}

	/// <summary>
	/// One type name: the name up to a bracket/comma/suffix, then one of
	///   - generic arguments <c>[[Arg, Assembly],[Arg, Assembly]]</c>,
	///   - an array rank <c>[]</c> / <c>[,]</c>,
	///   - <c>&amp;</c> / <c>*</c> suffixes.
	/// An assembly qualification after a comma is dropped: the snapshot never
	/// carries one.
	/// </summary>
	private static string Parse(string text, ref int index)
	{
		var name = new StringBuilder();
		while (index < text.Length && text[index] is not ('[' or ']' or ',' or '&' or '*'))
		{
			name.Append(text[index]);
			index++;
		}

		var suffix = string.Empty;
		if (index < text.Length && text[index] == '[')
		{
			if (index + 1 < text.Length && text[index + 1] == '[')
			{
				var arguments = new List<string>();
				index += 2;
				while (true)
				{
					arguments.Add(Parse(text, ref index));

					// Skip this argument's assembly qualification up to its closing bracket.
					while (index < text.Length && text[index] != ']')
					{
						index++;
					}

					if (index < text.Length)
					{
						index++;
					}

					if (index + 1 < text.Length && text[index] == ',' && text[index + 1] == '[')
					{
						index += 2;
						continue;
					}

					break;
				}

				if (index < text.Length && text[index] == ']')
				{
					index++;
				}

				return name + "<" + string.Join(",", arguments) + ">";
			}

			index++;
			var rank = 1;
			while (index < text.Length && text[index] == ',')
			{
				rank++;
				index++;
			}

			while (index < text.Length && text[index] != ']')
			{
				index++;
			}

			if (index < text.Length)
			{
				index++;
			}

			suffix = "[" + new string(',', rank - 1) + "]";
		}

		while (index < text.Length && text[index] is '&' or '*')
		{
			suffix += text[index];
			index++;
		}

		return name + suffix;
	}
}
