# Share

A folder-sharing service for developers. Run a CLI inside any folder, get a public URL with a configurable TTL. Optimized for ongoing shared working folders (continuous push-update semantics), not just one-shot transfers.

## Product summary

A developer installs a small Rust CLI (one-time, via a hosted install script that detects platform and downloads the right binary). They authenticate with an API key issued manually by an admin. From any folder, they can:

- **One-shot share** — `shareit` uploads the current folder, prints a URL, exits.
- **Persistent share** — `shareit init` registers the folder as a long-lived share; `shareit push` updates it with whatever has changed; `shareit url`, `shareit ls`, `shareit rm` round out the lifecycle.

Invite-only. API keys are issued manually and revocable. Downloads go through the API (private R2 bucket + presigned URLs) so expiry is honored exactly.

## Key decisions

Full history is in the Decisions Log. The headline calls:

- **Upload to VPS, not tunnel** — laptop can close, share stays alive.
- **Cloudflare R2** — zero egress fees, S3-compatible API.
- **Turso (libSQL)** — managed SQLite, no backup/durability ops on the VPS. Concurrent-write rate at team scale is well within Turso's serialized-writer model.
- **API keys (not Auth0/Keycloak)** — invite-only team tool, ~10 users. Manual issuance with `key_hash` storage, `last_used_at`, revocation. Appropriate scale, no third-party auth dependency.
- **Private R2 bucket, presigned downloads via API 302** — expiry honored exactly; no leak window. API stays out of the data path (it returns a redirect, not bytes).
- **Next.js for API + SPA together** — one process, no CORS, simpler deploy.
- **Rust CLI** — single static binary distribution, explicit learning goal. Distributed via GitHub Releases + hosted install script.
- **Cleaner is a separate systemd service touching Turso only** — process isolation; R2 lifecycle handles blob cleanup as a self-healing backstop.
- **CLI local state in SQLite** — diff semantics, share registry, per-file (path, size, mtime, sha256) tracking. Better than YAML for concurrent CLI invocations and indexed lookups.

## Data model

### Server-side (Turso/PostGreSQL)

```sql
CREATE TABLE users (
  id TEXT PRIMARY KEY,           -- e.g. "u_alice"
  name TEXT NOT NULL,
  email TEXT,
  created_at INTEGER NOT NULL
);

CREATE TABLE api_keys (
  id TEXT PRIMARY KEY,           -- "k_<nanoid>"
  user_id TEXT NOT NULL REFERENCES users(id),
  key_hash TEXT NOT NULL,        -- SHA-256 of the raw key
  prefix TEXT NOT NULL,          -- first 8 chars of the key (for identification)
  label TEXT,                    -- "laptop", "ci", etc.
  created_at INTEGER NOT NULL,
  last_used_at INTEGER,
  revoked_at INTEGER             -- NULL = active
);

CREATE TABLE shares (
  id TEXT PRIMARY KEY,           -- nano-id, used in URL
  owner_user_id TEXT NOT NULL REFERENCES users(id),
  status TEXT NOT NULL,          -- 'pending' (initial only) | 'ready' | 'expired'
  created_at INTEGER NOT NULL,
  updated_at INTEGER NOT NULL,
  expires_at INTEGER NOT NULL,
  configured_ttl_minutes INTEGER NOT NULL,  -- the TTL the share is configured to reset to on push
  total_bytes INTEGER NOT NULL,
  file_count INTEGER NOT NULL
);

CREATE INDEX idx_shares_expires_at ON shares(expires_at) WHERE status = 'ready';
CREATE INDEX idx_shares_owner ON shares(owner_user_id);

CREATE TABLE files (
  share_id TEXT NOT NULL,
  relative_path TEXT NOT NULL,
  size INTEGER NOT NULL,
  sha256 TEXT NOT NULL,
  content_type TEXT,
  updated_at INTEGER NOT NULL,
  PRIMARY KEY (share_id, relative_path),
  FOREIGN KEY (share_id) REFERENCES shares(id) ON DELETE CASCADE
);
```

