# Release Checklist (NuGet.org)

Use this checklist before publishing a new `Parol.Runtime` version.

For a copy/paste routine, see `RELEASE_TEMPLATE.md`.

## 1) Update metadata and docs

- [ ] Confirm package version is updated
- [ ] Update `CHANGELOG.md`
- [ ] Verify `README.md` reflects current API and examples

## 2) Validate quality

- [ ] `dotnet restore Parol.Runtime.slnx`
- [ ] `dotnet build Parol.Runtime.slnx -c Release`
- [ ] `dotnet test Parol.Runtime.slnx -c Release`

## 3) Pack and inspect

- [ ] `dotnet pack src/Parol.Runtime/Parol.Runtime.csproj -c Release`
- [ ] Inspect `.nupkg` content (README included)

## 4) Publish

- [ ] `dotnet nuget push <path-to-nupkg> --source https://api.nuget.org/v3/index.json --api-key <NUGET_API_KEY>`
