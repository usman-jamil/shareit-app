# `apps/app-image` — production Docker image

Packages the API and the React web app into a single production container behind one public port.

This project exists **only** to produce that image. It is not part of the local development loop:
`nx serve api` and `nx serve web` are unchanged, and nothing here needs to be built or running for
them to work. See [Local development](#local-development) below.

---

## Architecture

```
Browser
  │
  ▼
App Image Host :8080                (apps/app-image — ASP.NET Core + YARP, the only public port)
  ├── /api          ──► YARP ──►    Api :5000 on 127.0.0.1  (loopback only, never published)
  ├── /api/{**rest} ──► YARP ──►    Api :5000 on 127.0.0.1
  ├── /health       ──►             app host liveness
  ├── /health/ready ──►             app host readiness
  ├── /assets/*     ──►             React build output, immutable caching
  └── /*            ──►             React index.html (client-side routes only)
```

Both .NET processes run inside one container, supervised by `docker-entrypoint.sh` as PID 1. The API
binds loopback and is unreachable from outside the container; only the host is published.

The web app keeps calling the API with relative URLs — `fetch("/api/example")` — in development and
in production alike. Nothing in `apps/web` changes.

### The `/api` prefix is stripped before forwarding

This repository's API maps its endpoints at the root of its own address space. `apps/api/Program.cs`
calls `MapEndpoints()` with no route group, so `apps/api/Endpoints/Shares/Create.cs` registers
`shares`, not `api/shares`. The routes the API actually serves are `/shares`, `/users/{id}` and
`/health`.

The `/api` prefix therefore belongs to the public URL space this host owns, and YARP removes it
before forwarding (`PathRemovePrefix`). `/api/shares` arrives at the API as `/shares`.

If the API ever moves its endpoints under `/api`, set `APP_IMAGE_API_STRIP_PATH_PREFIX=false` — no
code change needed.

---

## Building the image

```bash
nx run app-image:docker-build
```

That is the whole command. It drives this task graph:

```
web:build            ──┐
api:publish          ──┼──► app-image:build ──► app-image:docker-build
app-image:build-host ──┘     (stage artifacts)     (docker build)
```

| Target       | What it does                                                                                             | Cached |
| ------------ | -------------------------------------------------------------------------------------------------------- | ------ |
| `build-host` | `dotnet publish` this host to `dist/apps/app-image/host`                                                  | yes    |
| `build`      | Collects all three outputs into the Docker build context at `dist/apps/app-image/context`                 | yes    |
| `docker-build` | `docker build` from that context                                                                        | no     |
| `docker-run` | Runs the built image with only 8080 published                                                            | no     |

`docker-build` and `docker-run` are not cached: they have side effects outside the workspace (the
local image store), and this repository has no convention for caching those.

Nx builds everything **outside** Docker and the Dockerfile only packages the results. There is no SDK
stage, so the final image contains no Node.js, no npm, no Nx and no .NET SDK — just the ASP.NET Core
runtime, the published assemblies and the static files.

### Image name and tag

Both are environment variables, defaulting to `app-image:local`:

```bash
APP_IMAGE_NAME=ghcr.io/acme/share APP_IMAGE_TAG=1.4.0 nx run app-image:docker-build
```

Extra arguments go straight to `docker build`:

```bash
nx run app-image:docker-build --args="--no-cache --platform=linux/amd64"
```

### Required build outputs

The staging step consumes exactly three directories and fails with a clear message if any is absent:

| Produced by             | Location                            |
| ----------------------- | ----------------------------------- |
| `nx build web`          | `dist/apps/web`                     |
| `nx run api:publish`    | `apps/api/bin/Release/<tfm>/publish` |
| `nx run app-image:build-host` | `dist/apps/app-image/host`     |

The API's target framework and both assembly names are **discovered** from the published output
(`*.runtimeconfig.json`), not hardcoded. The result is written to `app.env` in the image, which the
entrypoint sources — so a rename or a TFM bump needs no change here.

Nothing under `dist/` is committed; it is already in `.gitignore`.

> The Docker build context is the staged directory, not the repository root, so the root
> `.dockerignore` does not apply to this build and needed no changes.

---

## Running the image

```bash
docker run --rm -p 8080:8080 \
  -e "ConnectionStrings__Database=Host=host.docker.internal;Port=5432;Database=share;Username=postgres;Password=postgres" \
  -e "ApiKey__Pepper=<the same pepper the API keys were minted with>" \
  -e "Outbox__IntervalInSeconds=60" \
  -e "Outbox__BatchSize=10" \
  -e "Share__ConfiguredTtlMinutes=60" \
  -e "Storage__AccessKeyId=..." \
  -e "Storage__SecretAccessKey=..." \
  -e "Storage__ServiceUrl=..." \
  -e "Storage__BucketName=..." \
  app-image:local
```

Or through Nx:

```bash
nx run app-image:docker-run
nx run app-image:docker-run --args="-e ConnectionStrings__Database=..."
```

### The API's configuration does not travel into the image

This is the one thing most likely to bite. The API's own settings are supplied the way ASP.NET Core
always supplies them, with `Section__Key` environment variables — but locally they come from **user
secrets**, and user secrets do not exist in a container. `apps/api/Program.cs` only calls
`AddUserSecrets` when the environment is `Development`, and even then it reads
`~/.microsoft/usersecrets/...` on the developer's machine.

So everything the repository README tells you to put in user secrets has to be passed to
`docker run` instead:

| Setting                     | Consequence if missing                                              |
| --------------------------- | ------------------------------------------------------------------- |
| `ConnectionStrings__Database` | The API aborts at startup, and the container exits non-zero.       |
| `Outbox__IntervalInSeconds`, `Outbox__BatchSize` | Quartz throws `Repeat Interval cannot be zero` at startup. |
| `ApiKey__Pepper`            | **Every authenticated request returns `500`** — see below.           |
| `Storage__*`                | Share/file endpoints fail when they reach object storage.            |
| `Share__ConfiguredTtlMinutes` | Shares are created with a zero TTL.                                |

**`localhost` in the connection string means the container, not your machine.** A database running
on the Docker host is `host.docker.internal` from inside the container (add
`--add-host host.docker.internal:host-gateway` on Linux; Docker Desktop resolves it already), or the
service name if you put the container on the same Docker network. The symptom is
`/health/ready` logging `The internal API answered 503 from '/health'` and `/api/health` reporting
`Failed to connect to 127.0.0.1:5432` — that endpoint needs no API key, so it is the quickest way to
tell a database problem from an auth one.

**`ApiKey__Pepper` deserves its own note.** API keys are stored as
`HMACSHA256(pepper).ComputeHash(secret)` ([ApiKeyHasher.cs](../../libs/api/infrastructure/Authentication/ApiKeyHasher.cs)),
so the pepper is part of the stored credential, not just a startup setting. If the container's
pepper differs from the one the key was minted with — including the default empty string when the
variable is unset — the row is found by `key_id` but `Verify` fails, and a perfectly valid key is
rejected.

That rejection surfaces as `500`, not `401`, because `apps/api/Program.cs` calls
`AddAuthentication()` without registering a scheme: authorization fails, ASP.NET Core tries to
challenge the caller, finds no `DefaultChallengeScheme`, and throws
`InvalidOperationException`, which `GlobalExceptionHandler` turns into

```json
{ "title": "Server failure", "status": 500 }
```

A `500` from any `.RequireApiKey()` endpoint is therefore almost always a rejected credential, not a
server fault. The API log shows the real story: `Completed query GetApiKeyQuery with error`,
immediately followed by the `No authenticationScheme was specified` exception. Fixing that to return
`401` is a change to `apps/api`/`libs/api`, outside this project.

### API logging

The API logs through Serilog, and its Serilog configuration exists only in
`appsettings.Development.json`. In `Production`, `ReadFrom.Configuration` finds no `Serilog` section
and builds a logger with **no sinks** — and `AddSerilog` has already replaced the default providers,
so the API writes nothing at all, not even unhandled exceptions. Only the app host's log lines reach
`docker logs`.

A container whose main process is silent cannot be operated, so `docker-entrypoint.sh` gives the API
a console sink by default:

```
Serilog__Using__0=Serilog.Sinks.Console
Serilog__MinimumLevel__Default=Information
Serilog__MinimumLevel__Override__Microsoft.AspNetCore=Warning
Serilog__WriteTo__0__Name=Console
```

This is configuration passed to the API, not a change to it. **Setting any `Serilog__*` variable on
the container disables the defaults entirely**, so a real logging configuration — Seq, a file, a
different level — is used as given rather than merged into. The entrypoint logs
`no Serilog__* configuration supplied; defaulting the api to a console sink` when the defaults apply,
so which of the two is in force is visible in `docker logs`.

The fix belonging to `apps/api` would be a `Serilog` section in `appsettings.json`; until that
exists, this keeps the image usable.

### Ports

| Port   | Scope                       |
| ------ | --------------------------- |
| `8080` | Published. The app host.    |
| `5000` | **Internal only.** The API, bound to `127.0.0.1` inside the container. Not `EXPOSE`d and not publishable. |

---

## Releasing

`.github/workflows/release-share-api.yml` builds and pushes this image. Pushing a tag matching
`shareapi-*` is the whole release process:

```bash
git tag shareapi-1.2.3 && git push origin shareapi-1.2.3   # or shareapi-v1.2.3-beta.1
```

The tag is the version: `shareapi-` and an optional `v` are stripped, and the rest must be
`MAJOR.MINOR.PATCH` with an optional `-suffix`. It becomes `musmanbhatti/shareit-app:1.2.3`; a
stable version also moves `:latest`, so a prerelease can be published without becoming the default
pull. Running the workflow from the Actions tab with a version input rehearses the whole thing —
build and smoke test — and pushes nothing.

Needs two repository secrets: `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` (a Read & Write access
token, not the account password).

- **The runner needs Node as well as .NET**, because the image is artifact packaged: the workflow
  runs `npm ci` and then `nx run app-image:build --skip-nx-cache`, which drives the whole task graph
  and stages the context. `--skip-nx-cache` is deliberate — a release should never reuse a cached
  artifact. The Docker build context is `dist/apps/app-image/context`, not the repository root.
- **The smoke test starts the image and probes it** rather than only building it: `/health` for the
  host, `/` for the React build, `/api/health` for the proxy hop to the API, a client route and a
  missing asset for the fallback rules, then a process count and a clean `docker stop`. Every one of
  those fails for a different reason, so a broken artifact is caught by name.
  `/api/health` is asserted to be **503**: the database is absent, so a body from the API proves the
  request crossed the proxy, where a 502 or a timeout would mean the API never started.
- **`APP_IMAGE_VERSION` stamps the release version into the app host assembly.** The API assembly
  keeps the version pinned in `apps/api/Api.csproj`, because an explicit `<Version>` in a project
  file beats an MSBuild environment variable and the inferred `api:publish` target takes no extra
  arguments. Changing that line to
  `<Version Condition="'$(Version)' == ''">0.1.0</Version>` would let the tag drive it too; until
  then the image tag and the OCI labels are what carry the version.

---

## Routing

| Request                        | Handled by             | Result                                    |
| ------------------------------ | ---------------------- | ----------------------------------------- |
| `/health`                      | app host               | `200 Healthy` — the process is up         |
| `/health/ready`                | app host               | `200` ready, `503` not ready              |
| `/api`, `/api/{**catch-all}`   | YARP → API             | Whatever the API returns, including its 404s |
| `/api/unknown`                 | YARP → API             | The API's `404` — never the SPA document  |
| `/assets/*`                    | static files           | The file, or `404` if it is missing       |
| `/favicon.ico`                 | static files           | The file, or `404` if it is missing       |
| `/`, `/customers/123`          | SPA fallback           | `index.html`                              |
| `/assets/missing.js`           | SPA fallback           | `404` — a request for a file that is not there |

Middleware order in `Program.cs`, which is the order that makes the table above true:

1. `UseForwardedHeaders`
2. `UseRouting`
3. Host health endpoints
4. `MapReverseProxy` — **before** the static files and the fallback
5. `UseStaticFiles`
6. `MapFallback` — the SPA document, plus an explicit guard that answers any `/api` request that
   somehow reaches it with a ProblemDetails `404` rather than HTML

### Why a missing asset is a 404, not `index.html`

The fallback returns the SPA document only when the request looks like a client-side route. If the
last path segment contains a dot it is treated as a request for a file, and a missing file gets a
404. Answering it with `index.html` would hand the browser `text/html` where it expected JavaScript,
turning a broken deploy into a silent one that fails much further from its cause.

The trade-off: a client-side route whose last segment contains a dot (`/share/v1.2`) would 404 on a
full page load. No route in `apps/web` looks like that today.

### Caching

| Response                            | `Cache-Control`                        |
| ----------------------------------- | -------------------------------------- |
| `/assets/*` (Vite fingerprints these) | `public,max-age=31536000,immutable`   |
| `index.html` and everything else    | `no-cache,no-store,must-revalidate`    |

---

## Health endpoints

| Endpoint        | Checks                                                                 |
| --------------- | ---------------------------------------------------------------------- |
| `/health`       | Nothing. Answering it proves the host process is up and serving.       |
| `/health/ready` | `index.html` is present in the web root, and the internal API answers HTTP. |

Readiness uses the API's own `/health` endpoint (which already exists in `apps/api/Program.cs`, so
nothing was added to the API). The two failure modes are treated differently on purpose:

