#!/bin/bash
set -e

DP_KEYS_DIR="${DP_KEYS_DIR:-/app/dpkeys}"
DP_CERT_PATH="${DP_CERT_PATH:-$DP_KEYS_DIR/dp-cert.pfx}"

mkdir -p "$DP_KEYS_DIR"

if [ ! -f "$DP_CERT_PATH" ]; then
    echo "Generating Data Protection certificate..."
    openssl req -x509 -newkey rsa:4096 -keyout /tmp/dp-key.pem -out /tmp/dp-cert.pem \
        -days 3650 -nodes -subj "/CN=Budgie-DataProtection"
    openssl pkcs12 -export -out "$DP_CERT_PATH" \
        -inkey /tmp/dp-key.pem -in /tmp/dp-cert.pem -passout pass:
    rm -f /tmp/dp-key.pem /tmp/dp-cert.pem
    echo "Certificate generated at $DP_CERT_PATH"
fi

exec dotnet Budgie.Api.dll
