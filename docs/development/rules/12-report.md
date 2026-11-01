# Step 12 — close the item and talk to the person

Read this file and `12-report.local.md` completely before ending a work item.

## Talk like a person

First line: what this means for the reader — it works now / still broken / stuck where. Then a few plain sentences. No
log excerpts, no file paths, no commit ids, no gate names, no probe names, no field names, no counts of tests unless the
count is the answer.

Ask of every sentence: what can the reader decide after reading it? If the answer is nothing, the sentence is replaced
by the next tool call.

## The item's own records are updated

The ticket's status says what was judged with which reading; the index, census and any linked page move with it. A
ticket that cannot be finished keeps its open status and names the missing thing.

## Before claiming the whole objective complete

Run the completion gate and let it reach zero by rewriting each ticket's own status line with the reason nobody can do
it alone and the reading behind that reason. A summary is not evidence; the command's exit code is.
## The records move with the item

A finished item moves out of the working section with its index row, its census entry and every page that links it; a
ticket that keeps its open status says in its own status line what is still missing. User-reported problems are release
blockers: fixed until the exact reproduction is gone, not until the tests pass.

## Progress is judged rows and moved tickets

A cycle that only adds tooling, records a finding or writes a page has not advanced the backlog. Say what was judged and
what moved.
## A ticket's status answers one question

"Does any code still need writing?" Yes keeps it in the working list; no moves it to the judged list. There is no third
state, and a status invents none.