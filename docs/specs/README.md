# Feature specs

One file per feature slice: `NNNN-slug.md`, written by the planner (Claude Code) with the
write-spec skill. Every spec starts with a challenge brief in chat: the planner checks the
idea for reasonableness, researches how real products solve it, proposes alternatives and
asks questions, and writes the file only after the user answers. Status moves `draft` → `approved` (set by a human) → `done`.
Implementers (Codex, Gemini) start only from approved specs and only on the tasks the
spec's task table assigns to them. Shipwise-Web agents read this folder from the sibling
backend checkout.

No specs yet.
