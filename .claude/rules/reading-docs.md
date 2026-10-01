# Reading the Project Docs

When the user asks you (or an agent you spawn) to "read the docs", "read the documentation", "check the docs", "đọc docs" or similar, it means reading **all** of the following, not only the file that seems most relevant.

## Included

| Path | Content |
|---|---|
| `docs/md/**` | Architecture and API design (`ARCHITECTURE.md`, `API_DESIGN.md`, and any new files) |
| `.claude/business-rules/**` | Business rules `BR-XX.md` and their index |
| `.claude/use-cases/**` | Use cases `UC-XX.md` and their index |
| `.claude/sequences/**` | PlantUML sequence diagrams `UC-XX.puml` and their index |
| `.claude/rules/**` | Project rules (including this file) |
| `.claude/skills/**` | Project skills — **except** `.claude/skills/frontend-design/` (see below) |
| Any other folder added under `docs/` or `.claude/` later | Treat it as documentation unless it is listed under "Excluded" |

Start each folder with its `README.md` index when it has one, then read the files it lists.

## Excluded

| Path | Why |
|---|---|
| `docs/docx/**` (e.g. `features.docx`) | Written for humans only. Never read, edit or regenerate these files unless the user names the file explicitly. |
| `.claude/skills/frontend-design/` | Only relevant when implementing or redesigning UI (views, `wwwroot`, CSS, components). Read it only for that kind of task. |
| `.claude/logs/`, `.claude/settings*.json` | Runtime and tool configuration, not documentation. |

## Rules

- When delegating to an agent, spell out these inclusions and exclusions in its prompt, so the agent does not depend on this rule being loaded.
- If the docs disagree with each other, do not choose silently: name the conflicting files and ask, or follow the `sync-docs` skill to fix them.
- Docs are written in English. Do not translate them.
