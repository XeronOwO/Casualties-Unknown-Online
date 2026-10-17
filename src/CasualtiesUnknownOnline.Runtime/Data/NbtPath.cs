using System.Collections.Generic;
using System.Text;

namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>
/// A parsed NBT path: the item state language's address of one value inside a document.
///
/// <para>
/// The subset is keys, quoted keys and indices — the shapes the item state needs. A wildcard
/// (<c>[]</c>) or a filter (<c>[{…}]</c>) selects several values in Minecraft, and every CUO
/// verb here addresses one, so both are refused BY NAME by the parser rather than
/// half-supported.
/// </para>
/// </summary>
public sealed record NbtPath(IReadOnlyList<NbtPathSegment> Segments)
{
	/// <summary>The path with no segments: the document itself. Only <c>get</c> and <c>merge</c> accept it.</summary>
	public static NbtPath Root { get; } = new([]);

	public bool IsRoot => Segments.Count == 0;

	/// <summary>The canonical text, the form an answer quotes back.</summary>
	public string Text
	{
		get
		{
			var builder = new StringBuilder();
			foreach (var segment in Segments)
			{
				if (segment is NbtPathKey key)
				{
					if (builder.Length > 0)
					{
						builder.Append('.');
					}

					if (SnbtWriter.KeyNeedsQuotes(key.Key))
					{
						builder.Append('"').Append(key.Key.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
					}
					else
					{
						builder.Append(key.Key);
					}
				}
				else if (segment is NbtPathIndex index)
				{
					builder.Append('[').Append(index.Index).Append(']');
				}
			}

			return builder.ToString();
		}
	}
}
