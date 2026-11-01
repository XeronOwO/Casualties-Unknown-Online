# Step 5 — plan, then self-check the plan

Read this file and `05-plan.local.md` completely before writing code.

## Write the plan down

The plan names the files to touch, the tests that will prove the behaviour, and the order (red → implement → verify).
Large or user-visible changes are approved by the user first; routine detail is not.

## Self-check table

Before implementing, answer for each row of the acceptance matrix: which instrument judges it, and what would make it
fail. A row with no instrument is not ready — either build the instrument or say what is missing.

## Split what cannot finish in one verified unit

Stages each produce a verifiable result and their own commit. A stage that ends without one is recorded as failed.

## Keep a delivery checklist as you go

The checklist is filled in as steps complete, not reconstructed at the end. A delivery claim that has to be assembled
afterwards is usually a claim that was never verified.
