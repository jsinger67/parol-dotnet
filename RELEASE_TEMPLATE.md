# Release Cut Template

Use this template for each new release.

## 1) Pick version

- Next version: `X.Y.Z`
- Tag: `vX.Y.Z`
- Release date: `YYYY-MM-DD`

## 2) Update files

- `src/Parol.Runtime/Parol.Runtime.csproj`
  - Set `<Version>X.Y.Z</Version>`
- `CHANGELOG.md`
  - Move entries from `Unreleased` to `## [X.Y.Z] - YYYY-MM-DD`

## 3) Local validation

```bash
dotnet restore Parol.Runtime.slnx
dotnet build Parol.Runtime.slnx -c Release
dotnet test Parol.Runtime.slnx -c Release
dotnet pack src/Parol.Runtime/Parol.Runtime.csproj -c Release -o artifacts
```

## 4) Commit and push

```bash
git add .
git commit -m "Release X.Y.Z"
git push
```

## 5) Tag and publish via GitHub Actions

```bash
git tag vX.Y.Z
git push origin vX.Y.Z
```

- This triggers `.github/workflows/nuget-publish.yml`.

## 6) Post-release checks

- Verify workflow success in GitHub Actions.
- Verify version appears on NuGet.org.
- Ensure package is listed (not unlisted).

## Optional: Hotfix re-tag (same version)

```bash
git tag -f vX.Y.Z
git push -f origin vX.Y.Z
```
