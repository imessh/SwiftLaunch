# Contributing to SwiftLaunch

Thank you for your interest in contributing! Please read this guide before opening
a pull request or filing an issue.

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Branch Naming](#branch-naming)
3. [Commit Message Style](#commit-message-style)
4. [Pull Request Guidelines](#pull-request-guidelines)
5. [Coding Standards](#coding-standards)
6. [Reporting Bugs](#reporting-bugs)
7. [Feature Requests](#feature-requests)

---

## Getting Started

1. **Fork** the repository on GitHub.
2. **Clone** your fork locally:
   ```bash
   git clone https://github.com/your-username/SwiftLaunch.git
   cd SwiftLaunch
   ```
3. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
4. Build and run to confirm everything works before making changes:
   ```bash
   dotnet build
   dotnet run
   ```
5. Create a branch (see [Branch Naming](#branch-naming)) and make your changes.
6. Open a Pull Request against the `main` branch.

---

## Branch Naming

Use the following prefixes, followed by a short lowercase kebab-case description:

| Type | Pattern | Example |
|------|---------|---------|
| New feature | `feature/short-description` | `feature/file-search` |
| Bug fix | `fix/short-description` | `fix/stale-cache-rename` |
| Documentation | `docs/short-description` | `docs/update-readme` |
| Refactoring | `refactor/short-description` | `refactor/indexer-cleanup` |
| Performance | `perf/short-description` | `perf/faster-hot-cache` |
| Chore / tooling | `chore/short-description` | `chore/update-gitignore` |

---

## Commit Message Style

SwiftLaunch uses the [Conventional Commits](https://www.conventionalcommits.org/) specification.

### Format

```
<type>(<scope>): <short summary>

[optional body]

[optional footer(s)]
```

### Types

| Type | When to use |
|------|-------------|
| `feat` | A new feature |
| `fix` | A bug fix |
| `docs` | Documentation only |
| `style` | Formatting, whitespace (no logic change) |
| `refactor` | Code restructuring without behavior change |
| `perf` | Performance improvement |
| `test` | Adding or updating tests |
| `chore` | Build system, tooling, dependency updates |

### Scope (optional)

Use the file or subsystem affected: `indexer`, `launcher`, `hotkey`, `tray`, `startup`, `ui`, `readme`, etc.

### Examples

```
feat(indexer): add FileSystemWatcher for live rename detection
fix(cache): replace UPDATE on PK with INSERT OR REPLACE to prevent stale entries
docs(readme): add command syntax table and project structure
chore: update Microsoft.Data.Sqlite to 8.0.1
```

### Rules

- **Summary line** must be 72 characters or fewer.
- Use the **imperative mood** ("add", "fix", "remove" — not "added", "fixing").
- Do **not** end the summary line with a period.
- Separate the body from the summary with a blank line.
- Wrap the body at 80 characters.
- Reference issues in the footer: `Closes #42` or `Fixes #17`.

---

## Pull Request Guidelines

- **One concern per PR.** Don't bundle unrelated changes.
- **Target `main`** unless told otherwise.
- **Fill in the PR template** completely — description, motivation, testing steps.
- **Ensure the project builds** with no errors before submitting:
  ```bash
  dotnet build -c Release
  ```
- **No formatting-only PRs** unless they fix a real consistency problem — keep diffs readable.
- **Link related issues** in the PR description: `Closes #12`.
- A PR needs **at least one approving review** before it can be merged.
- Squash or rebase before merging to keep the history linear and clean.

---

## Coding Standards

- Follow the rules in `.editorconfig` — your editor should enforce them automatically.
- **Target .NET 8** — do not use APIs that require a higher version.
- **WPF + WinForms interop** — UI must remain on the Dispatcher thread; all indexing and file I/O must stay on background threads.
- **No blocking calls on the UI thread** — use `Task.Run`, `async/await`, or background threads for anything that touches the disk or network.
- **Nullable reference types are enabled** — annotate all new code properly; avoid `!` suppression unless genuinely safe.
- **Error handling** — swallow exceptions only when failure is non-fatal and the catch is documented. Never swallow exceptions silently in new code without a comment.
- **No magic strings** — extract constants for hotkey IDs, registry key paths, app names, and similar values.
- Keep methods **short and single-purpose**. If a method exceeds ~40 lines, consider splitting it.
- **No external dependencies** without prior discussion in an issue. Every new NuGet package adds to the published binary size.

---

## Reporting Bugs

Before filing a bug report, please:

1. Search [existing issues](../../issues) to avoid duplicates.
2. Reproduce the bug on the latest version.

When filing, include:

- **SwiftLaunch version** (check tray icon tooltip or binary properties)
- **Windows version** (`winver`)
- **Steps to reproduce** — exact steps, not a summary
- **Expected behavior**
- **Actual behavior**
- **Screenshots or screen recordings** if the issue is visual
- **Relevant logs** if any (SwiftLaunch does not currently write log files, but include any error dialogs)

Use the **Bug Report** issue template.

---

## Feature Requests

Feature requests are welcome. Please:

1. Search [existing issues](../../issues) first — your idea may already be tracked.
2. Open a new issue using the **Feature Request** template.
3. Describe:
   - **The problem you are solving** (not just the solution)
   - **Your proposed solution**
   - **Alternatives you considered**
   - **How important this is to you** (nice-to-have vs. blocking)

Feature requests that align with the [roadmap](README.md#future-roadmap) are more
likely to be picked up quickly. Feel free to implement a requested feature yourself
and open a PR — just comment on the issue first so effort isn't duplicated.

---

## Code of Conduct

Be respectful and constructive. Harassment of any kind will not be tolerated.
