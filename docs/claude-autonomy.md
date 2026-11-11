# EVE-O Custom — independent Claude Code worker (pilot v1)

The Linux host can run Claude Code without ChatGPT or Codex in the loop. The local host runner selects **owner-created, open issues with the label `claude-ready`**, creates an isolated git worktree from `origin/main`, invokes Claude Code, validates allowed file changes, then creates an **unmerged Draft PR**. It has a fixed per-run turn limit, estimated API budget cap and one-hour timeout.

**No automatic merging, publishing, permission modifications, or other repository administration.** The script creates no issue and has no self-initiated product roadmap. User/maintainer approval is still required to choose issues and merge results. Do not claim 100% unattended end-to-end software delivery.

## Dependencies and initial authentication (one time, Linux)

- Python 3.10+, `git`, `gh`, `claude`. The machine must access the private repo `Psykolic/eve-o-custom`.
- Authenticate as the repository owner (or a scoped automation account with permissions limited to writing branches and PRs): `gh auth login` and verify `gh auth status`.
- Authenticate Claude Code separately: `claude auth login`, `claude auth status`, and `claude doctor`. Provider accounts and API usage limits are separate from ChatGPT.
- Use a **dedicated unprivileged Linux account or sandbox** with no production credentials. Restrict access to GH credentials; the worktree alone is not a security boundary.
- Avoid `--dangerously-skip-permissions`; the runner uses `dontAsk`, approved edit/read tools, explicit denials for Bash/network/subagents and workdir restrictions. Check `claude --help` because flags and model/provider access can vary by installed version. Project/user settings or mods can affect effective permissions.
- Verify the repo root exists at `~/projetos/eve-o-custom`; override using `EVEO_SOURCE_REPO` if needed. The script itself is checked out from the development branch separately and does not have to be merged to `main` for a pilot.

## Pilot deployment from this Draft branch

```bash
cd ~/projetos/eve-o-custom
git fetch origin feature/claude-autonomy-runner-v1
git worktree add --detach ~/eveo-claude-autonomy origin/feature/claude-autonomy-runner-v1
python3 -m unittest discover -s ~/eveo-claude-autonomy/scripts/claude-autonomy/tests -v
```

Create a GitHub issue authored by `Psykolic`, with a narrow task touching product code, tests, or audit notes only. Add label `claude-ready` using the GitHub Issues UI or (once):

```bash
gh label create claude-ready --repo Psykolic/eve-o-custom --color 2EA44F --description "Owner-approved autonomous Claude task"
```

If the label already exists, skip creation. Before enabling the timer, run **dry-run**:

```bash
python3 ~/eveo-claude-autonomy/scripts/claude-autonomy/runner.py --dry-run
```

Then execute exactly one eligible issue:

```bash
python3 ~/eveo-claude-autonomy/scripts/claude-autonomy/runner.py --issue ISSUE_NUMBER
```

The runner refuses issues not opened by `Psykolic` or lacking `claude-ready`. It refuses path changes outside product code, tests and audit notes, and never runs commands supplied in issue text. All edits and agent output remain in `~/eveo-agent-workspaces`; private local logs are mode 0600. The runner does not retry a partially completed worktree automatically: inspect failures before manual cleanup.

## Enable the unattended user timer (after pilot succeeds)

```bash
mkdir -p ~/.config/systemd/user
cp ~/eveo-claude-autonomy/scripts/claude-autonomy/systemd/* ~/.config/systemd/user/
systemctl --user daemon-reload
systemctl --user enable --now eveo-claude-agent.timer
systemctl --user list-timers eveo-claude-agent.timer
journalctl --user -u eveo-claude-agent.service -n 100 --no-pager
```

The timer checks for tasks twice per hour. An empty queue does not call Claude. By default, user services may stop after logout; if you deliberately want operation without an active SSH session, arrange user lingering with the system administrator. Stop polling anytime:

```bash
systemctl --user disable --now eveo-claude-agent.timer
```

## Flow and review gates

1. Only issues owned by `Psykolic` and labeled `claude-ready` are eligible. Pick the smallest issue number first.
2. Host creates `claude/issue-N` from the current `origin/main`, with a fresh local worktree.
3. Claude edits the new worktree but cannot invoke its own shell, GitHub, network tools, or subagents through its allowed tools. Claude's settings are checked separately at runtime.
4. Host enforces a maximum of 12 changed files and 250 deleted lines. Only `src/Eve-O-Preview/`, `src/EveO.Tests/`, `docs/audits/` are allowed; no project files or symlinks. Violation stops before push.
5. Host commits, pushes the branch and opens a **Draft** PR. Windows CI tests and reviewer sign-off remain required before merge.
6. Any failure is visible in local logs; no destructive reset or automatic retry.
7. The PR remains Draft even if all checks pass. The agent cannot merge it.

### Known limitations and next phase

- This v1 provides bounded task execution, **not** a continuously self-planning AI organization. It does not discover and label issues, auto-fix CI, or autonomously approve its PRs.
- Windows-specific build/tests and 6+1 UI behavior cannot be confirmed solely from a Linux host; GitHub Actions and manual Windows test sessions remain mandatory.
- Claude CLI may not support specific flags, models or read/write sandbox settings in older versions. Confirm on the dedicated host before enabling the timer.
- For stronger isolation, run Claude in a container/VM under a different Unix user from the GitHub publishing controller, with a separate credential boundary; path checks alone cannot guarantee containment.
- PR #8 (skills) and PRs #3–5 (GDI) remain untouched. Until the shared skills are merged, the runner enforces its local POLICY.md rather than depending on skill discovery.
- Limit costs: the `--max-budget-usd 5.00` CLI cap estimates usage and can overshoot; verify Anthropic/provider billing separately.

## Safety checks

```bash
python3 -m py_compile ~/eveo-claude-autonomy/scripts/claude-autonomy/runner.py
python3 -m unittest discover -s ~/eveo-claude-autonomy/scripts/claude-autonomy/tests -v
```

No script here performs a merge, force push, branch deletion, EVE gameplay action or unsafe git reset.