- **The API did not answer at all** — every `/api` request will fail. `Unhealthy` → `503`.
- **The API answered but reported itself unwell** (its database is down, say) — routing works and
  this container can still serve the SPA. `Degraded` → `200`. The API's own dependency health is the
  API's to report, and it is readable at `/api/health`.

Responses are the status word only (`Healthy` / `Unhealthy`); failure detail goes to the container
logs, never to the caller.

The app host's `/health` is never proxied to the API.

### Container health check

The ASP.NET Core runtime image ships neither `curl` nor `wget`, and adding one means an `apt-get`
layer plus its ongoing CVE surface for the sake of a single HTTP GET. Instead the host implements the
probe itself:

```
HEALTHCHECK CMD ["dotnet", "/app/host/AppImage.Host.dll", "--healthcheck"]
```

It runs before the web host is built, so it never binds a port. It GETs
`http://127.0.0.1:8080/health/ready` (override with `APP_IMAGE_HEALTHCHECK_URL`) and exits `0` or
`1`. The cost is one short-lived .NET process per interval.

---

## Process supervision

`docker-entrypoint.sh` is PID 1. It starts both processes, then:

- forwards `SIGTERM`, `SIGINT` and `SIGHUP` to both children;
- stops the other child as soon as either one exits, for any reason;
- exits non-zero when a child stopped on its own — even if that child exited `0`, because the
  container would otherwise stay up serving a half-broken stack;
