#!/usr/bin/env bash
set -euo pipefail

show_help() {
  cat <<'EOF'
Build and deploy all services (API + Worker).

Usage:
  ./build_all.sh
  ./build_all.sh -h

Useful commands:
  systemctl status multicountryfx-api.service
  systemctl status multicountryfx-worker.service
  journalctl -u multicountryfx-api.service -f
  journalctl -u multicountryfx-worker.service -f
  systemctl restart multicountryfx-api.service
  systemctl restart multicountryfx-worker.service
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  show_help
  exit 0
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="${ROOT_DIR}/publish"
SETUP_SCRIPT="${ROOT_DIR}/scripts/setup-services.sh"
WORKER_PUBLISH_DIR="${PUBLISH_DIR}/MultiCountryFxImporter.Worker"
CONFIG_BACKUP_DIR="${ROOT_DIR}/.deploy-config-backup/worker"
LOGS_BACKUP_DIR="${ROOT_DIR}/.deploy-logs-backup/worker"
WORKER_LOGS_DIR="${WORKER_PUBLISH_DIR}/logs"
WORKER_SCHEDULE_FILE="${ROOT_DIR}/worker-schedule.json"
WORKER_SCHEDULE_PUBLISH_FILE="${PUBLISH_DIR}/worker-schedule.json"
WORKER_SCHEDULE_BACKUP="${CONFIG_BACKUP_DIR}/worker-schedule.json"
WORKER_CONFIG_FILES=(
  "appsettings.json"
  "appsettings.Production.json"
  "appsettings.Development.json"
)

backup_worker_config() {
  if [[ ! -d "${WORKER_PUBLISH_DIR}" ]]; then
    return
  fi
  mkdir -p "${CONFIG_BACKUP_DIR}"
  for file in "${WORKER_CONFIG_FILES[@]}"; do
    if [[ -f "${WORKER_PUBLISH_DIR}/${file}" ]]; then
      cp -a "${WORKER_PUBLISH_DIR}/${file}" "${CONFIG_BACKUP_DIR}/${file}"
    fi
  done
}

restore_worker_config() {
  if [[ ! -d "${CONFIG_BACKUP_DIR}" ]]; then
    return
  fi
  mkdir -p "${WORKER_PUBLISH_DIR}"
  for file in "${WORKER_CONFIG_FILES[@]}"; do
    if [[ -f "${CONFIG_BACKUP_DIR}/${file}" ]]; then
      cp -a "${CONFIG_BACKUP_DIR}/${file}" "${WORKER_PUBLISH_DIR}/${file}"
    fi
  done
}

backup_worker_schedule() {
  local source=""
  if [[ -f "${WORKER_SCHEDULE_PUBLISH_FILE}" ]]; then
    source="${WORKER_SCHEDULE_PUBLISH_FILE}"
  elif [[ -f "${WORKER_SCHEDULE_FILE}" ]]; then
    source="${WORKER_SCHEDULE_FILE}"
  fi
  if [[ -n "${source}" ]]; then
    mkdir -p "${CONFIG_BACKUP_DIR}"
    cp -a "${source}" "${WORKER_SCHEDULE_BACKUP}"
  fi
}

restore_worker_schedule() {
  if [[ -f "${WORKER_SCHEDULE_BACKUP}" ]]; then
    mkdir -p "${PUBLISH_DIR}"
    cp -a "${WORKER_SCHEDULE_BACKUP}" "${WORKER_SCHEDULE_PUBLISH_FILE}"
    cp -a "${WORKER_SCHEDULE_BACKUP}" "${WORKER_SCHEDULE_FILE}"
  fi
}

backup_worker_logs() {
  if [[ ! -d "${WORKER_LOGS_DIR}" ]]; then
    return
  fi
  mkdir -p "${LOGS_BACKUP_DIR}"
  cp -a "${WORKER_LOGS_DIR}/." "${LOGS_BACKUP_DIR}/"
}

restore_worker_logs() {
  if [[ ! -d "${LOGS_BACKUP_DIR}" ]]; then
    return
  fi
  mkdir -p "${WORKER_LOGS_DIR}"
  cp -a "${LOGS_BACKUP_DIR}/." "${WORKER_LOGS_DIR}/"
}

echo "Stopping services..."
systemctl stop multicountryfx-api.service || true
systemctl stop multicountryfx-worker.service || true

echo "Backing up worker schedule..."
backup_worker_schedule

echo "Pulling latest changes..."
git -C "${ROOT_DIR}" pull
echo "Restoring worker schedule..."
restore_worker_schedule

echo "Publishing projects..."
echo "Backing up worker configuration..."
backup_worker_config
echo "Backing up worker logs..."
backup_worker_logs

rm -rf "${PUBLISH_DIR}"
mkdir -p "${PUBLISH_DIR}"

dotnet publish "${ROOT_DIR}/MultiCountryFxImporter.Api/MultiCountryFxImporter.Api.csproj" -c Release -o "${PUBLISH_DIR}/MultiCountryFxImporter.Api"
dotnet publish "${ROOT_DIR}/MultiCountryFxImporter.Worker/MultiCountryFxImporter.Worker.csproj" -c Release -o "${PUBLISH_DIR}/MultiCountryFxImporter.Worker"

echo "Restoring worker configuration..."
restore_worker_config
echo "Restoring worker logs..."
restore_worker_logs

echo "Adjusting ownership for publish directories..."
chown -R www-data:www-data "${PUBLISH_DIR}"

echo "Refreshing systemd services..."
bash "${SETUP_SCRIPT}" "${ROOT_DIR}"

echo "Starting services..."
systemctl start multicountryfx-api.service
systemctl start multicountryfx-worker.service

echo "Service status:"
systemctl status --no-pager multicountryfx-api.service || true
systemctl status --no-pager multicountryfx-worker.service || true

echo ""
show_help
