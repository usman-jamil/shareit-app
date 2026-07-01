# CLAUDE.md

Guidance for working in this repository. This is an **Nx monorepo** (`@share/source`) that houses three cooperating projects:

- **`apps/api`** — the backend: a **.NET 10** **Clean Architecture** solution (minimal APIs, EF Core, hand-rolled CQRS).
- **`apps/cli`** — the goto CLI for autoamtion: a **.NET 10** solution (console app).
- **`apps/web`** — the frontend: a **React 19 + TypeScript** client-side-rendered (CSR) app built with Vite.
- **`libs/api-types`** — the seam between them: **auto-generated** TypeScript types derived from the API's OpenAPI contract.

The single most important thing this file does is tell you **where a given change belongs**. Decide which project owns the change _first_, then follow that project's local rules. Cross-cutting changes flow in one direction: **API → OpenAPI contract → `libs/api-types` → `apps/web`.**

---

## Monorepo layout — where things go

```
/ (repo root)
├── nx.json                     # Nx workspace config (plugins, target defaults, generators)
├── package.json                # JS toolchain + workspace scripts (serve/gen:types/lint)
├── tsconfig.base.json          # Base TS config + path aliases (e.g. @share/api-types)
├── share.slnx                  # .NET solution file (references apps/api + the libs/* class libraries)
├── Directory.Packages.props    # Central NuGet version management for the .NET solution
├── Directory.Build.props       # Shared MSBuild settings (lands here as the API grows)
├── eslint.config.mjs           # Root ESLint flat config
├── .prettierrc / .editorconfig # Formatting
│
├── apps/
│   ├── api/                    # .NET 10 Api host (apps/api/Api.csproj) — composition root  (Nx project: "api")
│   └── web/                    # React/TS CSR frontend                (Nx project: "web")
│   └── cli/                    # .Net 10 Cli                          (Nx project: "cli")
│
└── libs/
    ├── api-types/              # Generated OpenAPI → TS types         (Nx project: "api-types")
    ├── shared-kernal/          # SharedKernel.csproj — shared building blocks (no dependencies)
    ├── domain/                 # Domain.csproj — enterprise rules
    ├── application/            # Application.csproj — use cases + abstractions
    └── infrastructure/         # Infrastructure.csproj — technical implementations
```

> **Convention:** every .NET **class library** lives under `libs/` (one folder per project); only the **Api host** (`apps/api/Api.csproj`) lives under `apps/api`. `share.slnx` references all of them.

