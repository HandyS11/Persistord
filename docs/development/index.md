# Building & Contributing

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build & Test

```bash
dotnet restore
dotnet build
dotnet test
```

## Formatting

Formatting is enforced with ReSharper — **not** `dotnet format`. The tool is declared in
`.config/dotnet-tools.json` and must be restored before first use:

```bash
dotnet tool restore
dotnet jb cleanupcode Persistord.slnx --profile="ReformatAndReorder"
```

CI runs the same command on Linux and fails the build if the working tree is dirty
after cleanup, so run this locally before pushing.

## Mutation Testing (Local)

[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) is available
as a local tool. After restoring tools, run it from a test project directory:

```bash
dotnet tool restore
cd tests/Persistord.Core.Tests
dotnet stryker --config-file stryker-config.json --project Persistord.Core.csproj --reporter cleartext
```

Replace `tests/Persistord.Core.Tests` and `--project Persistord.Core.csproj` to target a
different module (e.g. `tests/Persistord.Messages.Tests` / `Persistord.Messages.csproj`).
Each library test project contains its own `stryker-config.json`.

The **full mutation suite** runs weekly (Mondays 03:00 UTC) and on manual dispatch in CI
via the Mutation workflow (`.github/workflows/Mutation.yml`).

## CI

The CI workflow (`.github/workflows/CI.yml`) runs on every push to `develop` and on all
pull requests. It builds and tests on both **Linux** and **Windows**:

- `dotnet restore` → `dotnet build --configuration Release` → `dotnet test`
- Formatting check (ReSharper, Linux only) — fails if any diff is produced
- Provider integration tests run as a separate job on Linux

### Quality analysis

The Sonar workflow (`.github/workflows/Sonar.yml`) runs on every push to `develop` and
performs:

- **SonarQube analysis** — static analysis and code-smell reporting.
- **Codecov coverage upload** — line and branch coverage from the `XPlat Code Coverage`
  collector, uploaded via `codecov/codecov-action`.

## Docs (This Site)

The documentation site is built with [DocFX](https://dotnet.github.io/docfx/) 2.78.5,
declared in `.config/dotnet-tools.json`.

Build locally:

```bash
dotnet tool restore
dotnet docfx docs/docfx.json
```

Build and serve locally (live-reload at `http://localhost:8080`):

```bash
dotnet docfx docs/docfx.json --serve
```