### CLI-side (local SQLite)

```sql
CREATE TABLE shares (
  share_id TEXT PRIMARY KEY,
  local_path TEXT NOT NULL,      -- absolute path to the folder root
  server_url TEXT NOT NULL,
  configured_ttl_minutes INTEGER NOT NULL,
  last_pushed_at INTEGER,
  share_url TEXT NOT NULL        -- cached for shareit url
);

CREATE TABLE files (
  share_id TEXT NOT NULL,
  relative_path TEXT NOT NULL,
  size INTEGER NOT NULL,
  mtime INTEGER NOT NULL,        -- fs mtime nanos
  sha256 TEXT NOT NULL,
  PRIMARY KEY (share_id, relative_path)
);
```

### Per-folder config (.shareit/config.toml)

```toml
share_id = "abc123"
server_url = "https://share.teamfullstack.io"
```

Nothing else lives in the folder. Hashes, mtimes, last-push time all live in the global SQLite, keyed by share_id. A .shareitignore file (gitignore-syntax) can sit in the folder for exclusions and is intended to be checked into version control if the folder is also a git repo.

### R2 object layout

```
shareit-blobs/
  <share_id>/
    <relative_path>     # e.g. shareit-blobs/abc123/src/main.rs
```

Bucket is private. All access via presigned URLs. Lifecycle rule deletes objects older than 16 days as a safety net for any orphans.

## Upload flow — in detail

There are two upload paths: initial creation and update-an-existing-share. They differ in semantics but share most of the wire protocol.

### Path A — shareit (one-shot) / shareit init (creates persistent share)

Both create a new share. The difference is purely client-side bookkeeping: init writes a .shareit/config.toml and stores the share in local SQLite; one-shot does not.

#### Step 1: CLI walks the directory

- Respects `.shareitignore` (and optionally `.gitignore` — to be decided per-feature flag).
- Builds an in-memory list of `(relative_path, size, content_type, sha256)`.
- Hashes every file the first time (no local cache yet).
- Validates: no `..`, no absolute paths, no null bytes, no symlinks pointing outside the folder.

#### Step 2: CLI calls POST /api/shares

```json
{
  "ttl_minutes": 240,
  "files": [
    { "relative_path": "src/main.rs", "size": 1234, "sha256": "...", "content_type": "text/x-rust" },
    ...
  ]
}
```

Server-side:

1. Authenticate the API key.
2. Validate caller limits (active shares per user, files per share, total bytes).
3. Generate share_id (nanoid).
4. Insert `shares` row with `status='pending'`, the requested `expires_at`, and `configured_ttl_minutes`.
5. Insert `files` rows for every file in the request.
6. For each file, generate a short-lived (e.g. 1-hour) presigned PUT URL targeting `<share_id>/<relative_path>` on R2. Include `Content-Length` and `x-amz-content-sha256` headers in the signature so R2 enforces integrity on upload.
7. Return:

   ```json
   {
     "share_id": "abc123",
     "share_url": "https://share.teamfullstack.io/s/abc123",
     "expires_at": 1735000000,
     "uploads": [
       { "relative_path": "src/main.rs", "url": "https://r2.../...", "headers": { ... } }
     ]
   }
   ```

#### Step 3: CLI uploads files directly to R2

- Bounded concurrency (default 4 parallel uploads). Configurable via `--parallel`.
- Streaming reads — never load a file fully into memory.
- Per-file retries with exponential backoff on transient failures (network, 5xx).
- Aggregate progress bar.
- If R2 rejects a file (size mismatch, sha256 mismatch — possible with the headers above), the CLI surfaces the error and does not call finalize.

#### Step 4: CLI calls POST /api/shares/:id/finalize

Server-side:

