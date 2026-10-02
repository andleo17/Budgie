#!/bin/bash
set -e

UPLOADS_DIR=/app/wwwroot/uploads
SEED_DIR=/seed/uploads
SEED_MARKER="$UPLOADS_DIR/.seeded"

# Pre-volume images shipped person photos baked into the image layer at
# wwwroot/uploads. On the first start with the volume mounted, copy anything the
# image carried into the volume so existing photos survive. Runs at most once per
# volume (the marker lives in the volume), and never overwrites existing files.
if [ -d "$SEED_DIR" ] && [ ! -f "$SEED_MARKER" ]; then
    mkdir -p "$UPLOADS_DIR"
    echo "Seeding $UPLOADS_DIR from image..."
    while IFS= read -r relative; do
        if [ ! -e "$UPLOADS_DIR/$relative" ]; then
            mkdir -p "$(dirname "$UPLOADS_DIR/$relative")"
            cp "$SEED_DIR/$relative" "$UPLOADS_DIR/$relative"
        fi
    done <<< "$(cd "$SEED_DIR" && find . -type f)"
    touch "$SEED_MARKER"
fi

exec dotnet Budgie.Api.dll
