using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The in-game command/chat console surface used by the Online UI. It owns a
/// bounded output buffer and the single input dispatcher: text beginning with
/// <c>/</c> is executed as a command, any other non-empty line is sent through
/// the existing text-chat domain.
/// </summary>
public interface ICommandControl : IDisposable
{
	/// <summary>Read-only console output, oldest first (bounded).</summary>
	IReadOnlyList<ConsoleLine> Lines { get; }

	/// <summary>
	/// Execute one console line. Returns true when the input was accepted
	/// (a command was recognized or a chat line was sent); false for empty
	/// input or an invalid chat line.
	/// </summary>
	bool TryExecute(string input);

	/// <summary>Clear the local console output buffer.</summary>
	void Clear();
}
