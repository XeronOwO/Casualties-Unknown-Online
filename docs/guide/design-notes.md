# Design notes

English | [中文](design-notes.zh.md)

This page explains why the mod behaves the way it does. Nothing here is needed in order to play; it
is here because the behaviour makes more sense once the reason behind it is known.

CUO keeps one authoritative copy of the world and treats everything else — the Unity objects you
see, the network messages, the save files — as a view built from it. That single decision sits
behind most of the rules below.

## Judging happens where the action happens

A player's action is judged by that player's own view and own time, and then reported. The host does
not wait for a round trip before letting you act, and a slow connection is never the reason an
action is refused.

## The host arbitrates conflicts

Two players can still want the same thing in the same instant. The host settles that race — first
claim wins — tells the losing side why, and broadcasts the outcome so that every machine agrees
afterwards.

## Corrections are visible

When your view and the host's view disagree, the shared copy wins and your view is corrected. The
mod prefers a visible correction over a silent divergence, because a silent divergence becomes a bug
report nobody can reproduce.

## What this costs

Interactions that depend on split-second timing between two players are the hardest part, and some
presentation details are not synchronised at all. The [limitations](limitations.md) page lists what
is left out.
