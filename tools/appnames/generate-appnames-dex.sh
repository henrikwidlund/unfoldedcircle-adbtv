#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SOURCE_FILE="$ROOT_DIR/tools/appnames/AppNames.java"
OUTPUT_FILE="${1:-$ROOT_DIR/tools/appnames/appnames.dex}"
OUTPUT_DIR="$(dirname "$OUTPUT_FILE")"
BUILD_DIR="$(mktemp -d "${TMPDIR:-/tmp}/uc-adbtv-appnames.XXXXXX")"
trap 'rm -rf "$BUILD_DIR"' EXIT

if ! command -v javac >/dev/null 2>&1; then
    echo "javac is required to generate the Android app names helper." >&2
    exit 1
fi

D8_BIN="${APPNAMES_D8:-}"
if [[ -z "$D8_BIN" ]] && command -v d8 >/dev/null 2>&1; then
    D8_BIN="$(command -v d8)"
fi

if [[ -z "$D8_BIN" ]]; then
    SDK_ROOT="${ANDROID_SDK_ROOT:-${ANDROID_HOME:-}}"
    if [[ -n "$SDK_ROOT" && -d "$SDK_ROOT/build-tools" ]]; then
        D8_BIN="$(find "$SDK_ROOT/build-tools" -type f -name d8 2>/dev/null | sort | tail -n 1)"
    fi
fi

if [[ -z "$D8_BIN" || ! -x "$D8_BIN" ]]; then
    echo "d8 was not found. Install Android Build Tools or set APPNAMES_D8=/path/to/d8." >&2
    exit 1
fi

echo "Using javac: $(command -v javac)"
echo "Using d8: $D8_BIN"
"$D8_BIN" --version || true

mkdir -p "$BUILD_DIR/classes" "$BUILD_DIR/dex" "$OUTPUT_DIR"

javac --release 8 \
    -d "$BUILD_DIR/classes" \
    "$SOURCE_FILE"

"$D8_BIN" \
    --min-api 24 \
    --output "$BUILD_DIR/dex" \
    "$BUILD_DIR/classes/uc/adbtv/AppNames.class"

cp "$BUILD_DIR/dex/classes.dex" "$OUTPUT_FILE"

echo "Generated $OUTPUT_FILE ($(wc -c < "$OUTPUT_FILE" | tr -d ' ') bytes)"