1. Authenticate; verify caller owns the share.
2. Verify share is `pending` (idempotency: finalize on a `ready` share is a no-op success).
3. HEAD-check a sample of R2 objects (e.g. 5 random, plus the largest and smallest) to confirm uploads landed. A full check on large shares is expensive; sampling catches the common failure modes ("client called finalize without actually uploading").
4. Update `shares` row: `status='ready'`, `updated_at=now()`.
5. Return `{ share_id, share_url, expires_at, total_bytes, file_count }`.

If finalize fails verification, the share stays `pending`. The CLI can retry. The cleaner will eventually expire and remove unfinalized shares.

### Path B — shareit push (update an existing share)

push sends a full state snapshot. The server diffs against current state and tells the client what to upload. Deletes happen on finalize.

#### Step 1: CLI computes current state

- Walks the directory.
- For each file: compare to local SQLite `files` table.
  - If size + mtime match the stored row → use the cached sha256 (skip hashing).
  - Otherwise → re-hash.
- Result: an authoritative list of `(relative_path, size, sha256)` for the current state.

#### Step 2: CLI calls PUT /api/shares/:id/state

```json
{
  "reset_ttl": true,         // default true; --keep-ttl sends false
  "ttl_minutes": null,        // optional override
  "files": [
    { "relative_path": "src/main.rs", "size": 1234, "sha256": "abc..." },
    ...
  ]
}
```

Server-side:

1. Authenticate; verify caller owns the share.
2. Diff client state against `files` table for this share:
   - **Uploads needed**: files where client's sha256 differs from server's, or where the file doesn't exist on the server yet.
   - **Deletes pending**: server-side files not in the client's list.
3. **Safety check**: if `deletes_pending.length > max(5, file_count * 0.5)`, return a `409 Confirm Required` with the diff summary. CLI re-sends with `?confirm=true` after user approval. This is the "oh no I cd'd into the wrong folder" guard.
4. If accepted, generate presigned PUT URLs for the uploads (same as Path A, step 2).
5. Return:

   ```json
   {
     "push_id": "p_xyz",
     "uploads": [...],
     "deletes_count": 3,
     "unchanged_count": 42
   }
   ```

The server does NOT update Turso yet. Current state is preserved; downloads of the existing share continue to work normally during the push.

#### Step 3: CLI uploads changed files directly to R2

- Same mechanics as Path A.
- Uploads go directly to the final R2 path `<share_id>/<relative_path>`, **overwriting** the existing object.

##### A subtlety worth being explicit about

There is a brief window during step 3 where R2 has new bytes for some files but Turso still has old metadata (size, content-type). Downloads during this window get the new bytes; their `Content-Length` may not match what the API previously reported. This is benign for normal browser/curl use. It does mean:

- The download endpoint should not aggressively trust cached metadata for `Content-Length` if it can be avoided — let R2 set it on the redirect response. Or, accept the small inconsistency and document it.
- After finalize, all metadata is correct.

This trade-off is deliberate: the alternative is staging uploads at separate R2 paths and doing copy-then-swap on finalize, which (a) requires R2 copy operations on every changed file, (b) doubles transient storage, and (c) isn't worth it for team-scale internal use.

#### Step 4: CLI calls POST /api/shares/:id/finalize

With the `push_id` from step 2.

Server-side, in a single Turso transaction:

1. Authenticate; verify caller owns the share.
2. HEAD-sample the newly-uploaded R2 objects.
3. **Update**: upsert rows in `files` for everything in the push state (new files inserted, changed files have size/sha256/updated_at updated).
4. **Delete**: remove `files` rows for the deletes_pending set. The R2 objects become orphans; R2 lifecycle cleans them up within 16 days.
5. **TTL reset**: if `reset_ttl`, set `expires_at = now() + configured_ttl_minutes` (or `ttl_minutes` override). If `--keep-ttl`, leave alone.
6. `updated_at = now()`. Status stays `ready` (was already `ready` from previous finalize).

#### Step 5: CLI updates local SQLite

Mark `last_pushed_at = now()`, update cached file rows. Local state is now in sync with server.

## Failure modes during upload

