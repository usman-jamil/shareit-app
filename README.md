# Share

Share makes it easy for developers to hand a folder to someone else. Run one command inside a
folder, get back a URL with a time-to-live; the recipient opens it in a browser and browses or
downloads the files. No zipping, no attachment limits, no "can you re-send that".

It is made of a **CLI** developers install on their machine and a **backend you self-host** — an
API, a web app for viewing shares, and a single Docker image that runs both.

Invite-only by design: API keys are issued by an admin and are revocable. Files live in a private
object-storage bucket and are reached through short-lived presigned URLs, so a share stops working
the moment it expires.

---

## Modules

The repository is an [Nx](https://nx.dev) monorepo. Architecture, conventions and layering rules
live in [CLAUDE.md](CLAUDE.md); each module's own README goes deeper where one exists.

| Module | What it is |
| ------ | ---------- |
| [`apps/api`](apps/api) | The backend. .NET 10 minimal APIs on a Clean Architecture stack (`libs/api/*`), PostgreSQL via EF Core, S3-compatible object storage. Owns the OpenAPI contract everything else is generated from. |
| [`apps/web`](apps/web) | The share-viewing page. React 19 + TypeScript, client-side rendered, built with Vite. Consumes generated API types; never talks to the backend's source. |
| [`apps/share-cli`](apps/share-cli) | The customer-facing CLI, published as `share`. Creates shares, manages its own configuration, and updates itself. Sits on its own Clean Architecture stack (`libs/share-cli/*`). |
| [`apps/cli`](apps/cli) | The internal admin CLI. Migrations, users, API keys. Not shipped to customers. |
| [`apps/app-image`](apps/app-image) | The deployment unit. An ASP.NET Core host that reverse-proxies `/api` to the API and serves the built web app, packaged with both into one Docker image. See its [README](apps/app-image/README.md). |
| [`libs/api/*`](libs/api) | `domain`, `application`, `infrastructure` for the API, plus `api-types` — TypeScript types generated from the OpenAPI document. |
| [`libs/share-cli/*`](libs/share-cli) | The same layering for the customer CLI, plus a generated Refit client. |
| [`libs/shared/shared-kernal`](libs/shared/shared-kernal) | `Result`, `Error`, `Entity` and other primitives. The only code both stacks share. |
| [`tests/`](tests) | Unit and integration tests, mirroring the `libs/` layout. |

---

## First-time setup

```bash
npm ci                                   # workspace dependencies
dotnet restore share.slnx                # required once; the Nx .NET build targets use --no-restore
docker compose up -d postgres            # or: nx run infrastructure:compose-up
```

Then configure secrets for the two .NET apps that talk to the database and object storage. Both
**must use the same pepper** — it is part of every stored API key hash, so changing it invalidates
every key that already exists.

```bash
PEPPER="$(openssl rand -base64 32)"
CONN="Host=localhost;Port=5432;Database=share;Username=postgres;Password=postgres"

for project in apps/api apps/cli; do
  (
    cd "$project"
    dotnet user-secrets init
    dotnet user-secrets set "ApiKey:Pepper" "$PEPPER"
    dotnet user-secrets set "ConnectionStrings:Database" "$CONN"
    dotnet user-secrets set "Storage:AccessKeyId" "<r2-access-key-id>"
    dotnet user-secrets set "Storage:SecretAccessKey" "<r2-secret-access-key>"
    dotnet user-secrets set "Storage:ServiceUrl" "https://<account>.r2.cloudflarestorage.com"
    dotnet user-secrets set "Storage:BucketName" "shareit-blobs"
  )
done
```

Create the schema and an API key to use:

```bash
nx run cli:migrate                                      # apply migrations
dotnet run --project apps/cli -- create-user --name "Ada" --email ada@example.com
dotnet run --project apps/cli -- create-api-key --user-id <user-id>    # prints the key once
```

---

## Everyday commands

### Run things

| Command | What it does |
| ------- | ------------ |
| `nx serve api` | Run the API on <http://localhost:5080> (starts PostgreSQL first) |
| `nx serve web` | Run the web app on <http://localhost:3000> |
| `nx run infrastructure:compose-up` | Start PostgreSQL and Seq |
| `nx run infrastructure:compose-down` | Stop them |
| `nx run app-image:docker-run` | Build and run the production image on <http://localhost:8080> |

Locally the web app calls the API with relative URLs (`fetch("/api/…")`), so put an NGINX in front
of the two dev servers to get production's routing. A working configuration is in the
[app-image README](apps/app-image/README.md#local-development).

### Build

| Command | What it does |
| ------- | ------------ |
| `dotnet build share.slnx` | Build every .NET project (warnings are errors) |
| `nx build api` | Build the API |
| `nx build web` | Production build of the web app to `dist/apps/web` |
| `nx build share-cli` | Build the customer CLI |
| `nx run app-image:docker-build` | Build the production Docker image (`app-image:local`) |
| `nx run-many -t build --all` | Build everything |

Override the image name and tag with environment variables:

```bash
APP_IMAGE_NAME=ghcr.io/acme/share APP_IMAGE_TAG=1.4.0 nx run app-image:docker-build
```

### Test and lint

| Command | What it does |
| ------- | ------------ |
| `dotnet test --solution share.slnx` | Every .NET test |
| `dotnet test --project tests/api/application-unit-tests/Application.UnitTests.csproj` | API unit tests |
| `dotnet test --project tests/api/application-integration-tests/Application.IntegrationTests.csproj` | API integration tests (Testcontainers) |
| `dotnet test --project tests/share-cli/application-unit-tests/Share.Application.UnitTests.csproj` | CLI use-case tests |
| `dotnet test --project tests/share-cli/infrastructure-unit-tests/Share.Infrastructure.UnitTests.csproj` | CLI adapter and update tests |
| `nx typecheck web` | Type-check the web app (`tsc -b` — Vite does not type-check) |
| `nx run-many -t lint --all` | Lint everything |

### Database

```bash
nx run cli:migrate                          # apply migrations

dotnet ef migrations add <Name> \
  --project libs/api/infrastructure --startup-project apps/api

dotnet ef database update \
  --project libs/api/infrastructure --startup-project apps/api

dotnet run --project apps/cli -- drop-database
```

### Regenerate the API contract

The API owns the contract; the TypeScript types and the CLI's HTTP client are **generated** and must
never be hand-edited. Run these after changing any endpoint's request or response shape, and commit
the result.

```bash
npm run generate:api-types        # serve the API, regenerate libs/api/api-types
npm run gen:types:local           # same, from the build-time apps/api/Api.json
npm run generate:refitter-types   # serve the API, regenerate the CLI's Refit client
```

### Admin CLI (`apps/cli`)

```bash
dotnet run --project apps/cli -- apply-migration
dotnet run --project apps/cli -- drop-database
dotnet run --project apps/cli -- create-user --name "Ada" --email ada@example.com
dotnet run --project apps/cli -- create-api-key --user-id <user-id>
dotnet run --project apps/cli -- validate-api-key --api-key <key>
```

### Customer CLI (`share`)

```bash
share config set --base-url https://share.example.com   # or -u
share config set --api-key <key>                        # or -k, write-only
share config set --user-id <user-id>                    # or -i, the owner of new shares
share config set --timeout-seconds 45                   # or -t
share config show                                       # effective settings
share config path                                       # where the file lives

share create                                            # share the current folder
share create --path ./docs --ttl-minutes 240

share update --check                                    # is a newer release available?
share update                                            # replace the running binary
share update --version 1.3.2 --yes                      # move to a specific release
```

Configuration lives in `~/.share/config.yaml`, which wins over `appsettings.json`, user secrets and
environment variables. `SHARE_CLI_CONFIG` overrides the path.

---

## Releasing

Both artifacts release by pushing a tag. The tag **is** the version: the prefix and an optional `v`
are stripped, and what remains must be `MAJOR.MINOR.PATCH` with an optional `-suffix`. A suffix
marks the release as a prerelease, which is published but does not become the default.

### The API and web app — one Docker image

```bash
git tag shareapi-1.2.3 && git push origin shareapi-1.2.3      # or shareapi-v1.2.3-beta.1
```

[`.github/workflows/release-share-api.yml`](.github/workflows/release-share-api.yml) runs the tests,
builds the web app and both .NET applications, packages them into one image, starts it and probes
it, then pushes `musmanbhatti/shareit-app:1.2.3`. A stable version also moves `:latest`.

Needs the `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` repository secrets.

### The customer CLI — six platform binaries

```bash
git tag sharecli-1.2.3 && git push origin sharecli-1.2.3      # or sharecli-v1.2.3-beta.1
```

[`.github/workflows/release-share-cli.yml`](.github/workflows/release-share-cli.yml) publishes
self-contained single-file binaries for `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`,
`win-x64` and `win-arm64`, with a `SHA256SUMS.txt`, and attaches them to a GitHub release. Installed
CLIs find their own updates through this release, so the archive naming is a contract — see the
release notes in [CLAUDE.md](CLAUDE.md#releasing-the-share-cli).

### Rehearsing a release

Both workflows accept a version input from the Actions tab. That runs the whole build and every
check but publishes nothing, so a release can be tried before it is tagged.

---

## Running the backend

One container serves the whole product on one port: the web app at `/`, the API at `/api`.

```bash
docker run --rm -p 8080:8080 \
  -e "ConnectionStrings__Database=Host=host.docker.internal;Port=5432;Database=share;Username=postgres;Password=postgres" \
  -e "ApiKey__Pepper=<the same pepper the API keys were minted with>" \
  -e "Outbox__IntervalInSeconds=60" \
  -e "Outbox__BatchSize=10" \
  -e "Share__ConfiguredTtlMinutes=60" \
  -e "Storage__AccessKeyId=..." \
  -e "Storage__SecretAccessKey=..." \
  -e "Storage__ServiceUrl=https://<account>.r2.cloudflarestorage.com" \
  -e "Storage__BucketName=shareit-blobs" \
  musmanbhatti/shareit-app:latest
```

Everything the setup step above puts in user secrets has to be passed here instead — user secrets do
not exist inside a container. `localhost` in the connection string means the container, not the host
machine. `ApiKey__Pepper` must match the pepper the keys were created with, or every authenticated
request fails.

Health endpoints: `/health` (the container is up) and `/health/ready` (it can serve). The full list
of settings, what breaks without each, and the routing rules are in the
[app-image README](apps/app-image/README.md).

---

## API surface

| Method | Path | Auth | Purpose |
| ------ | ---- | ---- | ------- |
| `POST` | `/api/shares` | API key | Create a share. Returns the share id and a presigned upload URL per file. |
| `PUT` | `/api/shares/{id}/finalize` | API key | Commit a share once its files are uploaded. |
| `GET` | `/api/shares/{id}` | API key | Share metadata and file list. |
| `GET` | `/api/users/{id}` | API key | Look up a user. |
| `GET` | `/api/health` | — | API health, including the database. |

Authenticate with an `X-Api-Key` header. The app host strips the `/api` prefix before forwarding, so
the API itself serves these at `/shares`, `/users/{id}` and `/health` — which is what you hit when
running `nx serve api` directly on port 5080.

Uploading is a three-step conversation: create the share, `PUT` each file to the presigned URL it
came back with, then finalize. Files go straight to object storage and never pass through the API.

---

## Limits

What the API validates today:

| | |
| --- | --- |
| Share TTL | Must be greater than zero. There is no built-in default — set `Share__ConfiguredTtlMinutes`, or pass `--ttl-minutes` per share. |
| File path | 1024 characters, no `..` segments |
| Content type | 255 characters |
| File size | Under 2 GiB per file; the CLI rejects anything larger before uploading |

Caps on files per share, bytes per share and active shares per user are not enforced yet.

---

## Contributing

Read [CLAUDE.md](CLAUDE.md) before making a change — it says which project owns what, and the
layering rules that the architecture tests enforce. In short:

- Dependencies point inward: `Api → Infrastructure → Application → Domain → SharedKernel`.
- `libs/api/*` and `libs/share-cli/*` never reference each other.
- Generated files (`libs/api/api-types/src/lib/schema.ts`, `libs/share-cli/api-types/Generated.cs`)
  are regenerated, never edited.
- Warnings are errors. `dotnet build` and `dotnet test` must be clean before you push.