- gives children `APP_IMAGE_SHUTDOWN_TIMEOUT_SECONDS` (default 15) to drain, then `SIGKILL`s them;
- escalates to `SIGKILL` immediately on a second signal;
- reaps every child it started.

It is bash rather than POSIX `sh` because the coordination it needs is "block until *any* child
exits, and tell me which one". In `sh` that means polling — which cannot tell a running child from an
unreaped zombie, since `kill -0` succeeds for both — or a FIFO wrapper that puts a subshell between
PID 1 and the child and breaks signal forwarding. `wait -n -p` answers it exactly, with no race and
no extra process. bash is already in the Debian-based `aspnet` image; the Dockerfile asserts it at
build time. Moving to a chiseled or distroless base means replacing both this script and the health
check.

Each child is started with `exec` inside its own subshell, so signals reach `dotnet` directly with no
intermediate shell, and with its own working directory, which is what ASP.NET Core uses as the
content root — that is how each process finds its own `appsettings.json` and not the other's.

The container runs as the base image's non-root `$APP_UID` account.

---

## Environment variables

Host configuration. Each also has the standard ASP.NET Core spelling
(`AppImage__Api__Destination`, …) and a default in `appsettings.json`.

| Variable                                | Default                  | Meaning                                                     |
| --------------------------------------- | ------------------------ | ----------------------------------------------------------- |
| `APP_IMAGE_WEB_ROOT`                    | `/app/web`               | Directory holding the built React app                        |
| `APP_IMAGE_API_DESTINATION`             | `http://127.0.0.1:5000/` | Internal API base address                                    |
| `APP_IMAGE_API_PATH_PREFIX`             | `/api`                   | Public path prefix owned by the API                          |
| `APP_IMAGE_API_STRIP_PATH_PREFIX`       | `true`                   | Remove the prefix before forwarding — see the note above     |
| `APP_IMAGE_API_HEALTH_PATH`             | `/health`                | API path used by the readiness probe                         |
| `APP_IMAGE_API_HEALTH_TIMEOUT_SECONDS`  | `5`                      | Readiness probe timeout                                      |
| `APP_IMAGE_TRUST_ALL_PROXIES`           | `false`                  | Accept `X-Forwarded-*` from any proxy, not only loopback     |

