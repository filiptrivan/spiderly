---
name: backups
description: Back up and restore a Spiderly app's Postgres database, and archive logs off the VPS. Use when setting up automated database backups, scheduling pg_dump to object storage (Cloudflare R2), restoring from a backup, running a restore drill, configuring backup retention, or archiving Serilog file logs. Covers the backup and restore scripts, cron scheduling, and the R2 bucket Terraform.
---

# Backups

If you ship this stack to prod, you ship a backup with it. Postgres data lives in a Docker volume on a single VPS — disk failure, accidental `docker volume rm`, or `terraform destroy` all wipe it without a snapshot to restore from.

## Database backups (required)

Daily `pg_dump` to a Cloudflare R2 bucket, 7-day retention both local and remote. ~24h RPO; if you need tighter, layer WAL archiving on top (separate skill).

**1. R2 bucket — Terraform-managed.** No chicken-and-egg here (only the *state* bucket has to be bootstrapped manually); manage data buckets like any other resource:

```hcl
# infrastructure/cloudflare-r2.tf
resource "cloudflare_r2_bucket" "db_backups" {
  account_id = var.cloudflare_account_id
  name       = "<your-app>-db-backups"
  location   = "EEUR"

  lifecycle {
    prevent_destroy = true
  }
}
```

**2. Backup script — `infrastructure/scripts/pg_backup_s3.sh`** (deployed to `/usr/local/bin/` on the VPS). Note the `trap` cleanup, the `aws s3api list-objects-v2 --query` for retention (structured + locale-safe vs parsing `aws s3 ls` with awk), and the per-iteration warn-on-failure inside the `while` subshell (where `set -e` does NOT propagate):

```bash
#!/usr/bin/env bash
set -euo pipefail

CONTAINER_NAME="<your-app>-postgres-1"
DB_NAME="<your-db>"
DB_USER="postgres"
CLOUDFLARE_ACCOUNT_ID="<account-id>"
S3_BUCKET="s3://<your-app>-db-backups"
R2_ENDPOINT="https://${CLOUDFLARE_ACCOUNT_ID}.r2.cloudflarestorage.com"
RETENTION_DAYS=7
LOCAL_BACKUP_DIR="/var/backups/postgresql"
LOG_FILE="/var/log/<your-app>_pg_backup.log"

TIMESTAMP=$(date +%Y-%m-%d_%H%M%S)
BACKUP_FILE="<your-app>_${TIMESTAMP}.sql.gz"
LOCAL_PATH="${LOCAL_BACKUP_DIR}/${BACKUP_FILE}"

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"; }

trap 'rc=$?; [[ $rc -ne 0 ]] && rm -f "$LOCAL_PATH" && log "Aborted, removed partial $LOCAL_PATH"; exit $rc' ERR INT TERM

mkdir -p "$LOCAL_BACKUP_DIR"

if ! docker exec "$CONTAINER_NAME" pg_dump -U "$DB_USER" "$DB_NAME" | gzip > "$LOCAL_PATH"; then
    log "ERROR: pg_dump failed"; exit 1
fi
log "Backup created: ${LOCAL_PATH} ($(du -h "$LOCAL_PATH" | cut -f1))"

if ! aws s3 --endpoint-url "$R2_ENDPOINT" cp "$LOCAL_PATH" "${S3_BUCKET}/${BACKUP_FILE}"; then
    log "ERROR: S3 upload failed"; exit 1
fi
log "Uploaded to ${S3_BUCKET}/${BACKUP_FILE}"

find "$LOCAL_BACKUP_DIR" -name "<your-app>_*.sql.gz" -mtime +"$RETENTION_DAYS" -delete

CUTOFF_ISO=$(date -u -d "-${RETENTION_DAYS} days" +%Y-%m-%dT%H:%M:%SZ)
BUCKET_NAME="${S3_BUCKET#s3://}"
OLD_KEYS=$(aws s3api --endpoint-url "$R2_ENDPOINT" list-objects-v2 \
    --bucket "$BUCKET_NAME" \
    --query "Contents[?LastModified<'${CUTOFF_ISO}'].Key" \
    --output text 2>/dev/null || echo "")
if [[ -n "$OLD_KEYS" && "$OLD_KEYS" != "None" ]]; then
    for KEY in $OLD_KEYS; do
        aws s3 --endpoint-url "$R2_ENDPOINT" rm "${S3_BUCKET}/${KEY}" \
            && log "Deleted remote: ${KEY}" \
            || log "WARN: failed to delete remote ${KEY}"
    done
fi
```