| Failure                                                  | What happens                                                                                                                                   |
| -------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| CLI crashes mid-upload (initial share)                   | Share stays `pending`; cleaner expires it after TTL.                                                                                           |
| CLI crashes mid-upload (push)                            | Some R2 objects updated, Turso unchanged. Share remains usable with old metadata; next `push` self-heals (will re-detect what needs updating). |
| R2 PUT returns 5xx                                       | CLI retries with backoff per file. After max retries, surfaces error and exits without finalize.                                               |
| Presigned URL expires before upload                      | Rare with 1-hour expiry. CLI surfaces error; user re-runs. Future enhancement: refresh URLs from the API mid-push.                             |
| sha256 mismatch on R2 PUT                                | R2 rejects with 400. CLI surfaces error; user investigates (likely a file mutated mid-push).                                                   |
| Network drops during finalize                            | Finalize is idempotent on `share_id + push_id`. CLI retries safely.                                                                            |
| User runs `push` from a subdirectory                     | CLI walks up looking for `.shareit/config.toml`; errors if not found. Won't accidentally create a new share.                                   |
| User pushes from a folder where 90% of files are deleted | Server returns 409 with diff summary; CLI prompts for confirmation.                                                                            |

## Download flow — in detail

Downloads are public-by-URL but mediated by the API to enforce expiry. The R2 bucket is private; no one has direct access to its objects.

### Browser download via the SPA

#### Step 1: Browser navigates to `share.teamfullstack.io/s/abc123`

NGINX serves the Next.js app. The page is a server-rendered share-view page that fetches `/api/shares/abc123` server-side (or client-side; either works).

#### Step 2: API returns share metadata

```json
{
  "share_id": "abc123",
  "expires_at": 1735000000,
  "file_count": 12,
  "total_bytes": 348293,
  "files": [
    { "relative_path": "src/main.rs", "size": 1234, "content_type": "text/x-rust" },
    ...
  ]
}
```

No presigned URLs in this response. The SPA renders the file tree using these paths.

If the share is not `ready` or `expires_at < now()`, the API returns 404.

#### Step 3: User clicks a file in the SPA

The link points to `/api/shares/abc123/files/src/main.rs` (a regular link on the API origin, no client-side fanciness needed).

#### Step 4: API issues a 302 redirect

The download endpoint:

1. Looks up the share; if not `ready` or expired, returns 404.
2. Looks up the file row; if not found, returns 404.
3. Generates a short-lived (5-15 minute) presigned GET URL for `<share_id>/<relative_path>` on R2.
4. Sets response headers:
   - `Content-Disposition: attachment; filename="main.rs"` for risky content types (HTML, JS, SVG, etc.) to prevent inline rendering / phishing.
   - For safer types (images, plain text, PDF), inline is acceptable.
5. Returns 302 with `Location: <presigned URL>`.

#### Step 5: Browser follows the redirect, downloads from R2 directly

The bytes never traverse the VPS. The API was only briefly in the path to issue the redirect.

### curl download

```bash
curl -L -O https://share.teamfullstack.io/api/shares/abc123/files/script.sh
```

The `-L` flag is essential — it follows the 302 to R2. Without it, curl just shows the redirect response. Worth documenting prominently in the user-facing CLI/SPA help.

For scripts: `curl -L https://share.teamfullstack.io/api/shares/abc123/files/install.sh | bash` works because `-L` follows the redirect transparently.

### "Download all as tar.gz" (Phase 6 / nice-to-have)

Not in v1. When built, options are:

- **Server-side tar streaming**: API streams R2 objects through a tar encoder and into the response. CPU and bandwidth cost on the VPS proportional to share size. Acceptable for small-to-medium shares.
- **Client-side**: SPA fetches each file and zips in-browser via JS. Doesn't scale beyond a few hundred MB realistically.

Defer until someone asks for it.

### Failure modes during download

