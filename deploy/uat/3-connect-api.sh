#!/bin/bash
# Step 3: connect the HRMS API on Core-UAT-API to the RDS database, then create the first administrator.
# Run ON THE API SERVER as ubuntu, after steps 1 and 2:
#     bash 3-connect-api.sh
# It asks for the hrms_api_uat password (not echoed, not stored in shell history) and writes it only to
# /etc/hrms/hrms.env (root, 600).
set -euo pipefail

DB_NAME="HR"
DB_USER="hrms_api_uat"
ENV_FILE="/etc/hrms/hrms.env"
# The RDS endpoint is not in the repository: pass it as RDS_ENDPOINT=... or type it (see uat.local.env on your PC).
DB_HOST="${RDS_ENDPOINT:-}"
[ -n "$DB_HOST" ] || read -r -p "RDS endpoint (host name): " DB_HOST
# The health check must send a host the API allows: the first AllowedHosts entry.
PUBLIC_HOST="$(sudo grep -m1 '^AllowedHosts=' "$ENV_FILE" | cut -d= -f2- | tr -d "'\"" | cut -d';' -f1)"

echo "== 1/4  Trust the Amazon RDS certificates =="
if [ ! -f /usr/local/share/ca-certificates/rds/.installed ]; then
  tmp=$(mktemp -d)
  curl -fsSL https://truststore.pki.rds.amazonaws.com/global/global-bundle.pem -o "$tmp/global-bundle.pem"
  sudo install -d -m 755 /usr/local/share/ca-certificates/rds
  # update-ca-certificates wants one certificate per .crt file
  awk -v dir="$tmp" '/BEGIN CERTIFICATE/{n++} {print > (dir "/rds-" n ".crt")}' "$tmp/global-bundle.pem"
  sudo cp "$tmp"/rds-*.crt /usr/local/share/ca-certificates/rds/
  sudo touch /usr/local/share/ca-certificates/rds/.installed
  sudo update-ca-certificates >/dev/null
  rm -rf "$tmp"
  echo "RDS CA certificates installed."
else
  echo "Already installed."
fi

echo "== 2/4  Connection string =="
read -r -s -p "Password for $DB_USER: " DB_PASS; echo
case "$DB_PASS" in
  *";"*|*"'"*|*'"'*|*" "*|"") echo "The password must not be empty or contain ; ' \" or spaces. Change it in SQL Server first."; exit 1 ;;
esac
CONN="Server=$DB_HOST,1433;Database=$DB_NAME;User Id=$DB_USER;Password=$DB_PASS;Encrypt=True;TrustServerCertificate=False"
sudo cp -p "$ENV_FILE" "$ENV_FILE.bak-$(date +%Y%m%d%H%M%S)"
# Replace the line without putting the password on a command line (it goes through stdin).
# Single quotes: systemd strips them, and "source" in bash (step 4) then reads ; and $ literally.
{ sudo grep -v '^ConnectionStrings__Hrms=' "$ENV_FILE"; printf "ConnectionStrings__Hrms='%s'\n" "$CONN"; } | sudo tee "$ENV_FILE.new" >/dev/null
sudo chown root:root "$ENV_FILE.new"; sudo chmod 600 "$ENV_FILE.new"; sudo mv "$ENV_FILE.new" "$ENV_FILE"
unset DB_PASS CONN
echo "Connection string saved in $ENV_FILE."

echo "== 3/4  Restart the API and check the database =="
sudo systemctl restart hrms-api
for i in $(seq 1 20); do
  code=$(curl -sk -o /dev/null -w '%{http_code}' -H "Host: $PUBLIC_HOST" https://localhost/health/ready || true)
  [ "$code" = "200" ] && break
  sleep 3
done
if [ "$code" = "200" ]; then
  echo "Database reachable: /health/ready = 200 Healthy"
else
  echo "Database NOT reachable (/health/ready = $code). Recent API log:"
  sudo journalctl -u hrms-api -n 30 --no-pager | grep -iE "fail|error|exception|login" | tail -10
  exit 1
fi

echo "== 4/4  First administrator =="
read -r -p "Create the first HR Administrator now? (y/N) " ok
if [ "${ok,,}" = "y" ]; then
  read -r -p "Administrator email: " ADMIN_EMAIL
  read -r -p "Display name (optional): " ADMIN_NAME
  args=(bootstrap-admin --email "$ADMIN_EMAIL")
  [ -n "$ADMIN_NAME" ] && args+=(--name "$ADMIN_NAME")
  # Runs as the hrms service user with the service's environment; it prints the target database and asks for "yes",
  # then for the password twice.
  sudo bash -c 'set -a; . /etc/hrms/hrms.env; set +a; cd /opt/hrms/api; exec runuser -u hrms -- dotnet Apex.Hrms.Api.dll "$@"' _ "${args[@]}"
fi

echo "Done. Swagger: https://$PUBLIC_HOST/swagger   Health: https://$PUBLIC_HOST/health/ready"