**3. VPS prerequisites.** Add `awscli` and `cron` to `cloud-init` `packages:` so future server replacements have them. Configure R2 credentials in `/root/.aws/credentials` (same keys as the Terraform state backend — they're account-scoped):

```ini
[default]
aws_access_key_id = <R2 access key>
aws_secret_access_key = <R2 secret>
```

**4. Cron entry** (`/etc/cron.d/<your-app>-pg-backup`). Wrap with `flock -n` so a hung run doesn't get a second instance started 24h later:

```
0 2 * * * root /usr/bin/flock -n /var/lock/<your-app>-pg-backup.lock /usr/local/bin/pg_backup_s3.sh >> /var/log/<your-app>_pg_backup.log 2>&1
```

**5. Restore — `scripts/db-restore.sh`** (run from local). Lists server-side backups via SSH, prompts for selection, takes a safety dump first (with size check — refuses to proceed if pg_dump silently produced an empty file), then restores. Note `set -euo pipefail` inside the SSH heredocs (without it, a `pg_dump` failure inside a pipeline is masked by a successful `gzip`) and the `OVERWRITE <db>` confirmation phrase (a bare DB name is too easy to typo into):

```bash
#!/usr/bin/env bash
set -euo pipefail
SSH_ALIAS="<your-app>"
CONTAINER="<your-app>-postgres-1"
DB="<your-db>"
REMOTE_DIR="/var/backups/postgresql"
MIN_SAFETY_BYTES=1024

trap 'echo "Interrupted — DB may be inconsistent. Latest safety snapshot is in $SSH_ALIAS:$REMOTE_DIR."' INT TERM

ssh -o ConnectTimeout=5 "$SSH_ALIAS" "docker exec $CONTAINER pg_isready -U postgres -d $DB" >/dev/null \
    || { echo "Postgres not ready"; exit 1; }

mapfile -t BACKUPS < <(ssh "$SSH_ALIAS" "ls -1t $REMOTE_DIR/<your-app>_*.sql.gz 2>/dev/null | xargs -n1 basename")
[[ ${#BACKUPS[@]} -gt 0 ]] || { echo "No backups found"; exit 1; }
for i in "${!BACKUPS[@]}"; do printf "  %2d) %s\n" "$((i+1))" "${BACKUPS[$i]}"; done
read -rp "Pick: " PICK
[[ "$PICK" =~ ^[0-9]+$ ]] && (( PICK >= 1 && PICK <= ${#BACKUPS[@]} )) || { echo "Invalid"; exit 1; }
CHOSEN="${BACKUPS[$((PICK-1))]}"

read -rp "Type 'OVERWRITE $DB' to confirm: " CONFIRM
[[ "$CONFIRM" == "OVERWRITE $DB" ]] || { echo "aborted"; exit 1; }

SAFETY="<your-app>_pre_restore_$(date +%Y-%m-%d_%H%M%S).sql.gz"
ssh "$SSH_ALIAS" "set -euo pipefail; docker exec $CONTAINER pg_dump -U postgres $DB | gzip > $REMOTE_DIR/$SAFETY"
SIZE=$(ssh "$SSH_ALIAS" "stat -c%s $REMOTE_DIR/$SAFETY")
(( SIZE >= MIN_SAFETY_BYTES )) || { echo "Safety snapshot suspiciously small ($SIZE B) — aborting"; exit 1; }

ssh "$SSH_ALIAS" "
    set -euo pipefail
    docker exec $CONTAINER psql -U postgres -d postgres -c \"DROP DATABASE IF EXISTS \\\"$DB\\\" WITH (FORCE);\"
    docker exec $CONTAINER psql -U postgres -d postgres -c \"CREATE DATABASE \\\"$DB\\\";\"
    gunzip -c $REMOTE_DIR/$CHOSEN | docker exec -i $CONTAINER psql -U postgres -d $DB
"
echo "Restored. Safety snapshot at $SSH_ALIAS:$REMOTE_DIR/$SAFETY."
```

**6. Restore drill — quarterly.** A backup you've never restored from is not a backup. At least once a quarter, restore the latest dump into a throwaway local Postgres and verify schema + row counts. Calendar reminder.

## Log archival (optional)

When to use it: you need long-term log retention beyond Docker's `json-file` rotation buffer (default ~150 MB rolling per container, configured in compose).

Pattern: a Hangfire recurring job watches `/app/logs/`, ships files older than N days to R2 once total > threshold, deletes locally.

To make `/app/logs` writable under `USER app`:

- **Bind mount with chowned host dir** (recommended): `volumes: - /var/log/<your-app>:/app/logs` in compose, one-time `mkdir -p /var/log/<your-app> && chown 1654:1654 /var/log/<your-app>` on the VPS. Bind mounts respect host ownership; named volumes overlay the path with root-owned storage which `app` can't write to.
- **No File sink at all** (simpler): rely on Console + Docker rotation. Drop the `/app/logs` volume entirely. Right answer for greenfield deploys.