Container/entrypoint:

| Variable                                | Default                  | Meaning                                       |
| --------------------------------------- | ------------------------ | --------------------------------------------- |
| `APP_IMAGE_API_URLS`                    | `http://127.0.0.1:5000`  | Where the API binds. Keep it on loopback.     |
| `APP_IMAGE_HOST_URLS`                   | `http://0.0.0.0:8080`    | Where the host binds                          |
| `APP_IMAGE_API_ENVIRONMENT`             | `Production`             | `ASPNETCORE_ENVIRONMENT` for the API only     |
| `APP_IMAGE_HOST_ENVIRONMENT`            | `Production`             | `ASPNETCORE_ENVIRONMENT` for the host only    |
| `APP_IMAGE_SHUTDOWN_TIMEOUT_SECONDS`    | `15`                     | Drain window before `SIGKILL`                 |
| `Serilog__*`                            | unset                    | Any value disables the API's default console sink — see [API logging](#api-logging) |
| `APP_IMAGE_HEALTHCHECK_URL`             | `http://127.0.0.1:8080/health/ready` | What the container health check probes |

`ASPNETCORE_URLS` set on the container is deliberately ignored: the entrypoint sets it inline per
process and unsets `ASPNETCORE_HTTP_PORTS`, `ASPNETCORE_HTTPS_PORTS` and `DOTNET_URLS`, so one
process's environment cannot move the other's listener or make both fight over 8080.

