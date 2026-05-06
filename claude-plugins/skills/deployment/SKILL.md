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

`Backend/Caddyfile`:

```
api.<your-domain> {
    tls /etc/caddy/certs/origin.pem /etc/caddy/certs/origin-key.pem
    reverse_proxy backend:8080
}

admin.<your-domain> {
    tls /etc/caddy/certs/origin.pem /etc/caddy/certs/origin-key.pem
    reverse_proxy admin:80
}
```

Both subdomains share a single Cloudflare origin cert with both names as SANs (set up in Terraform — see below).

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
4. **Run EF migrations** via SSH tunnel to the VPS Postgres port (so prod schema updates before the new backend starts).
5. **Sync compose + Caddyfile** with `envsubst` to inject image tags + secrets, then `scp` to `/opt/<your-app>/`.
6. **Deploy**: `ssh ... "docker compose pull backend && docker compose up -d"` (compose's healthcheck-aware deps roll the new container only after the new image is healthy).

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
- **CORS `FrontendUrl`.** The backend's `AppSettings__Spiderly.Shared__FrontendUrl` must point to the admin subdomain, not a build-preview URL. Update it when you cut over from staging hosting.
- **First deploy ordering.** The backend compose references `${ADMIN_IMAGE}`. On a fresh VPS, that image must exist in GHCR before backend deploys, or the admin service definition fails to pull. Push admin first, or run the admin workflow once before the first backend deploy.
- **`docker compose up -d` brings up everything.** When a backend deploy uses `up -d` without a service name, it'll also bring up `admin` if it's not running. That's usually desired — but means a backend-only commit can replace the admin container. The healthcheck-gated `depends_on` keeps this safe.
- **Static assets caching.** Hashed Angular bundles (e.g. `main.abc123.js`) can be cached forever; `index.html` must never cache, or users will get a stale shell after a deploy. The Caddyfile in this skill already handles both cases.
- **`tls_private_key` lives in Terraform state in cleartext.** R2 encryption-at-rest is bucket-level; anyone with R2 API access (or a leaked state file) can read the key. Scope the R2 token tightly, audit who can pull state, and never copy `terraform.tfstate` to laptops or shared drives. Rotating the cert means a fresh `terraform apply` followed by re-pasting the new outputs into GitHub Secrets — plan a maintenance window.
- **Cloudflare in front of Vercel must stay "DNS only".** When the Next.js storefront (or a Vercel-hosted admin) deploys to Vercel and DNS is on Cloudflare, those records must be **DNS only** (grey cloud), not proxied. Vercel terminates TLS and runs its own edge — CDN, image optimization, ISR, PPR — so adding the Cloudflare proxy on top breaks the TLS handshake and double-caches/conflicts with Vercel's edge features. Cloudflare's WAF/cache/analytics/Turnstile belong on the **Hetzner-served** records (`api.*`, `admin.*`) where the orange cloud stays on. If you want Cloudflare features in front of Vercel anyway, that's an advanced setup ("Full (strict)" SSL + custom hostnames) that Vercel does not officially recommend.
