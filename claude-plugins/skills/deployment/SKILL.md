---
name: deployment
description: Deploy a Spiderly project to your own infrastructure with Docker, Caddy, and Terraform. Use when setting up production hosting for the .NET backend and Angular admin, configuring CI/CD, managing TLS with Cloudflare origin certificates, or laying out infrastructure-as-code for a Spiderly app.
---

# Deployment

Spiderly is Docker-first by design (see `ai-agentic-design`). The recommended production setup is a single VPS running Docker Compose, fronted by Caddy and Cloudflare, with all infrastructure declared in Terraform.

## Recommended stack

| Tier | Choice | Why |
|---|---|---|
| Compute | **Hetzner Cloud** (or any VPS) | Predictable monthly cost, full control, no PaaS lock-in |
| Orchestration | **Docker Compose** | Single-host simplicity; matches Spiderly's Docker-first philosophy |
| Reverse proxy / TLS | **Caddy v2** | Auto-config from Cloudflare origin certs, simple Caddyfile |
| DNS / WAF / CDN | **Cloudflare** (orange-cloud) | DDoS protection, origin certs, Turnstile |
| IaC | **Terraform** | Declarative; one source of truth for VPS + DNS + certs |
| State backend | **Cloudflare R2** | S3-compatible, free tier, encrypted at rest |
| Container registry | **GitHub Container Registry (GHCR)** | Free for public/internal repos, native to GitHub Actions |
| CI/CD | **GitHub Actions** | Build, push, SSH-deploy in one workflow file |

## What to host where

- **.NET Backend** → Hetzner+Docker (recommended)
- **Angular admin** → Hetzner+Docker, **same VPS** as the backend, served by the same Caddy on a separate subdomain
- **Next.js storefront (if applicable)** → **Vercel recommended**. Next.js + Vercel gives you ISR, edge caching, image optimization, PPR, and instant preview URLs. Hetzner+Docker for SSR is viable but loses these features. Use Hetzner only if you need a single ops surface.

## Compose stack shape

`Backend/docker-compose.prod.yml` (placed alongside the backend project):

```yaml
services:
  caddy:
    image: caddy:2
    command: caddy run --config /etc/caddy/Caddyfile --watch
    ports: ["80:80", "443:443"]
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - ./certs:/etc/caddy/certs:ro
      - caddy_data:/data
    depends_on:
      backend:
        condition: service_started
      admin:
        condition: service_healthy
    restart: unless-stopped

  backend:
    image: ${BACKEND_IMAGE}
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: "8080"
      AppSettings__Spiderly.Shared__ConnectionString: "Host=postgres;Port=5432;Database=${DB_NAME};Username=postgres;Password=${DB_PASSWORD};SSL Mode=Disable;"
      # ... plus storage / mail / OAuth env vars (see file-storage skill for the storage set)
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 5s
      start_period: 60s
      retries: 3
    depends_on:
      postgres: { condition: service_healthy }
    restart: unless-stopped

  admin:
    image: ${ADMIN_IMAGE}
    expose: ["80"]
    healthcheck:
      test: ["CMD", "wget", "-q", "--spider", "http://localhost/"]
      interval: 30s
      timeout: 5s
      start_period: 10s
      retries: 3
    restart: unless-stopped

  postgres:
    image: postgres:18
    environment:
      POSTGRES_DB: ${DB_NAME}
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes: [postgres_data:/var/lib/postgresql/data]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      retries: 5
    restart: unless-stopped

volumes:
  caddy_data:
  postgres_data:
```

