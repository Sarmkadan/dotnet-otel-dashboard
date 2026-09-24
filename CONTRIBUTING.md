# Contributing

Thanks for your interest in dotnet-otel-dashboard.

## Workflow

1. Open an issue describing the bug or feature before starting large changes.
2. Fork, branch from `main` (`feat/<topic>` or `fix/<topic>`).
3. Make the change with tests. `dotnet build` must pass with zero warnings (warnings are errors).
4. Run `dotnet test`.
5. Open a PR. Describe what changed and why. Link the issue.

## Ground rules

- No Aspire dependencies. That is the whole point of the project.
- Keep the public API small; prefer internal types.
- Public members need XML docs.
- Follow `.editorconfig`; run `dotnet format` before pushing.
- Conventional commit messages: `feat:`, `fix:`, `docs:`, `test:`, `chore:`, `refactor:`.

## Reporting security issues

Do not open a public issue. Email the maintainer listed in the repository profile.

## License

By contributing you agree your contributions are licensed under the MIT license.
