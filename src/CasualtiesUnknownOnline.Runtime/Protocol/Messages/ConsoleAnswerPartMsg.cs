using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One PART of a command's answer, as the catalogue sees it: a key plus its ordered arguments. A command answer is not one
/// sentence — <c>/heal @a</c> answers one part per target, each naming a player and what happened to them — so it travels as
/// a list of these and the READING client renders each part in its own language.
///
/// <para>
/// This is the answer half of the rule the refusal half already follows (the user's 2026-10-25 ruling, translated: "it is not only
/// commands — everything that involves more than one language has to change"): a sentence crosses a language boundary only once, and
/// the client that shows the line is the one whose language must
/// win. The result messages keep their plain text field beside the parts, because the host's log and any client without the
/// entry still read it.
/// </para>
/// </summary>
[ProtoContract]
public sealed class ConsoleAnswerPartMsg
{
	/// <summary>The catalogue key of this part.</summary>
	[ProtoMember(1)]
	public string Key { get; set; } = string.Empty;

	/// <summary>The values the key's sentence places, in the order the catalogue places them.</summary>
	[ProtoMember(2)]
	public List<string> Args { get; set; } = [];
}