Each project is an **Nx project** defined by its `project.json`. Nx targets (`serve`, `build`, `lint`, `test`, `generate-types`, …) are the canonical way to run work; see [Build, run, test](#build-run-test).

### Decision table — which project owns a change

| You are changing…                                     | It belongs in…                                         | Notes                                                                         |
| ----------------------------------------------------- | ------------------------------------------------------ | ----------------------------------------------------------------------------- |
| API behavior, endpoints, domain logic, persistence    | `apps/api`                                             | Follow the Clean Architecture rules below.                                    |
| The shape of a request/response the frontend consumes | `apps/api` **first**, then regenerate `libs/api-types` | The contract is owned by the backend; types are generated, never hand-edited. |
| UI, pages, components, client-side state, fetching    | `apps/web`                                             | Self-contained; consumes `@share/api-types`.                                  |
| The generated API type definitions                    | **Do not edit by hand** — regenerate from the API      | See [The API contract pipeline](#the-api-contract-pipeline).                  |
| Shared frontend/TS code reused across web apps        | a **new `libs/*`** project                             | Create with Nx generators; never reach into another app's `src`.              |
| NuGet versions                                        | `Directory.Packages.props`                             | Central Package Management — see .NET conventions.                            |
| npm dependency versions                               | root `package.json`                                    | Single lockfile; the workspace is one npm install.                            |

**Golden rule of the monorepo:** never create a runtime dependency from `apps/web` into `apps/api`'s source, or vice-versa. The _only_ thing they share is the generated contract in `libs/api-types`. If you find yourself wanting to import across that boundary directly, the answer is almost always "extend the OpenAPI contract and regenerate types."

---

## The API contract pipeline

`libs/api-types` is **generated output**, not source you edit. The flow is:

```
apps/api (Api host)
   │  emits OpenAPI document on build (OpenApiGenerateDocumentsOnBuild)
   ▼
apps/api/Api.json   ──or──   http://localhost:5080/openapi/v1.json   (running server)
   │  openapi-typescript
   ▼
libs/api-types/src/lib/schema.ts        # generated types — DO NOT hand-edit
   │  re-exported via
   ▼
libs/api-types/src/index.ts  ──►  imported in apps/web as  @share/api-types
```

Regenerate types whenever the API contract changes:

```bash
# Against a running API (serves the API, waits for OpenAPI, then generates):
npm run generate:api-types          # == nx run api-types:generate-types

# Against the build-time OpenAPI doc already on disk (apps/api/Api.json):
npm run gen:types:local
```

- `schema.ts` is **machine-generated** — if it's wrong, fix the API and regenerate, don't patch the file.
- Anything hand-written that _augments_ the generated types (helpers, narrowed aliases) goes in `libs/api-types/src/lib/api-types.ts` and is re-exported from `index.ts`.
- The path alias `@share/api-types` is defined in `tsconfig.base.json`. Import from the alias, never via a relative `../../libs/...` path.
- **If the Api host's location or port changes, update the generation commands** (`gen:types`, `gen:types:local`, `serve:api` in `package.json`, the `serve` target in `apps/api/project.json`, and the OpenAPI output path) so the pipeline keeps working.

---

## `apps/web` — React/TS frontend

A self-contained CSR React 19 app built with Vite.

- Source lives under `apps/web/src` (`main.tsx` bootstraps; feature code under `src/app/...`).
- **No project references** other than `@share/api-types`. It does not import from `apps/api` or other apps.
- Consume API types from `@share/api-types`; do not redeclare request/response shapes locally.
- Dev server runs on **port 3000**; the API runs on **port 5080**.
- Lint/test/build are driven through Nx (`nx <target> web`). Vite config is in `apps/web/vite.config.mts`; TS config extends `tsconfig.base.json`.
- Add shared, reusable UI/logic as a **new `libs/*`** project (via `nx g @nx/react:lib`) rather than growing cross-app imports.

---

## `apps/api` — .NET 10 Clean Architecture backend

> The API currently starts as a single minimal-API project (`apps/api/Api.csproj`, the **Api** host). As it grows it expands into the full Clean Architecture layout below: the **Api host stays at `apps/api/Api.csproj`**, and the inner layers become separate **class-library projects under `libs/`** — `libs/shared-kernal/SharedKernel.csproj`, `libs/domain/Domain.csproj`, `libs/application/Application.csproj`, `libs/infrastructure/Infrastructure.csproj`. Test projects live under `apps/api/tests/`. Several assets referenced here may not exist yet — place them as described when you add them. The `share.slnx` solution at the repo root references all of these projects.

This is a **Clean Architecture** solution built on **.NET 10**, minimal APIs, EF Core, and a hand-rolled CQRS dispatcher with cross-cutting behaviors implemented as decorators.

## `apps/cli` — .NET 10 Clean Architecture cli

> The CLI currently starts as a console application project (`apps/cli/Cli.csproj`, the **Api** host). As it grows it expands into the full Clean Architecture layout just like the `apps/api/Api.csproj` project.

This is a **Clean Architecture** solution built on **.NET 10**, minimal APIs, EF Core, and a hand-rolled CQRS dispatcher with cross-cutting behaviors implemented as decorators.

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

These rules are enforced by `apps/api/tests/ArchitectureTests`. **If you add a project reference that points outward, those tests fail by design — fix the design, not the test.**

### Project layout & what belongs where

The class-library layers live under `libs/` (one folder per project); the Api host is `apps/api/Api.csproj`; tests live under `apps/api/tests/`.

| Project (path)                                                 | Responsibility                       | Allowed to contain                                                                                                                                                        |
| -------------------------------------------------------------- | ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SharedKernel` (`libs/shared-kernal/SharedKernel.csproj`)      | Building blocks shared by all layers | `Result`/`Error`, `Entity` base, `IDomainEvent`, `IDateTimeProvider`, primitives. No dependencies.                                                                        |
| `Domain` (`libs/domain/Domain.csproj`)                         | Enterprise rules                     | Entities, value objects, enums, domain events, **static `*Errors` classes**. No EF, no I/O, no framework references.                                                      |
| `Application` (`libs/application/Application.csproj`)          | Use cases                            | Command/Query handlers, validators, **all abstractions/interfaces** (`I...`), behaviors (decorators), DTO responses, domain-event handlers.                               |
| `Infrastructure` (`libs/infrastructure/Infrastructure.csproj`) | Technical implementations            | EF Core `DbContext`, entity configurations, migrations, auth/JWT, password hashing, time provider, domain-event dispatcher — implementations of Application abstractions. |
| `Api` (`apps/api/Api.csproj`)                                  | Presentation / composition root      | Minimal-API endpoints, middleware, exception handler, DI wiring, OpenAPI, request mapping. **Emits the OpenAPI document** consumed by `libs/api-types`.                   |
| `Cli` (`apps/cli/Cli.csproj`)                                  | Basic automation                     | console application project, DI wiring, request mapping.                                                                                                                  |

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
libs/application/   (Application.csproj)
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
- **Endpoint shapes are the public contract.** Anything you change here flows into the OpenAPI document and therefore into `libs/api-types` — regenerate types after changing a request/response shape (see [The API contract pipeline](#the-api-contract-pipeline)).

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
  --project libs/infrastructure \
  --startup-project apps/api
```

Migrations are applied automatically in Development on startup (`app.ApplyMigrations()`).

---

## Adding a new feature — end-to-end checklist

Backend changes that affect the contract must flow all the way through to the web app.

1. **Domain** (`libs/domain`): add/extend the entity (derive from `Entity`), any value objects/enums, domain events, and a `*Errors` class.
2. **Application** (`libs/application/<Feature>/<UseCase>/`): create the command/query (`ICommand`/`IQuery`), its `internal sealed` handler returning `Result`, and a FluentValidation validator if needed. Add a response DTO for queries.
3. **Abstractions**: if you need a new external capability, define the interface in `libs/application/Abstractions`.
4. **Infrastructure** (`libs/infrastructure`): implement any new abstraction; add the entity to `ApplicationDbContext`, an `IEntityTypeConfiguration<T>`, and a migration. Register implementations in `Infrastructure/DependencyInjection.cs`.
5. **Api** (`apps/api/Endpoints/<Feature>/`): add the `IEndpoint`, map request → command/query, call the handler, return `result.Match(...)`.
6. **Verify the backend**: `dotnet build` (clean) and `dotnet test` (ArchitectureTests stay green — no outward dependencies introduced).
7. **Regenerate the contract** (`libs/api-types`): run `npm run generate:api-types` (or `gen:types:local`). Commit the regenerated `schema.ts` — never hand-edit it.
8. **Frontend** (`apps/web`): consume the new/changed types from `@share/api-types` and wire up the UI/data-fetching. Do not redeclare request/response shapes.
9. **Verify the frontend**: `nx lint web` and `nx test web`.
