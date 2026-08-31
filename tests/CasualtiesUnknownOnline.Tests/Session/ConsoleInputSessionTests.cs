using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class ConsoleInputSessionTests
{
	[Fact]
	public void Open_PrefillsSlashAndSetsOpen()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));

		session.Open();

		Assert.True(session.IsOpen);
		Assert.Equal("/", session.Input);
	}

	[Fact]
	public void Submit_ExecutesClearsAndKeepsConsoleOpen()
	{
		var control = new StubControl();
		var session = CreateSession(control, new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("/help");

		Assert.True(session.Submit());
		Assert.Equal(["/help"], control.Executed);
		Assert.Equal(["/help"], session.History);
		Assert.True(session.IsOpen);
		Assert.Equal("", session.Input);
	}

	[Fact]
	public void Escape_ClosesWithoutExecuting()
	{
		var control = new StubControl();
		var session = CreateSession(control, new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("/help");

		Assert.True(session.Escape());
		Assert.False(session.IsOpen);
		Assert.Empty(control.Executed);
	}

	[Fact]
	public void History_UpAndDown_RestoresDraft()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("/help");
		session.Submit();
		session.SetInput("/players");
		session.Submit();

		Assert.True(session.PreviousHistory());
		Assert.Equal("/players", session.Input);
		Assert.True(session.PreviousHistory());
		Assert.Equal("/help", session.Input);
		Assert.True(session.NextHistory());
		Assert.Equal("/players", session.Input);
		Assert.True(session.NextHistory());
		Assert.Equal("", session.Input);
	}

	[Fact]
	public void CycleCompletion_CompletesCommandName()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [new CommandSuggestion("/help"), new CommandSuggestion("/whoami")], _ => null));
		session.Open();
		session.SetInput("/h");

		Assert.True(session.CycleCompletion());
		Assert.Equal("/help", session.Input);
	}

	[Fact]
	public void CycleCompletion_QuotesSpacedArgument()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [new CommandSuggestion("John Doe")], _ => null));
		session.Open();
		session.SetInput("/kick Jo");

		Assert.True(session.CycleCompletion());
		Assert.Equal("/kick \"John Doe\"", session.Input);
	}

	[Fact]
	public void CycleCompletion_NoCandidates_ReturnsFalseAndKeepsInput()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("/kick Jo");

		Assert.False(session.CycleCompletion());
		Assert.Equal("/kick Jo", session.Input);
	}

	[Fact]
	public void InsertChar_InsertsAtCursorAndMovesForward()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("abc", cursor: 1);

		session.InsertChar('X');

		Assert.Equal("aXbc", session.Input);
		Assert.Equal(2, session.Cursor);
	}

	[Fact]
	public void Backspace_DeletesBeforeCursor()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("abc", cursor: 2);

		Assert.True(session.Backspace());
		Assert.Equal("ac", session.Input);
		Assert.Equal(1, session.Cursor);
	}

	[Fact]
	public void MoveCursorLeftAndRight_AdjustsPosition()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("abc", cursor: 2);

		session.MoveCursorLeft();
		Assert.Equal(1, session.Cursor);
		session.MoveCursorRight();
		Assert.Equal(2, session.Cursor);
	}

	[Fact]
	public void BackspaceWord_DeletesWholeWordBeforeCursor()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("kick John", cursor: 9);

		Assert.True(session.BackspaceWord());
		Assert.Equal("kick ", session.Input);
		Assert.Equal(5, session.Cursor);
	}

	[Fact]
	public void DeleteWord_DeletesNextWordAfterCursor()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("kick John", cursor: 5);

		Assert.True(session.DeleteWord());
		Assert.Equal("kick ", session.Input);
		Assert.Equal(5, session.Cursor);
	}

	[Fact]
	public void MoveWordLeftAndRight_JumpsBetweenWords()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("kick John", cursor: 9);

		session.MoveWordLeft();
		Assert.Equal(5, session.Cursor);
		session.MoveWordRight();
		Assert.Equal(9, session.Cursor);
	}

	[Fact]
	public void SelectAll_HasSelectionAndSelectedText()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("help me");

		session.SelectAll();

		Assert.True(session.HasSelection);
		Assert.Equal("help me", session.SelectedText);
	}

	[Fact]
	public void InsertText_ReplacesSelection()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("hello", cursor: 5);
		session.SetSelection(0, 5);

		session.InsertText("bye");

		Assert.Equal("bye", session.Input);
		Assert.False(session.HasSelection);
	}

	[Fact]
	public void DeleteSelection_RemovesRange()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("hello world", cursor: 5);
		session.SetSelection(0, 5);

		Assert.True(session.DeleteSelection());
		Assert.Equal(" world", session.Input);
	}

	[Fact]
	public void MoveCursorRight_WithShiftExtendsSelection()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [], _ => null));
		session.Open();
		session.SetInput("abc", cursor: 1);

		session.MoveCursorRight(extendSelection: true);

		Assert.True(session.HasSelection);
		Assert.Equal("b", session.SelectedText);
	}

	[Fact]
	public void CycleCompletion_ReplacesTokenAtCursorPreservingSuffix()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [new CommandSuggestion("John Doe")], _ => null));
		session.Open();
		session.SetInput("/kick Jo rest", cursor: 7);

		Assert.True(session.CycleCompletion());
		Assert.Equal("/kick \"John Doe\" rest", session.Input);
	}

	[Fact]
	public void AcceptSuggestion_AppliesSpecificCandidateAndClearsList()
	{
		var session = CreateSession(new StubControl(), new StubCompletion(_ => [new CommandSuggestion("/help", "Show help")], _ => null));
		session.Open();
		session.SetInput("/h");

		Assert.True(session.AcceptSuggestion(new CommandSuggestion("/help", "Show help")));
		Assert.Equal("/help", session.Input);
		Assert.Empty(session.CompletionSuggestions);
	}

	[Fact]
	public void RealCompletion_HintForKnownCommand()
	{
		var (host, _) = TestNode.CreatePair(1001, 2001, 9001);
		var control = host.Services.GetRequiredService<ICommandControl>();
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();
		var session = new ConsoleInputSession(control, completion);

		session.Open();
		session.SetInput("/kick");

		Assert.Contains("steamId|displayName", session.Hint);
	}

	private static ConsoleInputSession CreateSession(ICommandControl control, ICommandCompletionSource completion) =>
		new(control, completion);

	private sealed class StubControl : ICommandControl
	{
		internal List<string> Executed { get; } = [];

		public IReadOnlyList<ConsoleLine> Lines => [];

		public bool TryExecute(string input)
		{
			Executed.Add(input);
			return true;
		}

		public void Clear()
		{
		}

		public void Dispose()
		{
		}
	}

	private sealed class StubCompletion(Func<string, IReadOnlyList<CommandSuggestion>> suggest, Func<string, string?> hint) : ICommandCompletionSource
	{
		private readonly Func<string, IReadOnlyList<CommandSuggestion>> _suggest = suggest;
		private readonly Func<string, string?> _hint = hint;

		public IReadOnlyList<CommandSpec> Commands => [];

		public IReadOnlyList<CommandSuggestion> Suggest(string input) => _suggest(input);

		public string? GetHint(string input) => _hint(input);
	}
}