**Key points:**
- Only `caddy` binds host ports. `backend` and `admin` are reachable only on the internal Docker network — Caddy reverse-proxies to them by service name.
- `caddy.depends_on` uses `condition: service_healthy` for the admin container (so Caddy doesn't 502 before the inner Caddy has bound port 80) but `condition: service_started` for the backend — the backend's `/health` endpoint only goes green after EF migrations and warmup, and gating Caddy on that would block all traffic for 20–30 s on every restart.
- Image tags come from CI via envsubst (`${BACKEND_IMAGE}`, `${ADMIN_IMAGE}`).

## Caddy site blocks

`Backend/Caddyfile` — extracts compression + security headers into a `(common)` snippet so both site blocks stay DRY. Cloudflare also sets some of these (HSTS, Brotli) at its edge, but defense-in-depth at the origin is cheap and keeps origin→Cloudflare bandwidth compressed:

```
(common) {
    encode zstd gzip
    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains; preload"
        X-Content-Type-Options nosniff
        X-Frame-Options DENY
        Referrer-Policy strict-origin-when-cross-origin
        -Server
    }
}

api.<your-domain> {
    import common
    tls /etc/caddy/certs/origin.pem /etc/caddy/certs/origin-key.pem
    reverse_proxy backend:8080
}

admin.<your-domain> {
    import common
    tls /etc/caddy/certs/origin.pem /etc/caddy/certs/origin-key.pem
    reverse_proxy admin:80
}
```

Both subdomains share a single Cloudflare origin cert with both names as SANs (set up in Terraform — see below).

## Backend Dockerfile

`Backend/Dockerfile` (multi-stage SDK build → aspnet runtime):

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0.102 AS build
WORKDIR /src
COPY ["<YourApp>.WebAPI/<YourApp>.WebAPI.csproj", "<YourApp>.WebAPI/"]
# ... copy other csprojs, restore, copy source, publish ...
RUN dotnet publish "<YourApp>.WebAPI/<YourApp>.WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0.1
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "<YourApp>.WebAPI.dll"]
```

**Pin patch versions** (`9.0.102` SDK, `9.0.1` runtime) — `:9.0` floats and breaks reproducible builds.

**Run as non-root.** The aspnet image ships an `app` user (UID 1654). Add `USER app` after `COPY` for defense-in-depth — limits the blast radius of a container escape and matches the platform's sandbox model.

**Caveat — log volumes:** if you're using a Serilog **File sink** (not just Console) with a Hangfire log-archival job — i.e., the app needs to *write* to `/app/logs` — a Docker named volume mount overlays that path with root-owned storage and `app` can't write. Two options:
- **Bind mount with chowned host dir** (recommended): `volumes: - /var/log/<your-app>:/app/logs` in compose, plus a one-time `mkdir -p /var/log/<your-app> && chown 1654:1654 /var/log/<your-app>` on the VPS. Bind mounts respect host ownership.
- **No File sink** (simplest): rely on Console sink + Docker's `json-file` driver (50 MB × 3 files rotation already in compose — see below). Access logs via `docker logs`. Drop the `/app/logs` volume entirely.

Console-only is right for greenfield deploys; file-sink-with-archival is right when you have a long-term log retention requirement and ship to S3/R2.

## Angular admin Dockerfile

`Frontend/Dockerfile` (multi-stage Node build → Caddy alpine runtime):

```dockerfile
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM caddy:2-alpine
COPY --from=build /app/dist/<YourApp>/browser /srv
COPY Caddyfile /etc/caddy/Caddyfile
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://localhost/ || exit 1
```

`Frontend/Caddyfile` (internal — runs inside the admin container):

```
:80 {
    root * /srv
    encode zstd gzip

    @hashedAssets {
        path_regexp hashed \.[a-f0-9]{8,}\.(js|css|woff2?|ttf|otf|svg|png|jpg|jpeg|webp|ico)$
    }
    header @hashedAssets Cache-Control "public, max-age=31536000, immutable"

    @indexHtml path /index.html
    header @indexHtml Cache-Control "no-cache, no-store, must-revalidate"

    try_files {path} /index.html
    file_server
}
```

The `try_files {path} /index.html` line is the SPA fallback that lets the Angular router handle deep links.

**Lockfile gotcha:** `npm ci` requires `package.json` and `package-lock.json` to be in sync. If a stale dev-dep pins an older Angular major as a peer (e.g. `@jsverse/transloco-keys-manager` 5.x pins Angular 17 while your project is on Angular 19), `npm ci` fails. Bump the dev-dep to the matching major (e.g. `transloco-keys-manager` 6.x for Angular 19) rather than reaching for `--legacy-peer-deps`.

## Terraform layout

Split files by provider concern; one provider, one or two files:

```
infrastructure/
├── main.tf                         # required_providers + state backend
├── variables.tf
├── outputs.tf
├── hetzner-firewall.tf             # firewall, allowed ports
├── cloudflare-dns.tf               # api/admin A records
├── cloudflare-origin-cert.tf       # origin CA cert + private key
└── cloudflare-zone.tf              # zone + headers
```

`main.tf` providers:

```hcl
terraform {
  required_version = ">= 1.5"

  backend "s3" {
    # Cloudflare R2 — S3-compatible
    bucket = "<your-app>-terraform-state"
    key    = "infrastructure/terraform.tfstate"
    region = "auto"
    skip_credentials_validation = true
    skip_metadata_api_check     = true
    skip_region_validation      = true
    skip_requesting_account_id  = true
    skip_s3_checksum            = true
    endpoints = { s3 = "https://<account-id>.r2.cloudflarestorage.com" }
  }

  required_providers {
    cloudflare = { source = "cloudflare/cloudflare", version = "~> 5.0" }
    hcloud     = { source = "hetznercloud/hcloud", version = "~> 1.49" }
    tls        = { source = "hashicorp/tls", version = "~> 4.0" }
  }
}
```

Origin cert covering both `api` and `admin` subdomains (`cloudflare-origin-cert.tf`):

```hcl
resource "tls_private_key" "origin" {
  algorithm = "RSA"
  rsa_bits  = 2048

  lifecycle {
    prevent_destroy = true
  }
}

