#!/bin/bash
# Creates the first HR Administrator account in the UAT database. Run on the API server:
#   bash 4-create-admin.sh
# It prints the target database and asks for "yes", then asks for the password twice (never on the command line).
# Note: Ubuntu 26.04 ships sudo-rs, which ignores "sudo -E"; runuser keeps the environment loaded from hrms.env.
set -euo pipefail
read -r -p "Administrator email: " ADMIN_EMAIL
read -r -p "Display name (optional): " ADMIN_NAME
args=(bootstrap-admin --email "$ADMIN_EMAIL")
[ -n "$ADMIN_NAME" ] && args+=(--name "$ADMIN_NAME")
sudo bash -c 'set -a; . /etc/hrms/hrms.env; set +a; cd /opt/hrms/api; exec runuser -u hrms -- dotnet Apex.Hrms.Api.dll "$@"' _ "${args[@]}"
