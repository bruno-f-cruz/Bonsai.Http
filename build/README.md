# Repository validation

The repository follows the pinned Bonsai Foundation Prefect reference tree. Run `pwsh build/validate-prefect.ps1` to obtain Prefect and validate it.

The validation script prepares a reference copy with explicit content exceptions. `.github/workflows/Bonsai.Http.yml` runs Framework tests on Windows and .NET 8 transport tests on Windows/Linux/macOS instead of the reference's unsupported framework matrix. `docs/docfx.json`, `docs/articles/toc.yml`, and `docs/template/public/main.js` contain the package's downloadable example contract, guide navigation, and repository link. The site uses the standard Bonsai documentation styles without additional styling. Structural checks and all other Prefect rules remain enabled. The generated API sample uses the conventional `Extensions.csproj` project recognized by Prefect for workflow extensions.

`SPEC.md` is an ignored local planning document and must never be staged or committed.

To build and serve the documentation at `http://localhost:8090`, run `pwsh docs/serve.ps1`. It restores DocFX, generates the API reference, and builds the site with documentation warnings treated as errors. Use `-NoBuild` to serve an existing build or `-Port 8091` to choose another port. The exported workflow SVGs used by the guides are stored alongside the examples.
