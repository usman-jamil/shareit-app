# CLAUDE.md

Guidance for working in this repository. This is an **Nx monorepo** (`@share/source`) that houses three cooperating projects:

- **`apps/api`** — the backend: a **.NET 10** **Clean Architecture** solution (minimal APIs, EF Core, hand-rolled CQRS).
- **`apps/cli`** — the goto CLI for autoamtion: a **.NET 10** solution (console app).
- **`apps/share-cli`** — the customer-facing CLI: a **.NET 10** console app on its own Clean Architecture backend under `libs/share-cli/`.
- **`apps/web`** — the frontend: a **React 19 + TypeScript** client-side-rendered (CSR) app built with Vite.
- **`libs/api/api-types`** — the seam between them: **auto-generated** TypeScript types derived from the API's OpenAPI contract.

The single most important thing this file does is tell you **where a given change belongs**. Decide which project owns the change _first_, then follow that project's local rules. Cross-cutting changes flow in one direction: **API → OpenAPI contract → `libs/api/api-types` → `apps/web`.**

---

## Monorepo layout — where things go

```
/ (repo root)
├── nx.json                     # Nx workspace config (plugins, target defaults, generators)
├── package.json                # JS toolchain + workspace scripts (serve/gen:types/lint)
├── tsconfig.base.json          # Base TS config + path aliases (e.g. @share/api-types)
├── share.slnx                  # .NET solution file (references apps/api + the libs/api/* class libraries)
├── Directory.Packages.props    # Central NuGet version management for the .NET solution
├── Directory.Build.props       # Shared MSBuild settings (lands here as the API grows)
├── eslint.config.mjs           # Root ESLint flat config
├── .prettierrc / .editorconfig # Formatting
│
├── apps/
│   ├── api/                    # .NET 10 Api host (apps/api/Api.csproj) — composition root  (Nx project: "api")
│   └── web/                    # React/TS CSR frontend                (Nx project: "web")
│   └── cli/                    # .Net 10 Cli                          (Nx project: "cli")
│   └── share-cli/              # .Net 10 customer-facing Cli (Share.Cli.csproj)  (Nx project: "share-cli")
│
├── libs/                       # class libraries, one folder per stack
│   ├── api/                    # backend owned by apps/api + apps/cli
│   │   ├── api-types/          # Generated OpenAPI → TS types         (Nx project: "api-types")
│   │   ├── domain/             # Domain.csproj — enterprise rules
│   │   ├── application/        # Application.csproj — use cases + abstractions
│   │   └── infrastructure/     # Infrastructure.csproj — technical implementations
│   │
│   ├── share-cli/              # backend owned exclusively by apps/share-cli
│   │   ├── api-types/          # Share.Api.Types.csproj — generated OpenAPI → Refit client
│   │   │                       #   (Nx project: "share-api-types")
│   │   ├── domain/             # Share.Domain.csproj          (Nx project: "share-domain")
│   │   ├── application/        # Share.Application.csproj     (Nx project: "share-application")
│   │   └── infrastructure/     # Share.Infrastructure.csproj  (Nx project: "share-infrastructure")
│   │
│   └── shared/                 # shared across every stack
│       └── shared-kernal/      # SharedKernel.csproj — building blocks (no dependencies)
│                               #   (Nx project: "shared-kernel")
│
└── tests/                      # test projects, mirroring libs/
    ├── api/                    # for the apps/api + libs/api stack
    │   ├── application-unit-tests/         # Application.UnitTests.csproj
    │   └── application-integration-tests/  # Application.IntegrationTests.csproj (Testcontainers)
    │
    └── share-cli/              # for the apps/share-cli + libs/share-cli stack
        ├── application-unit-tests/         # Share.Application.UnitTests.csproj
        └── infrastructure-unit-tests/      # Share.Infrastructure.UnitTests.csproj (Refit.Testing)
```

> **Convention:** every .NET **class library** lives under `libs/api/`, `libs/share-cli/` or `libs/shared/` (one folder per project); only executable hosts (`apps/api/Api.csproj`, `apps/cli/Cli.csproj`, `apps/share-cli/Share.Cli.csproj`) live under `apps/`. `share.slnx` references all of them.
>
> `libs/api/` and `libs/share-cli/` are **private to their own stack** — they never reference each other. `libs/shared/` is the only place a library both stacks may reference belongs.

