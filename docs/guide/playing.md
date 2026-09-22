# Playing in a session

English | [中文](playing.zh.md)

A guest plays the same game with the same rules; what differs is where a decision is made. Your
machine performs your action immediately and reports it, and the host accepts that report first
unless it is an obvious conflict.

This page describes what a guest sees in a normal session. It is written from the player's point of
view; the design notes explain why the seams sit where they do.

## What you will notice

- Other players' characters move, carry, fall and die inside your world, and the mod labels who is who.
- Items you pick up, drop, cook or use are shared; a container another player opens shows the same contents to both of you.
- An arrow at the edge of the screen points at a teammate who is outside your view.
- The CUO panel (the interaction key is configurable, `F6` by default) shows the state of the session.

## Actions that happen fast

Shots, hits and quick pickups settle immediately on the machine where they happen and are reported
to the host afterwards. The host accepts the report first and corrects only an obvious conflict,
such as two players claiming the same item in the same instant; the side that lost the race is told
why instead of having its action silently dropped.

## When something looks wrong

If your character or an item snaps back, the host's copy disagreed with your report. This is
deliberate: the shared world stays consistent, and the correction is visible instead of the two
sides drifting apart unannounced. Repeated snapping is worth reporting together with a log.
