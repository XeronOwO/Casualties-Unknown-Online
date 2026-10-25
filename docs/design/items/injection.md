# Injection: putting medicine into a vein

Injecting a teammate through the medical operation.

## What the player does

Opens a patient's medical view with an injectable in hand, selects the injection, and injects. The player expects the
medicine to enter the patient's blood, with the game's own injection presentation, and the container to be spent by the
amount injected.

## What the game itself does

An injection is not the drag gesture's business: an injectable container's own use action draws blood rather than
injecting, so running it to find out what it does would be destructive. The injection runs through the game's own
injection path inside a medical operation the operator and the patient perform together.

## Rules and states

- The drag gesture refuses an injectable BY NAME and never dispatches an injection branch; the medical view is its only
  entry.
- The item is classified by the stacks it holds: an injectable liquid present makes it an injection; a container refilled
  with a non-injectable liquid is the drink family's, by its own data.
- The operation is performed by both clients: the patient's side produces the outcome, the operator's side supplies the
  input, and the container's remaining content is the item's own state afterwards.
- A patient who is dead cannot be injected; no consciousness is required.

## Refusals

| Condition | The player reads |
|---|---|
| an injectable arrived through the drag | `use.refused.injectable_needs_medical_view` |
| the item is not the operator's | `use.refused.not_yours` |
| no patient selected | `use.refused.no_patient_selected` |
| the item has no identity | `use.refused.no_network_identity` |

## Who judges

The host admits the operation; each step of the injection is the game's own, executed on the patient's client, with the
operator supplying the input. Nothing about the injection is predicted by the operator.

## What travels

The item's identity, the patient, and the operation's own frames. The container's remaining content travels back as the
item's state.

## What the player sees and hears

The injection plays the game's own animation and sound on the patient, and the container's fill level drops by the amount
injected on both clients.

## Records

Decision 246 (the item's own data separates the entries). Accepted:
`../../backlog/done/remote-fentanyl-injection-and-medical-panel-desync.md`.

## Gaps

- Which liquids count as injectable, and what each does to the patient, is the game's own table and is not tabulated here
  yet.
