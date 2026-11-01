# Step 6 — make the failure visible before fixing it

Read this file and `06-red-first.local.md` completely before fixing a defect.

## Defects only

For a reported defect, the first artefact is a red test or a runtime probe that fails for the reported reason. Then the
fix turns it green.

## A compile error is not a red

The code not building proves nothing about the behaviour. A red is a check that would pass if the defect were absent and
fails on the current build.

## A cause is established by reading

Never by adding a log and re-running: a run costs a build, a deploy, a restart and a human at the keyboard. Read the
code, instrument through the running client's own eval channel, and spend a run on verifying the fix.

## Write the finding where it will be read

Every measured reading that changed a decision goes into the ticket with its instrument and its numbers, in the same
turn it was taken.
