#!/usr/bin/env bash
set -euo pipefail

# Ubuntu 24.04 systemd service setup for API and Worker.
# Usage:
#   sudo ./setup-services.sh /opt/MultiCountryFxImporter

APP_DIR="${1:-$(cd "$(dirname "$0")/.." && pwd)}"
RUN_AS_USER="www-data"

API_DLL="${APP_DIR}/publish/MultiCountryFxImporter.Api/MultiCountryFxImporter.Api.dll"
WORKER_DLL="${APP_DIR}/publish/MultiCountryFxImporter.Worker/MultiCountryFxImporter.Worker.dll"

if [[ ! -f "${API_DLL}" || ! -f "${WORKER_DLL}" ]]; then
  echo "Publish output not found."
  echo "Expected:"
  echo "  ${API_DLL}"
  echo "  ${WORKER_DLL}"
  echo "Run:"
  echo "  dotnet publish MultiCountryFxImporter.Api/MultiCountryFxImporter.Api.csproj -c Release -o ${APP_DIR}/publish/MultiCountryFxImporter.Api"
  echo "  dotnet publish MultiCountryFxImporter.Worker/MultiCountryFxImporter.Worker.csproj -c Release -o ${APP_DIR}/publish/MultiCountryFxImporter.Worker"
  exit 1
fi

echo "Adjusting ownership for publish directories..."
chown -R "${RUN_AS_USER}:${RUN_AS_USER}" "${APP_DIR}/publish"

cat > /etc/systemd/system/multicountryfx-api.service <<EOF
[Unit]
Description=MultiCountryFxImporter API
After=network.target

[Service]
WorkingDirectory=${APP_DIR}/publish/MultiCountryFxImporter.Api
ExecStart=/usr/bin/dotnet ${API_DLL}
Restart=always
RestartSec=5
User=${RUN_AS_USER}
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5999

[Install]
WantedBy=multi-user.target
EOF

cat > /etc/systemd/system/multicountryfx-worker.service <<EOF
[Unit]
Description=MultiCountryFxImporter Worker
After=network.target

[Service]
WorkingDirectory=${APP_DIR}/publish/MultiCountryFxImporter.Worker
ExecStart=/usr/bin/dotnet ${WORKER_DLL}
Restart=always
RestartSec=10
User=${RUN_AS_USER}
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable multicountryfx-api.service
systemctl enable multicountryfx-worker.service

systemctl status --no-pager multicountryfx-api.service || true
systemctl status --no-pager multicountryfx-worker.service || true
