# Repository validation

The repository follows the pinned Bonsai Foundation Prefect reference tree. Run `pwsh build/validate-prefect.ps1` to obtain Prefect and validate it.

There is one explicit template exception: `.github/workflows/Bonsai.Http.yml`. The reference workflow unconditionally runs Framework tests on Linux and tests a Windows framework that this package does not target. This repository's workflow runs Framework tests on Windows, .NET 8 tests on Windows/Linux/macOS, and a native Dear ImGui render check under Xvfb/Mesa on Linux. The validation script prepares a reference copy with only that workflow's content comparison disabled. All other Prefect rules are applied.

`SPEC.md` is an ignored local planning document and must never be staged or committed.
