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

echo "Stopping services..."
systemctl stop multicountryfx-api.service || true
systemctl stop multicountryfx-worker.service || true

echo "Pulling latest changes..."
git -C "${ROOT_DIR}" pull

echo "Publishing projects..."
rm -rf "${PUBLISH_DIR}"
mkdir -p "${PUBLISH_DIR}"

dotnet publish "${ROOT_DIR}/MultiCountryFxImporter.Api/MultiCountryFxImporter.Api.csproj" -c Release -o "${PUBLISH_DIR}/MultiCountryFxImporter.Api"
dotnet publish "${ROOT_DIR}/MultiCountryFxImporter.Worker/MultiCountryFxImporter.Worker.csproj" -c Release -o "${PUBLISH_DIR}/MultiCountryFxImporter.Worker"

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