Each project is an **Nx project** defined by its `project.json`. Nx targets (`serve`, `build`, `lint`, `test`, `generate-types`, …) are the canonical way to run work; see [Build, run, test](#build-run-test).

### Decision table — which project owns a change

| You are changing…                                     | It belongs in…                                             | Notes                                                                                    |
| ----------------------------------------------------- | ---------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| API behavior, endpoints, domain logic, persistence    | `apps/api` + `libs/api/*`                                  | Follow the Clean Architecture rules below.                                               |
| Customer-facing CLI behavior or its business logic    | `apps/share-cli` + `libs/share-cli/*`                            | Never reach into `libs/api/application`, `libs/api/domain` or `libs/api/infrastructure`. |
| What the customer-facing CLI is configured with       | `~/.shareit/config.yaml` via `share config set`            | The YAML file is the source of truth, not `appsettings.json`. Writes land in the active workspace. |
| What the customer-facing CLI looks like on screen     | `apps/share-cli/Rendering/`                                | Spectre.Console tables, prompts and progress bars. No rendering below `apps/`.            |
| The shape of a request/response the frontend consumes | `apps/api` **first**, then regenerate `libs/api/api-types` | The contract is owned by the backend; types are generated, never hand-edited.            |
| The API client the customer-facing CLI calls          | `apps/api` **first**, then regenerate `libs/share-cli/api-types` | Refitter-generated Refit client; never hand-edit `Generated.cs`.                         |
| UI, pages, components, client-side state, fetching    | `apps/web`                                                 | Self-contained; consumes `@share/api-types`.                                             |
| The generated API type definitions                    | **Do not edit by hand** — regenerate from the API          | See [The API contract pipeline](#the-api-contract-pipeline).                             |
| Shared frontend/TS code reused across web apps        | a **new `libs/shared/*`** project                          | Create with Nx generators; never reach into another app's `src`.                         |
| NuGet versions                                        | `Directory.Packages.props`                                 | Central Package Management — see .NET conventions.                                       |
| npm dependency versions                               | root `package.json`                                        | Single lockfile; the workspace is one npm install.                                       |

**Golden rule of the monorepo:** never create a runtime dependency from `apps/web` into `apps/api`'s source, or vice-versa. The _only_ thing they share is the generated contract in `libs/api/api-types`. If you find yourself wanting to import across that boundary directly, the answer is almost always "extend the OpenAPI contract and regenerate types."

---

## The API contract pipeline

`libs/api/api-types` is **generated output**, not source you edit. The flow is:

```
apps/api (Api host)
   │  emits OpenAPI document on build (OpenApiGenerateDocumentsOnBuild)
   ▼
apps/api/Api.json   ──or──   http://localhost:5080/openapi/v1.json   (running server)
   │  openapi-typescript
   ▼
libs/api/api-types/src/lib/schema.ts        # generated types — DO NOT hand-edit
   │  re-exported via
   ▼
libs/api/api-types/src/index.ts  ──►  imported in apps/web as  @share/api-types
```

Regenerate types whenever the API contract changes:

```bash
# Against a running API (serves the API, waits for OpenAPI, then generates):
npm run generate:api-types          # == nx run api-types:generate-types

# Against the build-time OpenAPI doc already on disk (apps/api/Api.json):
npm run gen:types:local
```

- `schema.ts` is **machine-generated** — if it's wrong, fix the API and regenerate, don't patch the file.
- Anything hand-written that _augments_ the generated types (helpers, narrowed aliases) goes in `libs/api/api-types/src/lib/api-types.ts` and is re-exported from `index.ts`.
- The path alias `@share/api-types` is defined in `tsconfig.base.json`. Import from the alias, never via a relative `../../libs/api/...` path.
- **If the Api host's location or port changes, update the generation commands** (`gen:types`, `gen:types:local`, `serve:api` in `package.json`, the `serve` target in `apps/api/project.json`, and the OpenAPI output path) so the pipeline keeps working.

---

## `apps/web` — React/TS frontend

A self-contained CSR React 19 app built with Vite.

- Source lives under `apps/web/src` (`main.tsx` bootstraps; feature code under `src/app/...`).
- **No project references** other than `@share/api-types`. It does not import from `apps/api` or other apps.
- Consume API types from `@share/api-types`; do not redeclare request/response shapes locally.
- Dev server runs on **port 3000**; the API runs on **port 5080**.
- Lint/test/build are driven through Nx (`nx <target> web`). Vite config is in `apps/web/vite.config.ts`; TS config extends `tsconfig.base.json`.
- **Type checking is `tsc -b`, not `tsc --noEmit`.** `apps/web/tsconfig.json` is a solution-style config — `"files": []` plus a reference to `tsconfig.app.json`, which is what actually holds the sources. A bare `tsc` there compiles **zero** files and exits 0, so it reports success without having checked anything; only build mode follows the reference. Keep `nx typecheck web` (and the `build` script) on `tsc -b`. Vite does not type-check at all — `nx build web` sets `skipTypeCheck`, so `typecheck` is the only thing standing between a type error and production.
- Add shared, reusable UI/logic as a **new `libs/shared/*`** project (via `nx g @nx/react:lib`) rather than growing cross-app imports.

---

## `apps/api` — .NET 10 Clean Architecture backend

> The API currently starts as a single minimal-API project (`apps/api/Api.csproj`, the **Api** host). As it grows it expands into the full Clean Architecture layout below: the **Api host stays at `apps/api/Api.csproj`**, and the inner layers become separate **class-library projects under `libs/api/`** (with `SharedKernel` in `libs/shared/`) — `libs/shared/shared-kernal/SharedKernel.csproj`, `libs/api/domain/Domain.csproj`, `libs/api/application/Application.csproj`, `libs/api/infrastructure/Infrastructure.csproj`. Test projects live under `tests/api/`. Several assets referenced here may not exist yet — place them as described when you add them. The `share.slnx` solution at the repo root references all of these projects.

This is a **Clean Architecture** solution built on **.NET 10**, minimal APIs, EF Core, and a hand-rolled CQRS dispatcher with cross-cutting behaviors implemented as decorators.

## `apps/cli` — .NET 10 Clean Architecture cli

> The CLI currently starts as a console application project (`apps/cli/Cli.csproj`, the **Api** host). As it grows it expands into the full Clean Architecture layout just like the `apps/api/Api.csproj` project.

This is a **Clean Architecture** solution built on **.NET 10**, minimal APIs, EF Core, and a hand-rolled CQRS dispatcher with cross-cutting behaviors implemented as decorators.

## `apps/share-cli` — .NET 10 customer-facing cli

The **customer-facing** CLI (`apps/share-cli/Share.Cli.csproj`, namespace `Share.Cli`, assembly/command name `share`). It is built with ConsoleAppFramework and hosted on `Microsoft.Extensions.Hosting`, exactly like `apps/cli`, and everything it puts on screen goes through **Spectre.Console** (see [`apps/share-cli/Rendering`](#apps-share-cli-rendering--everything-the-user-sees) below).

Unlike `apps/cli`, it does **not** sit on the `libs/api/*` backend. It has its own Clean Architecture stack under `libs/share-cli/`:

| Project (path)                                                                 | Namespace              | Depends on                                  |
| ------------------------------------------------------------------------------ | ---------------------- | ------------------------------------------- |
| `Share.Domain` (`libs/share-cli/domain/Share.Domain.csproj`)                         | `Share.Domain`         | `SharedKernel`                              |
| `Share.Application` (`libs/share-cli/application/Share.Application.csproj`)          | `Share.Application`    | `Share.Domain`, `SharedKernel`              |
| `Share.Api.Types` (`libs/share-cli/api-types/Share.Api.Types.csproj`)                | `Share.Api.Types`      | `Refit` only — no project references        |
| `Share.Infrastructure` (`libs/share-cli/infrastructure/Share.Infrastructure.csproj`) | `Share.Infrastructure` | `Share.Application`, `Share.Api.Types`      |
| `Share.Cli` (`apps/share-cli/Share.Cli.csproj`)                                | `Share.Cli`            | `Share.Application`, `Share.Infrastructure` |

- The **same golden rules apply**: dependencies point inward only, all abstractions live in `Share.Application/Abstractions`, implementations in `Share.Infrastructure`, vertical slices per use case (`libs/share-cli/application/<Feature>/<UseCase>/`), handlers return `Result`/`Result<T>`.
- `libs/shared/shared-kernal` (namespace `SharedKernel`) is the **one** thing both stacks share — it is dependency-free primitives (`Result`, `Error`, `Entity`, `IDateTimeProvider`). Nothing else crosses between `libs/api/*` and `libs/share-cli/*` in either direction.
- The CQRS interfaces are duplicated per stack on purpose: use `Share.Application.Abstractions.Messaging.*` here, never `Application.Abstractions.Messaging.*`.
- Commands live in `apps/share-cli/Commands/<Feature>Commands.cs` and are registered in `Program.cs` via `consoleApp.Add<T>()`. Keep them thin: resolve the handler from a scope, invoke it, render the result.
- `Ping` (`libs/share-cli/application/Ping/` + `apps/share-cli/Commands/PingCommands.cs`) is a placeholder slice proving the wiring. The first real use case (`share create`) has landed, so it is now safe to delete.

### `apps/share-cli/Rendering` — everything the user sees

Terminal output is **Spectre.Console**, and it lives in `apps/share-cli/Rendering/` — the only place in the stack that knows what a table or a prompt is.

| Piece                                                       | What it is                                                              |
| ----------------------------------------------------------- | ----------------------------------------------------------------------- |
| `ConsoleOutput`                                             | The one way to write: `Fail`/`Warn`/`Success`, the `Fields()` grid, byte formatting |
| `ConfigurationView`                                         | `config show` as a field list, `config list` as a table                 |
| `ConfigPrompts` + `PromptedWorkspace`                       | The questions `config create` and `config activate` ask                 |
| `UploadProgressDisplay`                                     | `share create`'s progress bar, an `IUploadProgressReporter`             |

- **Escape anything a user supplied** before it goes into markup — `ConsoleOutput.Value`/`Muted`/`Label` do it, and `Markup.Escape` is the manual form. A file path containing `[` is otherwise read as a style tag and either vanishes or throws.
- **Failures go to stderr**, through `ConsoleOutput.Fail`, which returns the exit code so a command can `return ConsoleOutput.Fail(error)`. `AnsiConsole` writes to stdout, so it must not be used for them. `config path` is the one command that writes plain `Console.WriteLine` — its output is meant to be pasted into another command.
- **Two levels of "is there a human here", and they are not the same.** `ConsoleOutput.IsInteractive` means something can be typed (a text prompt is safe); `ConsoleOutput.CanRedraw` also requires ANSI, which a selection list and a live progress bar both need. Spectre **throws** for a selection list on a terminal that cannot be drawn on, so check `CanRedraw` before showing one.
- **A command given all its arguments never prompts.** Prompting is what happens when an optional `[Argument]` was omitted _and_ there is a terminal; without one it fails with `Configuration.WorkspaceNameRequired` rather than hanging. That is what keeps every command scriptable.

### `libs/share-cli/api-types` — the CLI's generated API client

`Share.Api.Types` is **generated output**, not source you edit. Refitter turns the API's OpenAPI document into a Refit interface (`IApiv1`) plus its DTOs in `libs/share-cli/api-types/Generated.cs`:

```
apps/api  ──emits──▶  http://localhost:5080/openapi/v1.json
   │  refitter --settings-file .refitter   (config at repo root)
   ▼
libs/share-cli/api-types/Generated.cs      # namespace Share.Api.Types — DO NOT hand-edit
```

```bash
npm run generate:refitter-types   # serve API + regenerate (start-server-and-test)
npm run gen:types:refitter        # regenerate against an already-running API
```

It lives in its own project — not in `Share.Domain`, `Share.Application` or `SharedKernel` — because:

- it carries a hard `Refit` dependency, and the domain/application layers stay technology-agnostic;
- it is machine-generated, so it gets analyzers and style enforcement switched off (`AnalysisMode=None`, `SonarQubeExclude`) — settings we do **not** want leaking onto hand-written layers;
- `SharedKernel` is shared by _both_ stacks and must stay dependency-free; this contract belongs only to the CLI stack.

Rules:

- **`Share.Application` must never reference `Share.Api.Types`.** The Application layer talks to the API through `IShareApiClient` (see below).
- `Share.Infrastructure` is the only project that references it: it consumes `IApiv1`, and **maps** the generated DTOs to Application models, translating transport failures into `Result.Failure(...)`.
- If the generated shape is wrong, fix the API endpoint and regenerate. Never patch `Generated.cs`.
- `.refitter` sets `useCancellationTokens`, so a regenerated `IApiv1` takes a trailing `CancellationToken cancellationToken = default` on every method. When that lands, pass the token through in `ShareApiClient` and drop its `WaitAsync` workaround (see the remarks on that class).

### `IShareApiClient` — how the CLI calls the API

The seam between the CLI's use cases and HTTP:

| Piece                                                           | Lives in                                                            |
| --------------------------------------------------------------- | ------------------------------------------------------------------- |
| `IShareApiClient` + its request/response models                 | `libs/share-cli/application/Abstractions/Api/`                            |
| `ShareApiClient` (adapter), mapping, error translation, options | `libs/share-cli/infrastructure/Api/` + `libs/share-cli/infrastructure/Options/` |
| `ShareApiErrors`, `ShareStatus`                                 | `libs/share-cli/domain/Api/`, `libs/share-cli/domain/Shares/`                   |

- **Handlers depend on `IShareApiClient`, never on `IApiv1`.** One method per API operation, named for the CLI's usage rather than the route (`CreateShareAsync`, not `SharesPost`).
- **Every method returns `Result`/`Result<T>`.** The adapter unwraps the API's `Result` envelope, converts ProblemDetails responses back into `Error`s with the right `ErrorType` (400 → `Validation` incl. the `errors` extension, 404 → `NotFound`, 409 → `Conflict`, 401/403 → `ShareApiErrors.Unauthorized()`), and turns dead connections and timeouts into `ShareApiErrors.Unreachable`/`Timeout`. Only `OperationCanceledException` from the caller's own token propagates.
- **Uploading is a three-step conversation** — `CreateShareAsync` → PUT bytes to each presigned `FileUploadUrl` → `FinalizeShareAsync`. Step 2 goes straight to object storage and is deliberately _not_ on this interface: it has its own abstraction, `IFileUploader` (see below). Do not widen `IShareApiClient` to cover it — that would send the CLI's API key to a third party.
- Registration is `AddRefitGeneratedClient<IApiv1>()` (not `AddRefitClient`) — the generated interface is fully source-generated, so this avoids Refit's reflection request builder. `ApiKeyHeaderHandler` attaches `X-Api-Key` to every request.
- Configuration is the `ShareApi` section (`BaseUrl`, `ApiKey`, `TimeoutSeconds`), whose source of truth is the user's YAML file (see below). It is **not** validated at startup, so `share --help` works unconfigured; a missing key comes back as a `ShareApi.Unauthorized` failure result. Keep the key out of `appsettings.json` — use `share config set --api-key`, user secrets, or the `ShareApi__ApiKey` environment variable. Note that the YAML file wins over all of them (see precedence below), and the options are bound once at startup, so a key set in one process takes effect from the next command onwards.

### `share create` — the upload use case

`share create [--path <dir>] [--user-id <id>] [--ttl-minutes <n>]` shares a whole folder. The slice is `libs/share-cli/application/Shares/Create/`, and its handler is the only place the three-step conversation is sequenced.

| Piece                                           | Lives in                                        |
| ----------------------------------------------- | ----------------------------------------------- |
| `IFileScanner`, `LocalFile`, `ScannedDirectory` | `libs/share-cli/application/Abstractions/FileSystem/` |
| `IFileUploader`                                 | `libs/share-cli/application/Abstractions/Storage/`    |
| `IUploadProgressReporter`, `NullUploadProgressReporter` | `libs/share-cli/application/Abstractions/Progress/` |
| `FileScanner`, `ContentTypes`                   | `libs/share-cli/infrastructure/FileSystem/`           |
| `PresignedFileUploader`, `ProgressReportingStream` | `libs/share-cli/infrastructure/Storage/`           |
| `UploadProgressDisplay`                         | `apps/share-cli/Rendering/`                           |
| `ShareErrors`                                   | `libs/share-cli/domain/Shares/`                       |

- **The handler owns the sequence, not the I/O.** It resolves the owner, scans, calls `CreateShareAsync`, uploads each file, then calls `FinalizeShareAsync`. Every step is a `Result` check — nothing throws.
- **Everything under the folder is included**, recursively, hidden files and dotted directories too. Relative paths are normalised to forward slashes in `FileScanner` so a share created on Windows reads the same everywhere, and the file list is sorted so runs are reproducible.
- **Upload targets are matched to local files by relative path, never by position** — the API makes no promise about ordering. A file with no matching target fails with `Share.MissingUploadUrl`.
- **Uploads run one at a time and stop at the first failure**, and nothing is rolled back: a share that is created but never finalized stays `pending` and expires on its own. Do not add server-side cleanup the CLI does not own.
- **`PresignedFileUploader` gets its own `HttpClient`**, registered in `Infrastructure/DependencyInjection.cs` _without_ `ApiKeyHeaderHandler` and with no timeout — presigned URLs point at object storage, the API key must not travel there, and an upload takes as long as the file is (cancellation stops it). Keep both properties if you touch that registration.
- **The owner comes from `--user-id`, falling back to `userId` in the active workspace of the configuration file.** Unlike the other settings this one is read through `IConfigurationStore` rather than `IOptions`, so it is file-only — no `ShareApi__UserId` environment variable. The API takes `OwnerUserId` explicitly today; if it ever derives the owner from the API key, this fallback is the thing to delete.
- The API's manifest carries sizes as a 32-bit value, so a single file over `int.MaxValue` bytes fails with `Share.FileTooLarge` before anything is sent.
- **Progress is carried on the command, not injected.** `CreateShareCommand.Progress` is an optional `IUploadProgressReporter`; `ShareCommands` supplies `UploadProgressDisplay` for the length of one `AnsiConsole.Progress()` block, and the handler falls back to `NullUploadProgressReporter`. A display belongs to one invocation, so it is passed in rather than registered — do not turn it into a DI service with mutable state.
- **The bar is measured in bytes, and counted where the bytes leave the file.** `ProgressReportingStream` wraps the `FileStream` and reports a running total, which is why a 2 GB video does not tick past at the same rate as a README. That stream **must stay seekable and keep reporting `Length`**: `StreamContent` derives `Content-Length` from them, and a presigned PUT that arrives chunked instead is rejected. It counts bytes handed to the socket, so a file small enough to fit the send buffer reads as complete before it has arrived — only the `Result` says it landed.
- **No terminal, no bar.** `ShareCommands` checks `ConsoleOutput.CanRedraw` and otherwise runs the same handler with no reporter, so piping the command into a file does not fill it with escape codes.

### `share update` — the self-update use case

`share update [--check] [--version <v>] [--yes]` replaces the running binary with a build from the repository's GitHub releases. `--version` moves to any published release, including a lower one — a downgrade is carried out as asked.

| Piece                                                                             | Lives in                                        |
| --------------------------------------------------------------------------------- | ----------------------------------------------- |
| `SemanticVersion`, `UpdateErrors`, `UpdateDefaults`, `ReleasePackaging`, `Sha256Sums` | `libs/share-cli/domain/Updates/`                      |
| `IApplicationEnvironment`, `IReleaseCatalog`, `IUpdatePackageInstaller`, `IUpdateProcessLauncher` | `libs/share-cli/application/Abstractions/Updates/`    |
| `Check` / `Apply` / `Install` slices, `UpdaterCommandLine`                        | `libs/share-cli/application/Updates/`                 |
| `GitHubReleaseCatalog`, `UpdatePackageInstaller`, `UpdateProcessLauncher`, `ApplicationEnvironment`, `ArchiveExtractor`, `UpdateWorkspace` | `libs/share-cli/infrastructure/Updates/`              |
| `UpdateCommands`                                                                  | `apps/share-cli/Commands/`                      |

- **It is two processes, and that is not incidental.** No process can overwrite the file it is running from. `share update` (the `Apply` slice) resolves the release, starts a clone of itself in a temp directory with the hidden `update-apply` command, and exits; the clone (the `Install` slice) waits for that exit, downloads, verifies, and swaps. Both ends agree on the command line through `UpdaterCommandLine` — change the constants and the receiving parameter names in `UpdateCommands.UpdateApply` together.
- **The clone is a straight file copy of the executable**, which is only sound for a published single-file build. `IApplicationEnvironment.IsReleaseBuild` decides that by looking for `share.dll` beside the host, and a development build is refused with `Update.NotSelfUpdatable` rather than half-replaced. `--check` still works everywhere.
- **A download is never installed unverified.** `SHA256SUMS.txt` is fetched _before_ the archive, and a mismatch fails with `Update.ChecksumMismatch` and installs nothing. There is no flag to skip it — do not add one.
- **The swap is a rename inside the target's own directory**, so it is atomic and an interrupted update leaves either the old binary or the new one. The old file's Unix mode is carried onto the replacement. On Windows a still-locked target is renamed to `.old-<id>` first, and the original is put back if the second move fails.
- **`ReleasePackaging` is the one copy of the release-archive naming** (`share-<version>-<rid>.tar.gz|zip`, `SHA256SUMS.txt`). It mirrors the `Package` step of `.github/workflows/release-share-cli.yml`; if that changes, change it here and nowhere else.
- **The runtime identifier is composed, not read from `RuntimeInformation.RuntimeIdentifier`** — it has to be one of the six the workflow publishes, and anything else must come back as `Update.UnsupportedPlatform` rather than as a 404. It is keyed off the _process_ architecture, so an x64 build under Rosetta stays x64.
- **`GitHubReleaseCatalog` lists releases and filters them**, rather than using `/releases/latest`: that endpoint answers for the repository, not for the `sharecli-` tag prefix. `GetLatestAsync` returns stable releases only; a prerelease is reachable only by naming it with `--version`. Matching is on the parsed version, so `sharecli-1.3.2` and `sharecli-v1.3.2` are both found by asking for `1.3.2`.
- **Both HTTP clients are registered without `ApiKeyHeaderHandler`** — they talk to GitHub, and the Share API key must not travel there. The archive client has no timeout, for the same reason `PresignedFileUploader` has none.
- **Confirmation lives in `UpdateCommands`, not in a handler.** `--yes` never reaches the Application layer; a non-terminal stdin without `--yes` fails rather than assuming consent.
- Repository coordinates are `UpdateDefaults` in the domain, bindable through the `Update` options section for a fork or a test environment. Nothing has to be configured to use it, and it is deliberately not part of `~/.shareit/config.yaml`.
- The updater's clone cannot delete the file it is executing, so it is left in `<temp>/share-cli-update/` and swept by the next run. That is what `UpdateWorkspace.Sweep()` is for.

### `tests/share-cli` — testing the CLI stack

Two unit-test projects, mirroring `tests/api`. Both are xUnit v3 on the Microsoft Testing Platform with Shouldly, and both are registered in `share.slnx`.

| Project (path)                                                            | Tests                                   | Fakes the API with                 |
| ------------------------------------------------------------------------- | --------------------------------------- | ---------------------------------- |
| `Share.Application.UnitTests` (`tests/share-cli/application-unit-tests/`)       | Use-case handlers and validators        | NSubstitute over `IShareApiClient` |
| `Share.Infrastructure.UnitTests` (`tests/share-cli/infrastructure-unit-tests/`) | `ShareApiClient`, `ApiKeyHeaderHandler`, the update infrastructure, the configuration file and its workspaces | `Refit.Testing`'s `StubHttp`, `StubRoutedHandler`, a real file under `SHARE_CLI_CONFIG` |

- **Handler tests never touch HTTP.** Take an `IShareApiClient` from `ShareApiClientSubstitute.Create()` (every operation succeeds with `ShareApiData`), then re-arrange the one call the test is about with `FailsGetUser`/`FailsCreateShare`/`FailsFinalizeShare`/`FailsGetShare`. The update handlers follow the same convention through `UpdateSubstitutes`/`UpdateData` (`FailsGetLatest`, `FailsStage`, `FailsReplace`, `FailsStart`, `FailsWait`).
- **Progress is asserted as a sequence, not as counters.** `RecordingUploadProgressReporter` writes each call down as a line, so a test pins the whole order (`starting`, then `start`/`done` per file). Order is the property that matters — a file reported complete before it started is a bar that jumps about — and nothing in `apps/share-cli/Rendering` is unit-tested, so this is where the reporting contract is held.
- **Adapter tests stub the socket, not the client.** `StubHttp` is a route table (`Route.Get("/shares/{shareId}")` → `Reply.With(...)`/`Reply.Json(...)`/`Reply.Status(...)`) handed to `http.CreateGeneratedClient<IApiv1>(baseUrl)`, so the real generated Refit client, its serializer and Refit's exception behaviour are all exercised. That is where each `Error` mapping (404, 409, 400 + `errors`, 401/403, unreachable, failed envelope) is pinned.
- **The update infrastructure is not Refit**, so it stubs the socket with `StubRoutedHandler` (routes by absolute URL) instead. `UpdatePackageInstallerTests` builds a real gzipped tar in the test and serves it, so the download, the SHA-256 check and the unpacking are all the production code path. `UpdateProcessLauncher` is covered only for waiting — starting it would clone and run the test host, so the launch path is proven by publishing the CLI and running a real update instead.
- Wire payloads are built from the **generated contract types** in `ShareApiResponses`, so a regenerated `Generated.cs` breaks the build rather than letting the tests drift. Only the ProblemDetails bodies are raw JSON — the API composes those itself and they are not in the generated client.
- Handlers, validators and the adapter are `internal`, so each library grants `InternalsVisibleTo` to its test project. Keep that pairing when adding projects.

```bash
nx test Share.Application.UnitTests
nx test Share.Infrastructure.UnitTests
dotnet test --project tests/share-cli/application-unit-tests/Share.Application.UnitTests.csproj
```

### Configuration — `~/.shareit/config.yaml` is the source of truth

The CLI reads its settings from a YAML file in the user's home directory. Each root-level
section is a **workspace** — one complete set of settings for one server — and
`active_workspace` picks the one in force:

```yaml
# <user home>/.shareit/config.yaml
active_workspace: development

shareApi: # the default workspace; the section the file has always had
  baseUrl: https://api.example.com
  apiKey: sk_live_...
  userId: 11111111-1111-1111-1111-111111111111
  timeoutSeconds: 45

development:
  baseUrl: https://dev.example.com
  apiKey: sk_test_...
```

```bash
share config show                              # effective values of the active workspace
share config list                              # every workspace as a table: name + base URL
share config create development                # add an empty workspace and make it active
share config create                            # ask for name, base URL, API key and user id
share config activate shareApi                 # point the CLI at another workspace
share config activate                          # pick one from a list
share config set --base-url https://api.example.com --timeout-seconds 45
share config set --api-key sk_live_...         # or -k
share config set --user-id <id>                # or -i; the owner `share create` uses
share config path                              # where the file is
```

- **Every read and write acts on the active workspace, and only `activate` changes which that is.** `show`, `set` and `share create` never name a workspace, so switching servers is one command and nothing else has to know workspaces exist.
- **`create` and `activate` ask only for what they were not told.** Given a name, both behave exactly as they always have — `create <name>` still makes an *empty* workspace and prompts for nothing, which is the form scripts use. Given none, `create` asks for the four settings and `activate` shows a list. See `apps/share-cli/Rendering` for the terminal checks that keep both safe to script.
- **A prompted `create` writes once.** The settings travel on `CreateWorkspaceCommand.Settings` and the handler creates the workspace and then saves into it, rather than the command layer calling `create` and then `set` — a half-configured workspace should not be reachable. If the second write fails the empty workspace is left in place for `config set` to finish, deliberately.
- **`config list` reads every workspace's `baseUrl` but nothing else** — `WorkspaceList` carries `WorkspaceSummary` (name + base URL as written, unparsed). It is the one operation that reads across all workspaces at once, so it must never grow a secret; and it must not validate, because seeing a URL that is wrong is the point.
- **The default workspace is `shareApi`** (`ConfigurationWorkspaces.DefaultName`) and always exists, whether or not the file has a section for it. A file written before workspaces landed is therefore already a valid one-workspace file — there is no migration.
- **A file naming an `active_workspace` it does not define is a read failure** (`Configuration.WorkspaceNotFound`), not a fall back to defaults: defaulting would quietly aim the next command at localhost with no key. `config list` is the exception and still succeeds, because it is how the user diagnoses that file.
- **`create` refuses to overwrite an existing workspace and `activate` refuses to invent one.** Creating implicitly on `activate` would hide a typo behind a set of silently defaulted settings.
- **Path resolution** is `libs/share-cli/infrastructure/Configuration/CliConfigurationPath.cs`: `Environment.SpecialFolder.UserProfile` + `Path.Combine`, so it lands on `C:\Users\<name>\.shareit\config.yaml`, `/Users/<name>/.shareit/config.yaml` and `/home/<name>/.shareit/config.yaml` with no OS-specific code. `SHARE_CLI_CONFIG` overrides the whole path — **use it in tests** so they never touch the developer's real file.
- **Precedence:** `AddShareCliConfigurationFile()` is registered **last** in `Program.cs`, so the YAML file beats `appsettings.json`, user secrets and environment variables. That is what "source of truth" means here — do not reorder it.
- **Defaults live in one place**, `Share.Domain.Configuration.ShareApiDefaults`. Both `ShareApiOptions` and `config show` read from it, so there is no second copy to drift.
- **The file is optional and never fatal.** A missing file means everything defaults. A malformed file is reported on stderr and _ignored_ rather than crashing startup — otherwise no command, not even `config path`, could run to fix it. `config show` then reports the precise parse error.
- **`config set` merges**: it rewrites only the keys you pass in the active workspace, preserves every other workspace and any unrelated keys, writes via a temp file + move, and chmods `700`/`600` on Unix. It refuses to overwrite a file it cannot parse (`Configuration.Unparseable`) rather than discarding hand-written content. Comments are not preserved.
- **The API key is write-only.** `share config set --api-key` is the way to store it; nothing ever reads it back out to the user. `ShareApiSettings.ApiKey` leaves the store only to be written straight back to the file — `ConfigurationResponse` carries `ApiKeyIsSet` (a bool), `config show` prints `set`/`not set`, and no validator or `ConfigurationErrors` message interpolates the value. Keep it that way when adding settings that are secrets. There is no way to _clear_ the key from the CLI; delete the line from the file. A blank key is rejected rather than written.
- **Reading and writing** go through `IConfigurationStore` (`libs/share-cli/application/Abstractions/Configuration/`), implemented by `YamlConfigurationStore`. The `Get`/`Set`/`List`/`Create`/`Activate` use cases are ordinary CQRS slices under `libs/share-cli/application/Configuration/`. `config path` is the one command that bypasses a handler — it must keep working when the file is unreadable.
- **The file's shape is known in exactly one place**, `WorkspaceDocument` — the store writes through it and the configuration provider reads through it, so the two can never disagree about which section is in force. The `Section:Key` flattening `IConfiguration` needs is `YamlConfigurationParser`, applied to the node `WorkspaceDocument` hands back.
- **The provider surfaces the active workspace only**, remapped onto the `ShareApi` section that `ShareApiOptions` binds. The inactive workspaces are deliberately absent from `IConfiguration`: nothing binds them, and keeping them out means their API keys never reach it.
- **Workspace names must be usable as both a YAML key and an `IConfiguration` section**: start with a letter, then letters, digits, `-` or `_`. They are matched case-insensitively, the way `IConfiguration` matches sections, so `Development` and `development` are one workspace rather than two that shadow each other.

### Golden rules

The dependency direction is the most important invariant. **Dependencies point inward only:**

```
Api ──▶ Infrastructure ──▶ Application ──▶ Domain ──▶ SharedKernel
                                    │                         ▲
                                    └─────────────────────────┘
```

- `Domain` depends only on `SharedKernel`.
- `Application` depends on `Domain` + `SharedKernel`. **It must NOT reference `Infrastructure` or `Api`.**
- `Infrastructure` depends on `Application` (and inward). It implements abstractions the Application defines.
- `Api` is the composition root — it references `Infrastructure` and wires everything together at startup.

These rules are enforced by `tests/api/architecture-tests` (`ArchitectureTests`). **If you add a project reference that points outward, those tests fail by design — fix the design, not the test.**

### Project layout & what belongs where

The class-library layers live under `libs/api/` (one folder per project), except `SharedKernel`, which lives in `libs/shared/shared-kernal/` because both the API and the `share-cli` stacks depend on it; the Api host is `apps/api/Api.csproj`; tests live under `tests/api/`.

| Project (path)                                                     | Responsibility                       | Allowed to contain                                                                                                                                                        |
| ------------------------------------------------------------------ | ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SharedKernel` (`libs/shared/shared-kernal/SharedKernel.csproj`)   | Building blocks shared by all layers | `Result`/`Error`, `Entity` base, `IDomainEvent`, `IDateTimeProvider`, primitives. No dependencies.                                                                        |
| `Domain` (`libs/api/domain/Domain.csproj`)                         | Enterprise rules                     | Entities, value objects, enums, domain events, **static `*Errors` classes**. No EF, no I/O, no framework references.                                                      |
| `Application` (`libs/api/application/Application.csproj`)          | Use cases                            | Command/Query handlers, validators, **all abstractions/interfaces** (`I...`), behaviors (decorators), DTO responses, domain-event handlers.                               |
| `Infrastructure` (`libs/api/infrastructure/Infrastructure.csproj`) | Technical implementations            | EF Core `DbContext`, entity configurations, migrations, auth/JWT, password hashing, time provider, domain-event dispatcher — implementations of Application abstractions. |
| `Api` (`apps/api/Api.csproj`)                                      | Presentation / composition root      | Minimal-API endpoints, middleware, exception handler, DI wiring, OpenAPI, request mapping. **Emits the OpenAPI document** consumed by `libs/api/api-types`.               |
| `Cli` (`apps/cli/Cli.csproj`)                                      | Basic automation                     | console application project, DI wiring, request mapping.                                                                                                                  |
| `Share.Cli` (`apps/share-cli/Share.Cli.csproj`)                    | Customer-facing CLI                  | console application project, DI wiring, command → use-case mapping. Sits on `libs/share-cli/*`, **not** `libs/api/*`.                                                           |

#### Where abstractions live (critical)

**All abstractions are defined in `Application`, implemented in `Infrastructure`.** This is the dependency-inversion seam.

- Interface lives in `Application/Abstractions/...` (e.g. `IApplicationDbContext`, `IUserContext`, `IStorageService`, `IApiKeyHasher`).
- Implementation lives in `Infrastructure/...` and is registered in `Infrastructure/DependencyInjection.cs`.
- Application code depends on the interface only. It never references a concrete Infrastructure type.

When you need a new external capability (email, blob storage, a third-party client): define the interface in `Application/Abstractions`, implement it in `Infrastructure`, register it in `Infrastructure/DependencyInjection.cs`.

#### Database knowledge stays in Infrastructure

- The Application layer talks to the database **only** through `IApplicationDbContext` (exposes `DbSet<>`s + `SaveChangesAsync`). It uses `Microsoft.EntityFrameworkCore` query extensions (e.g. `ToListAsync`, `SingleOrDefaultAsync`) but knows nothing about the provider.
- The concrete `ApplicationDbContext`, the chosen provider (PostgreSQL/Npgsql here), connection strings, naming conventions (snake_case), schemas, `IEntityTypeConfiguration<T>` mappings, and migrations all live in `Infrastructure/Database`.
- Entity-to-table mapping goes in `Infrastructure/<Feature>/<Entity>Configuration.cs`, not in attributes on the domain entity. The domain entity stays persistence-ignorant (plain properties).

### CQRS: Command / Query handler structure

There is **no MediatR**. Dispatch is a small set of generic interfaces in `Application/Abstractions/Messaging`:

```csharp
public interface ICommand;                         // void-returning command
public interface ICommand<TResponse>;              // command that returns a value
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{ Task<Result> Handle(TCommand command, CancellationToken ct); }

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{ Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct); }

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{ Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct); }
```

Conventions:

- **Commands mutate state; queries read state.** Queries never write. Commands return `Result` or `Result<T>`; queries return `Result<TResponse>`.
- **Every handler returns `Result` / `Result<T>`** — never throws for expected failures. Use domain `*Errors` for failure cases (see Error handling below).
- Handlers are `internal sealed`, use **primary constructors** for dependency injection, and take a `CancellationToken` that is threaded through every async call.
- Handlers depend on Application abstractions only (`IApplicationDbContext`, `IUserContext`, `IDateTimeProvider`, …).
- Queries project directly to response DTOs in the EF query (`.Select(x => new XResponse { ... })`) and typically use `AsNoTracking()` for reads.
- Endpoints invoke handlers **directly** by injecting `ICommandHandler<...>` / `IQueryHandler<...>` into the route delegate. There is no `ISender`/`IMediator` indirection.

Handlers are auto-registered by assembly scanning (Scrutor) in `Application/DependencyInjection.cs` — you do **not** register handlers manually.

### Vertical-slice feature organization

Group by feature, then by use case. Each use case is its own folder holding the command/query, its handler, and its validator:

```
libs/api/application/   (Application.csproj)
  <Feature>/                     e.g. Todos, Users
    <UseCase>/                   e.g. Create, Complete, GetById
      <UseCase>Command.cs        (or <UseCase>Query.cs)
      <UseCase>CommandHandler.cs
      <UseCase>CommandValidator.cs   (optional, only if validation is needed)
      <Response>.cs                  (query response DTO, when returning data)
```

The matching endpoint lives in `apps/api/Endpoints/<Feature>/<UseCase>.cs` (the Api host).

### Cross-cutting concerns are behaviors (decorators)

Validation and logging are **not** written inside handlers. They are decorators in `Application/Abstractions/Behaviors`, applied via Scrutor's `Decorate(...)` in `Application/DependencyInjection.cs`:

- `ValidationDecorator` — runs all `FluentValidation` `IValidator<TCommand>`s before the inner handler; on failure returns `Result.Failure(...)` with a `ValidationError` instead of calling the handler. Applied to command handlers.
- `LoggingDecorator` — logs start/finish of each command and query, pushing the `Error` onto the Serilog `LogContext` on failure. Applied to command and query handlers.

To add a new cross-cutting concern (e.g. transactions, caching, metrics): write a new decorator class implementing the same handler interface and register it with `services.Decorate(...)`. **Decorator order matters** — the registration order in `DependencyInjection.cs` defines the wrapping order. Do not put cross-cutting logic inside individual handlers.

Validation rules go in `<UseCase>CommandValidator : AbstractValidator<TCommand>` using FluentValidation. Validators are auto-discovered (`AddValidatorsFromAssembly`, including internal types).

### Result & error handling

- `SharedKernel.Result` / `Result<T>` model success/failure explicitly. Prefer returning failures over throwing.
- `SharedKernel.Error` has a `Code`, `Description`, and `ErrorType` (`Failure`, `Validation`, `Problem`, `NotFound`, `Conflict`).
- Define expected errors as **static factory methods on a per-entity `*Errors` class in `Domain`** (e.g. `TodoItemErrors.NotFound(id)`, `UserErrors.Unauthorized()`). Reuse these in handlers — don't construct ad-hoc `Error`s in handlers.
- Endpoints translate `Result` to HTTP via `result.Match(onSuccess, CustomResults.Problem)`; `CustomResults.Problem` maps `ErrorType` to the correct status code / ProblemDetails. Unexpected exceptions are handled by `GlobalExceptionHandler`.

### Domain events

- Entities derive from `SharedKernel.Entity` and raise events with `entity.Raise(new SomethingHappenedDomainEvent(...))`.
- Events are `sealed record`s implementing `IDomainEvent`, defined in `Domain/<Feature>`.
- `ApplicationDbContext.SaveChangesAsync` extracts raised events and dispatches them **after** the DB save (eventual consistency) via `IDomainEventsDispatcher`.
- Handlers implement `IDomainEventHandler<TEvent>` and live in `Application/<Feature>/...`; they are auto-registered by scanning.

### Api endpoints

- Endpoints implement `IEndpoint` (`void MapEndpoint(IEndpointRouteBuilder app)`) and are auto-discovered/registered (`AddEndpoints` + `MapEndpoints`).
- One endpoint per file under `Api/Endpoints/<Feature>/<UseCase>.cs`, `internal sealed`.
- The endpoint defines its own `Request` shape, maps it to the Application command/query, calls the injected handler, and returns `result.Match(...)`.
- Tag endpoints with `.WithTags(Tags.<Feature>)` and protect them with `.RequireAuthorization()` / `.HasPermission(...)`.
- Keep endpoints thin: mapping + handler invocation + result translation. No business logic.
- **Endpoint shapes are the public contract.** Anything you change here flows into the OpenAPI document and therefore into `libs/api/api-types` — regenerate types after changing a request/response shape (see [The API contract pipeline](#the-api-contract-pipeline)).

### .NET conventions & tooling

- **.NET 10**, C# with `ImplicitUsings` and `Nullable` enabled solution-wide.
- **Warnings are errors** (`TreatWarningsAsErrors`, `AnalysisMode=All`, SonarAnalyzer + EnforceCodeStyleInBuild). Code must build clean — fix analyzer/style warnings, don't suppress them casually.
- **Central Package Management**: all versions live in `Directory.Packages.props` (repo root). Add a `<PackageReference Include="..." />` (no version) in the csproj and a `<PackageVersion .../>` entry centrally. Shared MSBuild settings live in `Directory.Build.props`.
- Prefer **primary constructors**, `sealed` classes, file-scoped namespaces, target-typed `new`, and collection expressions (`[]`) — match the existing style.
- Use `internal` for handlers/configurations/endpoints; expose `public` only what other assemblies genuinely need (commands, queries, abstractions, DTOs). `InternalsVisibleTo` is used for test assemblies.

---

## Build, run, test

Prefer **Nx targets** — they wrap the underlying dotnet/vite/eslint commands and give caching + a uniform interface. Run from the repo root.

```bash
# Frontend + backend dev servers
nx serve api                    # run the API (dotnet, http profile, port 5080)
nx serve web                    # run the React app (Vite, port 3000)
nx ping share-cli               # smoke-test the customer-facing CLI

# Build / lint / test (any project)
nx build api                    # dotnet build the API (Release)
nx build web                    # vite production build
nx lint web                     # eslint
nx test web                     # vitest
nx run-many -t lint --all       # == npm run lint:all

# Regenerate API types after a contract change
npm run generate:api-types      # serve API + generate from live OpenAPI
npm run gen:types:local         # generate from apps/api/Api.json (build-time doc)
```

Direct .NET commands (run from repo root; the solution is `share.slnx`):

```bash
dotnet build                                   # build whole solution (warnings = errors)
dotnet test                                    # run all tests, including ArchitectureTests
dotnet run --project apps/api                  # run the Api host directly

dotnet ef migrations add <Name> \              # add a migration
  --project libs/api/infrastructure \
  --startup-project apps/api
```

Migrations are applied automatically in Development on startup (`app.ApplyMigrations()`).

---

## Releasing the Share CLI

`.github/workflows/release-share-cli.yml` builds and publishes `apps/share-cli`. Pushing a tag matching `sharecli-*` is the whole release process:

```bash
git tag sharecli-1.2.3 && git push origin sharecli-1.2.3   # or sharecli-v1.2.3-beta.1
```

The tag is the version: `sharecli-` and an optional `v` are stripped, the rest must be `MAJOR.MINOR.PATCH` with an optional `-suffix`, and it is passed to `dotnet publish` as `-p:Version=` (so `share --version` matches the tag). A suffix marks the GitHub release as a prerelease. Run the workflow from the Actions tab with a version input to rehearse: it builds and uploads the archives as workflow artifacts but publishes nothing.

- **Six targets, all cross-published from one Linux runner**: `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, `win-x64`, `win-arm64`. A self-contained non-AOT publish does not need a matching host, and a Unix host is what keeps the executable bit inside the archives — do not move the Unix targets onto a Windows runner.
- **Publish flags are release packaging concerns and live in the workflow, not the csproj**: `--self-contained` (no .NET runtime on the user's machine), `PublishSingleFile` + `EnableCompressionInSingleFile` (one ~35 MB executable), `InvariantGlobalization=true` (drops the libicu dependency — without it the binary dies with an ICU error on a minimal image), `DebugType=embedded` (stack traces keep line numbers, no separate `.pdb` to ship).
- **Not trimmed**, deliberately: the configuration binder and Serilog's settings provider resolve types by reflection, and trimming breaks them silently.
- A build step runs the `linux-x64` binary inside a stock `ubuntu:24.04` container and asserts it prints the tagged version. That is the self-containment claim, tested rather than assumed — keep it.
- **`share update` reads this workflow's output**: the `sharecli-` tag prefix, the `share-<version>-<rid>.tar.gz|zip` archive names and the collected `SHA256SUMS.txt` are what the CLI looks for. They are mirrored in `Share.Domain.Updates.ReleasePackaging` and `UpdateDefaults` — change the workflow and change those in the same commit, or every installed CLI stops being able to update itself.
- `appsettings.Development.json` is `CopyToPublishDirectory=Never`, so developer overrides never reach a user.
- Binaries are unsigned; the release notes tell macOS users about `xattr -d com.apple.quarantine` and Windows users about SmartScreen. Signing is the obvious next step if that becomes a problem.

---

## Adding a new feature — end-to-end checklist

Backend changes that affect the contract must flow all the way through to the web app.

1. **Domain** (`libs/api/domain`): add/extend the entity (derive from `Entity`), any value objects/enums, domain events, and a `*Errors` class.
2. **Application** (`libs/api/application/<Feature>/<UseCase>/`): create the command/query (`ICommand`/`IQuery`), its `internal sealed` handler returning `Result`, and a FluentValidation validator if needed. Add a response DTO for queries.
3. **Abstractions**: if you need a new external capability, define the interface in `libs/api/application/Abstractions`.
4. **Infrastructure** (`libs/api/infrastructure`): implement any new abstraction; add the entity to `ApplicationDbContext`, an `IEntityTypeConfiguration<T>`, and a migration. Register implementations in `Infrastructure/DependencyInjection.cs`.
5. **Api** (`apps/api/Endpoints/<Feature>/`): add the `IEndpoint`, map request → command/query, call the handler, return `result.Match(...)`.
6. **Verify the backend**: `dotnet build` (clean) and `dotnet test` (ArchitectureTests stay green — no outward dependencies introduced).
7. **Regenerate the contract** (`libs/api/api-types`): run `npm run generate:api-types` (or `gen:types:local`). Commit the regenerated `schema.ts` — never hand-edit it.
8. **Frontend** (`apps/web`): consume the new/changed types from `@share/api-types` and wire up the UI/data-fetching. Do not redeclare request/response shapes.
9. **Verify the frontend**: `nx lint web` and `nx test web`.
