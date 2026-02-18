# Contributing

Thanks for contributing to `Parol.Runtime`.

## Development Prerequisites

- .NET SDK 10.0 or newer

## Local Setup

```bash
dotnet restore Parol.Runtime.slnx
dotnet build Parol.Runtime.slnx
dotnet test Parol.Runtime.slnx
```

## Pull Request Guidelines

- Keep changes focused and minimal.
- Add or update tests when behavior changes.
- Update `README.md` and `CHANGELOG.md` if user-visible behavior changes.

## Versioning and Releases

- Use semantic versioning for package versions.
- Document changes in `CHANGELOG.md` under `Unreleased`.
- Before release, move `Unreleased` entries into the new version section.