resource "tls_cert_request" "origin" {
  private_key_pem = tls_private_key.origin.private_key_pem

  subject {
    common_name  = "<your-domain>"
    organization = "<YourApp>"
  }

  dns_names = [
    "api.<your-domain>",
    "admin.<your-domain>",
  ]
}

resource "cloudflare_origin_ca_certificate" "origin" {
  csr                = tls_cert_request.origin.cert_request_pem
  hostnames          = tls_cert_request.origin.dns_names
  request_type       = "origin-rsa"
  requested_validity = 5475 # 15 years

  lifecycle {
    prevent_destroy = true
    # Cloudflare normalizes hostname/CSR ordering server-side; ignore both to avoid spurious recreate.
    ignore_changes = [hostnames, csr]
  }
}
```

Sensitive outputs (`outputs.tf`) so the deploy workflow can pull cert + key into GitHub Secrets:

```hcl
output "origin_cert" {
  value     = cloudflare_origin_ca_certificate.origin.certificate
  sensitive = true
}
output "origin_cert_key" {
  value     = tls_private_key.origin.private_key_pem
  sensitive = true
}
```

Retrieve with `terraform output -raw origin_cert` and paste into a GitHub Secret. **Note:** the API token must have `Origin CA: Edit` scope for `cloudflare_origin_ca_certificate` to work.

## CI/CD

Two workflows — one per deploy unit. Both should share a `concurrency` group so they serialize on the same VPS:

```yaml
# .github/workflows/deploy-backend.yml
name: Deploy Backend
on:
  push:
    branches: [main]
    paths: ['Backend/**', '.github/workflows/deploy-backend.yml']

concurrency:
  group: <your-app>-deploy
  cancel-in-progress: false

env:
  IMAGE_NAME:       ghcr.io/<your-user>/<your-app>-backend
  ADMIN_IMAGE_NAME: ghcr.io/<your-user>/<your-app>-admin
