#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

export MD_APPLE_SDK_ROOT="${MD_APPLE_SDK_ROOT:-/Applications/Xcode.app}"

echo "Using Xcode: $MD_APPLE_SDK_ROOT"
dotnet build SparkleShare/Mac/SparkleShare.Mac.csproj -c Debug "$@"
