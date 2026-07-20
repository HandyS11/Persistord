# Persistord meta package — design

## Goal

Ship a code-less NuGet package named `Persistord` that consumers install to get the
full library-neutral stack — `Persistord.Core` + `Persistord.Messages` +
`Persistord.History` — in a single package reference.

This mirrors how `Microsoft.EntityFrameworkCore` bundles the EF Core model without
selecting a database provider. The meta package deliberately **excludes**
`Persistord.Adapters.DiscordNet`, so it stays neutral and never pulls in a Discord
client library. Consumers add an adapter separately when they need one.

## Scope

In scope:

- A new dependency-only project `src/Persistord/Persistord.csproj`.
- A packed `README.md` for the meta package.
- Solution (`Persistord.slnx`) registration.
- A `CD.yml` pack step.
- Root `README.md` updates (packages table + install instructions).

Out of scope:

- Any compiled code.
- A test project (a dependency-only package has nothing to unit-test).
- Changes to the existing `Core` / `Messages` / `History` / `Adapters.DiscordNet`
  packages.

## Components

### 1. `src/Persistord/Persistord.csproj`

A dependency-only SDK project with no `.cs` files. It carries the same packaging
metadata conventions as the sibling packages (`Authors`, `PackageLicenseExpression`,
`PackageReadmeFile`, `PackageTags`), plus the properties that make it a proper
meta package:

- `PackageId` = `Persistord`
- `TargetFramework` = `net10.0`
- `IsPackable` = `true`
- `IncludeBuildOutput` = `false` — the empty assembly is excluded from the package;
  it ships dependencies only.
- `NoWarn` includes `NU5128` — suppresses the "no lib/ref for the declared
  dependency group" warning that dependency-only packages emit. Required because the
  repo builds with `TreatWarningsAsErrors`.

It declares an explicit `ProjectReference` to **all three** packages — Core,
Messages, and History. On `dotnet pack`, project references to packable projects
become NuGet package dependencies, so the package page lists the full bundle
explicitly rather than burying Core and Messages as transitive dependencies of
History.

`Description` (example): "Meta package that bundles the library-neutral Persistord
stack — Core, Messages, and History — in a single reference."

`PackageTags` (example): `discord;efcore;persistence;meta`.

### 2. `src/Persistord/README.md`

Packed into the nupkg following the repo convention (`<None Include="README.md"
Pack="true" PackagePath="\" />`). Describes the package as the convenience bundle
for the library-neutral stack, lists what it pulls in, and notes that the
Discord.Net adapter is a separate opt-in install
(`dotnet add package Persistord.Adapters.DiscordNet`).

### 3. `Persistord.slnx`

Add `<Project Path="src/Persistord/Persistord.csproj" />` under the existing
`/src/` solution folder.

### 4. `.github/workflows/CD.yml`

Add a pack step alongside the existing three:

```yaml
- name: Pack NuGet Package Persistord
  run: cd ./src/Persistord/ && dotnet pack --configuration Release -p:Version=$VERSION
```

The existing wildcard upload / `dotnet nuget push ./**/*.nupkg` / GitHub release
steps pick up the new `.nupkg` automatically, so no other CD changes are needed.

### 5. Root `README.md`

- Add a `Persistord` row to the **Packages** table describing it as the bundle of
  Core + Messages + History.
- Show `dotnet add package Persistord` as the recommended single-line install,
  keeping the individual `dotnet add package Persistord.*` commands for consumers
  who want granular control.

## Verification

Packaging-only change; verified by inspecting the produced package rather than by
unit tests:

1. `dotnet pack src/Persistord --configuration Release` succeeds with no warnings
   (NU5128 suppressed).
2. The produced `Persistord.1.0.0.nupkg` `.nuspec` declares
   `Persistord.Core`, `Persistord.Messages`, and `Persistord.History` as
   dependencies.
3. The package contains no `lib/` assembly (build output excluded).
4. The full solution still builds: `dotnet build Persistord.slnx`.

## Decisions

- **Name:** `Persistord` (bare brand), matching the EF Core meta-package convention.
- **Bundle scope:** Core + Messages + History — library-neutral; the DiscordNet
  adapter is excluded to avoid committing the meta package to a Discord library.
- **Explicit references:** all three referenced directly for a clearer package page,
  rather than referencing only History and relying on transitive flow.