```

Steps (typical sequence):

1. **Run tests** (gate the deploy on green tests).
2. **Build + push image** to GHCR (`docker/build-push-action@v6` with GHA cache).
3. **SSH key setup** + `ssh-keyscan` to trust the host.
4. **Run EF migrations** via SSH tunnel to the VPS Postgres port (so prod schema updates before the new backend starts). Set the cleanup trap *before* opening the tunnel so an early ssh failure doesn't leak the trap:
   ```bash
   trap 'pkill -f "ssh -o ExitOnForwardFailure=yes -fN -L 5432" || true' EXIT
   ssh -o ExitOnForwardFailure=yes -fN -L 5432:127.0.0.1:5432 root@host
   for i in {1..30}; do nc -z localhost 5432 && break; sleep 1; done
   nc -z localhost 5432 || { echo "::error::SSH tunnel never came up after 30s"; exit 1; }
   ```
   The explicit `nc` check after the loop is what turns a timed-out tunnel into a clear failure — without it, `dotnet ef` runs against a dead port and reports a confusing "connection refused" instead.
5. **Sync compose + Caddyfile** with `envsubst` to inject image tags + secrets, then `scp` to `/opt/<your-app>/`.
6. **Deploy**: `ssh ... "docker compose pull backend && docker compose up -d backend caddy"`. Scope to just the services you're updating — unscoped `up -d` will also bounce admin/postgres on every backend push, which is rarely what you want.

The admin workflow is similar but lighter: build → push → ssh → `docker compose pull admin && docker compose up -d admin`. **Don't** restart Caddy after an admin update — Caddy resolves `admin:80` via Docker DNS at request time and picks up the new container automatically.

For migration mechanics (creating migrations, the dedicated `*.Migrations` startup-project pattern, why direct DDL on prod is forbidden), see the `ef-migrations` skill.

## Pitfalls

- **Cookie domain across subdomains.** Set `CookieDomain` to `.<your-domain>` (leading dot) so cookies set by `api.<your-domain>` are accepted by `admin.<your-domain>`. `SameSite=Lax` is the right default for an admin SPA.
- **`ForwardLimit = 2`.** With Cloudflare → Caddy → backend, your forwarded headers cross two proxies. The Spiderly scaffold ships `appsettings.Production.json` with `ForwardLimit: 2` already set; if you sit behind Cloudflare, paste the current Cloudflare CIDR list into `TrustedProxyNetworks` (refresh from https://www.cloudflare.com/ips/ periodically — Cloudflare adds ranges occasionally):

  ```json
  {
    "AppSettings": {
      "Spiderly.Shared": {
        "ForwardLimit": 2,
        "TrustedProxyNetworks": [
          "173.245.48.0/20",
          "103.21.244.0/22",
          "103.22.200.0/22",
          "103.31.4.0/22",
          "141.101.64.0/18",
          "108.162.192.0/18",
          "190.93.240.0/20",
          "188.114.96.0/20",
          "197.234.240.0/22",
          "198.41.128.0/17",
          "162.158.0.0/15",
          "104.16.0.0/13",
          "104.24.0.0/14",
          "172.64.0.0/13",
          "131.0.72.0/22",
          "2400:cb00::/32",
          "2606:4700::/32",
          "2803:f800::/32",
          "2405:b500::/32",
          "2405:8100::/32",
          "2a06:98c0::/29",
          "2c0f:f248::/32"
        ]
      }
    }
  }
  ```

  When `TrustedProxyNetworks` is unset, Spiderly trusts RFC 1918 private ranges by default — fine for Docker-internal Caddy → backend traffic but **not** for the outermost Cloudflare → Caddy hop, which arrives over public IPs.

  On the Terraform side (Hetzner firewall, etc.), use the **`cloudflare_ip_ranges` data source** instead of a hardcoded list — keeps the firewall in sync with Cloudflare's published ranges automatically:

  ```hcl
  data "cloudflare_ip_ranges" "this" {}

  resource "hcloud_firewall" "main" {
    rule {
      direction  = "in"
      protocol   = "tcp"
      port       = "443"
      source_ips = concat(data.cloudflare_ip_ranges.this.ipv4_cidrs, data.cloudflare_ip_ranges.this.ipv6_cidrs)
    }
    # ...
  }
  ```

  The .NET `TrustedProxyNetworks` list still has to be hardcoded — there's no equivalent runtime data source short of fetching at startup. The hardcoded list and the Terraform data source describe the same set, so both rot at the same speed; pin a calendar reminder to refresh quarterly.
- **CORS `FrontendUrl`.** The backend's `AppSettings__Spiderly.Shared__FrontendUrl` must point to the admin subdomain, not a build-preview URL. Update it when you cut over from staging hosting.
- **First deploy ordering.** The backend compose references `${ADMIN_IMAGE}`. On a fresh VPS, that image must exist in GHCR before backend deploys, or the admin service definition fails to pull. Push admin first, or run the admin workflow once before the first backend deploy.
- **`docker compose up -d` scoping.** Always scope to the services you're updating: `docker compose up -d backend caddy` for backend deploys, `docker compose up -d admin` for admin deploys. Unscoped `up -d` bounces every service that's currently down or has a config change — including admin and postgres on a backend-only commit. The healthcheck-gated `depends_on` makes the unscoped form *safe* but not *desirable*.
- **Static assets caching.** Hashed Angular bundles (e.g. `main.abc123.js`) can be cached forever; `index.html` must never cache, or users will get a stale shell after a deploy. The Caddyfile in this skill already handles both cases.
- **`tls_private_key` lives in Terraform state in cleartext.** R2 encryption-at-rest is bucket-level; anyone with R2 API access (or a leaked state file) can read the key. Scope the R2 token tightly, audit who can pull state, and never copy `terraform.tfstate` to laptops or shared drives. Rotating the cert means a fresh `terraform apply` followed by re-pasting the new outputs into GitHub Secrets — plan a maintenance window. Add `lifecycle { prevent_destroy = true }` to the `tls_private_key` and `cloudflare_origin_ca_certificate` resources so accidental config changes can't trigger a silent rotation. Also add `ignore_changes = [hostnames, csr]` on the cert — Cloudflare normalizes hostname/CSR ordering server-side, otherwise every plan would show a phantom recreate.
- **Postgres data on Docker named volumes is not durable across server replacement.** If the VPS itself is Terraform-managed (`hcloud_server`) and gets recreated for any reason — image change, server_type change, accidental destroy — the local Docker named volume `postgres_data` goes with it. Add `lifecycle { prevent_destroy = true }` to the `hcloud_server` resource. For genuine durability, attach an `hcloud_volume` and bind-mount it into postgres (`/var/lib/postgresql/data`); volumes survive server destruction and snapshot independently.
- **Cloudflare in front of Vercel must stay "DNS only".** When the Next.js storefront (or a Vercel-hosted admin) deploys to Vercel and DNS is on Cloudflare, those records must be **DNS only** (grey cloud), not proxied. Vercel terminates TLS and runs its own edge — CDN, image optimization, ISR, PPR — so adding the Cloudflare proxy on top breaks the TLS handshake and double-caches/conflicts with Vercel's edge features. Cloudflare's WAF/cache/analytics/Turnstile belong on the **Hetzner-served** records (`api.*`, `admin.*`) where the orange cloud stays on. If you want Cloudflare features in front of Vercel anyway, that's an advanced setup ("Full (strict)" SSL + custom hostnames) that Vercel does not officially recommend.
