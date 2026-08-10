#!/usr/bin/env bash
set -Eeuo pipefail

operation=""
target_environment="isolated"
storage_path="${BACKUP_STORAGE_PATH:-/var/lib/his-hope/backups}"
vault_addr="${VAULT_ADDR:?VAULT_ADDR is required}"
vault_token="${VAULT_TOKEN:?VAULT_TOKEN is required}"
vault_key="${VAULT_TRANSIT_KEY:-his-hope-backup-encryption}"
databases="${DATABASE_CONTINUITY_DATABASES:-identitydb,patientdb,appointmentdb,clinicaldb,labdb,billingdb,pharmacydb}"
pitr_enabled="${PITR_ENABLED:-false}"
wal_archive_path="${PITR_WAL_ARCHIVE_PATH:-/var/lib/his-hope/wal-archive}"
pitr_base_backup_path="${PITR_BASE_BACKUP_PATH:-$storage_path/pitr-base}"
pitr_base_backup_interval_hours="${PITR_BASE_BACKUP_INTERVAL_HOURS:-24}"
retention_days="${RETENTION_DAYS:-30}"
restore_drill_pg_host="${RESTORE_DRILL_PGHOST:-$PGHOST}"
restore_drill_pg_port="${RESTORE_DRILL_PGPORT:-$PGPORT}"
restore_drill_pg_user="${RESTORE_DRILL_PGUSER:-$PGUSER}"
restore_drill_pg_password="${RESTORE_DRILL_PGPASSWORD:-$PGPASSWORD}"
restore_point=""

while (($#)); do
  case "$1" in
    --operation) operation="$2"; shift 2 ;;
    --target-environment) target_environment="$2"; shift 2 ;;
    --restore-point) restore_point="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 64 ;;
  esac
done

[[ "$target_environment" == "isolated" || "$operation" == "backup" ]] || { echo "Restore is restricted to isolated target." >&2; exit 65; }
mkdir -p "$storage_path"

vault_encrypt() {
  local plaintext="$1"
  curl -fsS -H "X-Vault-Token: $vault_token" -H 'Content-Type: application/json' --data "{\"plaintext\":\"$plaintext\"}" "$vault_addr/v1/transit/encrypt/$vault_key" | jq -er '.data.ciphertext'
}

vault_decrypt() {
  local ciphertext="$1"
  curl -fsS -H "X-Vault-Token: $vault_token" -H 'Content-Type: application/json' --data "{\"ciphertext\":\"$ciphertext\"}" "$vault_addr/v1/transit/decrypt/$vault_key" | jq -er '.data.plaintext'
}

encrypt_file() {
  local input="$1" output="$2" tmpdir part chunk
  : > "$output"
  tmpdir="$(mktemp -d)"
  split -b 32768 "$input" "$tmpdir/chunk-"
  for part in "$tmpdir"/chunk-*; do
    chunk="$(base64 -w0 "$part")"
    vault_encrypt "$chunk" >> "$output"
    printf '\n' >> "$output"
  done
  rm -rf "$tmpdir"
}

decrypt_file() {
  local input="$1" output="$2" plaintext
  : > "$output"
  while IFS= read -r ciphertext; do
    [[ -z "$ciphertext" ]] && continue
    plaintext="$(vault_decrypt "$ciphertext")"
    printf '%s' "$plaintext" | base64 -d >> "$output"
  done < "$input"
}

backup_database() {
  local database="$1" stamp file encrypted checksum
  stamp="$(date -u +%Y%m%dT%H%M%SZ)"
  file="$storage_path/${database}-${stamp}.dump"
  encrypted="$file.vault"
  pg_dump --format=custom --no-owner --no-acl --file="$file" --dbname="$database"
  encrypt_file "$file" "$encrypted"
  checksum="$(sha256sum "$encrypted" | awk '{print $1}')"
  jq -n --arg db "$database" --arg file "$encrypted" --arg sha "$checksum" --arg created "$stamp" '{database:$db,file:$file,sha256:$sha,createdAtUtc:$created,encryption:"vault-transit"}' > "$encrypted.manifest.json"
  rm -f "$file"
  echo "Backup created: $encrypted"
}

restore_database() {
  local database="$1" encrypted temp restore_db
  encrypted="$(find "$storage_path" -maxdepth 1 -type f -name "${database}-*.dump.vault" | sort | tail -n1)"
  [[ -n "$encrypted" ]] || { echo "No encrypted backup found for $database" >&2; return 1; }
  temp="$(mktemp --suffix=.dump)"
  restore_db="his_hope_restore_drill_${database}_$(date -u +%Y%m%d%H%M%S | tr -cd '[:alnum:]_')"
  decrypt_file "$encrypted" "$temp"
  PGHOST="$restore_drill_pg_host" PGPORT="$restore_drill_pg_port" PGUSER="$restore_drill_pg_user" PGPASSWORD="$restore_drill_pg_password" \
    createdb --maintenance-db=postgres "$restore_db"
  PGHOST="$restore_drill_pg_host" PGPORT="$restore_drill_pg_port" PGUSER="$restore_drill_pg_user" PGPASSWORD="$restore_drill_pg_password" \
    pg_restore --clean --if-exists --no-owner --no-acl --dbname="$restore_db" "$temp"
  PGHOST="$restore_drill_pg_host" PGPORT="$restore_drill_pg_port" PGUSER="$restore_drill_pg_user" PGPASSWORD="$restore_drill_pg_password" \
    psql --dbname="$restore_db" --command='SELECT 1 AS restore_validation;' >/dev/null
  PGHOST="$restore_drill_pg_host" PGPORT="$restore_drill_pg_port" PGUSER="$restore_drill_pg_user" PGPASSWORD="$restore_drill_pg_password" \
    dropdb --maintenance-db=postgres "$restore_db"
  rm -f "$temp"
  echo "Restore drill completed: $database"
}