The host refuses to start (exit `78`, `EX_CONFIG`) with a message naming the offending setting when
the web root is missing, `index.html` is missing, or the API destination is not an absolute http(s)
URL. No secrets are logged.

---

## Local development

**Unchanged.** Nothing in this project is a prerequisite for it.

```bash
nx serve api    # http://localhost:5080
nx serve web    # http://localhost:3000
```

Local development keeps using an externally managed NGINX in front of those two, giving the same
routing shape as production, so `fetch("/api/...")` works identically in both.

```nginx
# Development only. Mirrors the production routing above.
server {
    listen 8080;
    server_name localhost;

    # Same rule as the app host: /api is the API's, prefix stripped.
    # The trailing slash on proxy_pass is what removes /api — apps/api serves /shares, not /api/shares.
    location /api/ {
        proxy_pass http://127.0.0.1:5080/;

        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host  $host;
    }

    # Everything else goes to the Vite dev server, which serves index.html for client routes itself.
    location / {
        proxy_pass http://127.0.0.1:3000;

        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # Vite HMR is a WebSocket; without these the page reloads instead of hot-updating.
        proxy_set_header Upgrade    $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
        proxy_buffering off;
    }
}
```

Ports are taken from the repository: `5080` is the `http` launch profile in
`apps/api/Properties/launchSettings.json`, `3000` is `server.port` in `apps/web/vite.config.ts`.

Two differences from production are expected and fine: NGINX proxies to the Vite dev server rather
than to static files, so HMR works; and NGINX has no `/health` of its own — `/api/health` still
reaches the API either way.

---

## Layout

```
apps/app-image/
├── AppImage.Host.csproj
├── Program.cs                      # composition + middleware order
├── Configuration/
│   ├── AppImageOptions.cs          # the settings, with their defaults
│   ├── AppImageOptionsValidator.cs # startup validation → ValidatedAppImageOptions
│   └── EnvironmentConfiguration.cs # APP_IMAGE_* → AppImage:* configuration keys
├── Proxy/ProxyConfiguration.cs     # YARP routes and cluster
├── Spa/
│   ├── WebAssets.cs                # the React build output on disk
│   ├── SpaCacheHeaders.cs          # immutable assets vs. revalidated document
│   └── SpaFallbackHandler.cs       # client routes, 404s, and the /api guard
├── Health/
│   ├── HealthTags.cs
│   ├── SpaAssetsHealthCheck.cs
│   ├── ApiReachabilityHealthCheck.cs
│   └── HealthProbe.cs              # the container HEALTHCHECK command
├── Dockerfile
├── docker-entrypoint.sh
├── appsettings.json
├── appsettings.Production.json
├── project.json
└── tools/
    ├── stage-artifacts.sh          # collects the three build outputs into the build context
    ├── docker-build.sh
    └── docker-run.sh
```