| Failure                                          | What happens                                                                                                                    |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------- |
| Share expired between page load and click        | API returns 404 on the download endpoint; SPA shows a friendly error.                                                           |
| Presigned URL expires between redirect and fetch | Browser will show R2's error response. Mitigated by short presigned lifetime + immediate-follow by browsers (rare in practice). |
| File deleted server-side (push removed it)       | 404.                                                                                                                            |
| R2 returns 5xx on actual fetch                   | Browser shows R2's response. API can't really do anything; this is a Cloudflare problem.                                        |
| Mass-downloading by viral share                  | NGINX rate limits on `/api/shares/:id/files/*` (per-IP). Bandwidth comes from R2 (zero-egress), so cost is bounded.             |

## Share lifecycle

The pending state exists only for newly-created shares before their first finalize. Pushes against ready shares never go back to pending.

# API surface (v1)

| Method | Path                       | Auth            | Purpose                                                                                          |
| ------ | -------------------------- | --------------- | ------------------------------------------------------------------------------------------------ |
| POST   | `/api/shares`              | API key         | Create new share. Returns share_id, presigned upload URLs, share_url.                            |
| PUT    | `/api/shares/:id/state`    | API key (owner) | Submit new full state for an existing share. Returns push_id + uploads needed + deletes pending. |
| POST   | `/api/shares/:id/finalize` | API key (owner) | Commit a pending share (initial) or a pending push (update).                                     |
| GET    | `/api/shares/:id`          | —               | Public. Share metadata + file list. 404 if not ready or expired.                                 |
| GET    | `/api/shares/:id/files/*`  | —               | Public. 302 to presigned R2 GET URL. 404 if not ready/expired/missing.                           |
| DELETE | `/api/shares/:id`          | API key (owner) | Mark expired immediately.                                                                        |
| GET    | `/api/shares/mine`         | API key         | List the caller's active shares.                                                                 |
| GET    | `/api/me`                  | API key         | Echo caller identity. Useful for CLI debugging.                                                  |

## Limits (v1, configurable)

- `ttl_minutes`: 1 ≤ value ≤ 21600 (15 days). Default 90.
- Files per share: ≤ 1000.
- Total bytes per share: ≤ 5 GB.
- Active shares per user: ≤ 10.
- Per-file path: no `..`, no absolute, no null bytes, ≤ 1024 chars.

# User Secrets

```bash
PEPPER="$(openssl rand -base64 32)"
R2_ACCESS_KEY_ID=""
R2_SECRET_ACCESS_KEY=""
R2_SERVICE_URL=""
R2_BUCKET_NAME="shareit-blobs"
cd apps/cli
dotnet user-secrets init
dotnet user-secrets set "ApiKey:Pepper" "$PEPPER"
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=share;Username=postgres;Password=postgres"
dotnet user-secrets set "Storage:AccessKeyId" "$R2_ACCESS_KEY_ID"
dotnet user-secrets set "Storage:SecretAccessKey" "$R2_SECRET_ACCESS_KEY"
dotnet user-secrets set "Storage:ServiceUrl" "$R2_SERVICE_URL"
dotnet user-secrets set "Storage:BucketName" "$R2_BUCKET_NAME"

cd apps/api
dotnet user-secrets init
dotnet user-secrets set "ApiKey:Pepper" "$PEPPER"
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=share;Username=postgres;Password=postgres"
dotnet user-secrets set "Storage:AccessKeyId" "$R2_ACCESS_KEY_ID"
dotnet user-secrets set "Storage:SecretAccessKey" "$R2_SECRET_ACCESS_KEY"
dotnet user-secrets set "Storage:ServiceUrl" "$R2_SERVICE_URL"
dotnet user-secrets set "Storage:BucketName" "$R2_BUCKET_NAME"
```

# Running Migrations

```bash
# On the root directory generate migrations if needed
dotnet ef migrations add Initial --project libs/infrastructure --startup-project apps/cli

# Apply the migrations
dotnet ef database update --project libs/infrastructure --startup-project apps/cli
```