pitr_replay_drill() {
  [[ "$pitr_enabled" == "true" ]] || return 0
  local latest drill_dir port postmaster_opts marker marker_lsn
  latest="$(find "$pitr_base_backup_path" -mindepth 1 -maxdepth 1 -type d -name 'base-*' | sort | tail -n1 || true)"
  [[ -n "$latest" && -s "$latest/backup_manifest" ]] || { echo "No verified PITR base backup available." >&2; return 1; }
  /usr/lib/postgresql/16/bin/pg_verifybackup "$latest"
  marker="his_hope_pitr_drill_$(date -u +%Y%m%d%H%M%S)"
  marker_lsn="$(psql -Atc "select pg_create_restore_point('$marker');")"
  [[ -n "$marker_lsn" ]] || { echo "Unable to create PITR restore marker." >&2; return 1; }
  psql -Atc 'select pg_switch_wal();' >/dev/null
  sleep 2
  drill_dir="$(mktemp -d /tmp/his-hope-pitr-drill.XXXXXX)"
  port="${PITR_DRILL_PORT:-55432}"
  cp -a "$latest"/. "$drill_dir/"
  printf "lc_messages = 'C'\nlc_monetary = 'C'\nlc_numeric = 'C'\nlc_time = 'C'\nrestore_command = 'cp %s/%%f %%p'\nrecovery_target_name = '%s'\nrecovery_target_action = 'shutdown'\n" \
    "$wal_archive_path" "$marker" > "$drill_dir/postgresql.auto.conf"
  touch "$drill_dir/recovery.signal"
  chown -R postgres:postgres "$drill_dir"
  postmaster_opts="-p $port -c listen_addresses='' -c unix_socket_directories=/tmp"
  runuser -u postgres -- /usr/lib/postgresql/16/bin/pg_ctl -D "$drill_dir" -o "$postmaster_opts" -w start
  if ! runuser -u postgres -- psql -h /tmp -p "$port" -d postgres -Atc 'select 1;' >/dev/null; then
    runuser -u postgres -- /usr/lib/postgresql/16/bin/pg_ctl -D "$drill_dir" -m immediate stop || true
    rm -rf "$drill_dir"
    echo "PITR replay drill did not become ready." >&2
    return 1
  fi
  runuser -u postgres -- /usr/lib/postgresql/16/bin/pg_ctl -D "$drill_dir" -m fast stop
  rm -rf "$drill_dir"
  echo "PITR replay drill completed at marker $marker (LSN $marker_lsn)"
}

pitr_base_backup() {
  [[ "$pitr_enabled" == "true" ]] || return 0
  mkdir -p "$pitr_base_backup_path"
  local latest age_hours stamp destination
  latest="$(find "$pitr_base_backup_path" -mindepth 1 -maxdepth 1 -type d -name 'base-*' | sort | tail -n1 || true)"
  age_hours=999999
  if [[ -n "$latest" && -s "$latest/backup_manifest" ]]; then
    /usr/lib/postgresql/16/bin/pg_verifybackup "$latest"
    age_hours=$(( ( $(date +%s) - $(stat -c %Y "$latest") ) / 3600 ))
  elif [[ -n "$latest" ]]; then
    rm -rf "$latest"
    latest=""
  fi
  if (( age_hours < pitr_base_backup_interval_hours )); then
    echo "PITR base backup reused: $latest"
    return 0
  fi
  stamp="$(date -u +%Y%m%dT%H%M%SZ)"
  destination="$pitr_base_backup_path/base-$stamp"
  pg_basebackup --format=plain --checkpoint=fast --wal-method=stream --progress --pgdata="$destination"
  [[ -s "$destination/backup_manifest" ]] || { echo "PITR base backup manifest is missing." >&2; exit 1; }
  /usr/lib/postgresql/16/bin/pg_verifybackup "$destination"
  echo "PITR base backup created: $destination"
}

cleanup_pitr_artifacts() {
  [[ "$pitr_enabled" == "true" ]] || return 0
  find "$wal_archive_path" -type f -mtime +"$retention_days" -delete 2>/dev/null || true
  find "$pitr_base_backup_path" -mindepth 1 -maxdepth 1 -type d -name 'base-*' -mtime +"$retention_days" -exec rm -rf {} + 2>/dev/null || true
}

case "$operation" in
  backup) pitr_base_backup; IFS=',' read -ra database_list <<< "$databases"; for database in "${database_list[@]}"; do backup_database "$database"; done; cleanup_pitr_artifacts ;;
  restore-drill) pitr_replay_drill; IFS=',' read -ra database_list <<< "$databases"; for database in "${database_list[@]}"; do restore_database "$database"; done ;;
  *) echo "Unsupported operation: $operation" >&2; exit 64 ;;
esac
