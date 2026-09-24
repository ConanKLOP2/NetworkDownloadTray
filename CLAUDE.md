@AGENTS.md

## Claude Code specifics

- `AGENTS.md` (imported above) is the source of truth for commands, architecture and invariants.
  Put project rules there, not here; keep this file for Claude-only workflow notes.
- Environment is Windows. PowerShell 5.1 is the primary shell (no `&&`; use `;` / `if ($?)`); Git Bash
  is also available. There is no `python` on PATH.
- Source files are CRLF: prefer the Edit tool for multi-line changes. `sed`/`perl` patterns that span
  `\n` silently fail to match, while single-line substitutions in the same command still apply.
- Put benchmark harnesses and throwaway projects in the session scratchpad, referencing the built
  `bin\Release\net10.0-windows\NetworkDownloadTray.dll`; never add them to the repo.
- For a baseline comparison, check out the old commit with `git worktree add --detach <scratch>/baseline <sha>`
  and remove it with `git worktree remove` when done.
- Parallel subagents: use `isolation: "worktree"`, one owner per file (see "Multi-agent work" in AGENTS.md).
  Integrate with `git diff <base>..<branch> | git apply -3`, then `git reset` so changes stay unstaged
  for review. Clean up with `git worktree remove` + `git branch -D worktree-agent-*` once committed.
- Before publishing into a folder the app runs from, check with `Get-Process NetworkDownloadTray`.
- Git: follow "Git workflow" in AGENTS.md — never commit on `master`/`main`; branch first, then open a PR
  (with the `gh` CLI) only when the user asks.
