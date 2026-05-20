#!/bin/sh
function abspath()
{
  case "${1}" in
    [./]*)
    echo "$(cd ${1%/*}; pwd)/${1##*/}"
    ;;
    *)
    echo "${PWD}/${1}"
    ;;
  esac
}

# Resolve dugite architecture: arm64 | x64
# Args: optional RuntimeIdentifier (osx-arm64 / osx-x64) or shorthand (arm64 / x64)
resolve_git_arch()
{
  local rid="${1:-${SPARKLESHARE_MAC_RID:-}}"

  case "$rid" in
    osx-arm64|arm64|aarch64) echo "arm64"; return 0 ;;
    osx-x64|x64|x86_64|amd64) echo "x64"; return 0 ;;
  esac

  case "$(uname -m)" in
    arm64|aarch64) echo "arm64" ;;
    x86_64) echo "x64" ;;
    *) echo "arm64" ;;
  esac
}

export projectFolder=$(dirname $0)
export projectFolder=$(abspath ${projectFolder})

GIT_ARCH="$(resolve_git_arch "$1")"
export gitArch="$GIT_ARCH"

gitDownload=""
gitSHA256=""

while read -r line; do
  case "$line" in
    ''|\#*) continue ;;
  esac

  set -- $line
  arch="$1"
  url="$2"
  sha="$3"

  if [ "$arch" = "$GIT_ARCH" ]; then
    gitDownload="$url"
    gitSHA256="$sha"
    break
  fi
done < "${projectFolder}/git.download"

# Legacy single-line git.download (arm64 only)
if [ -z "$gitDownload" ]; then
  LINE=$(grep -v '^[[:space:]]*#' "${projectFolder}/git.download" | grep -v '^[[:space:]]*$' | head -1)
  TMP=()
  for val in $LINE ; do
    TMP+=("$val")
  done
  gitDownload="${TMP[0]}"
  gitSHA256="${TMP[1]}"
fi

if [ -z "$gitDownload" ]; then
  echo "No git bundle entry for architecture ${GIT_ARCH} in git.download" >&2
  exit 1
fi

export gitDownload
export gitName=${gitDownload##*/}
export gitSHA256

set -e

echo "Using dugite bundle: ${GIT_ARCH} (${gitName})"

if [[ ! -f ${projectFolder}/${gitName} ]];
then
  curl --silent --location ${gitDownload} > ${projectFolder}/${gitName}
  test -e ${projectFolder}/${gitName} || { echo "Failed to download git"; exit 1; }

  printf "${gitSHA256}  ${projectFolder}/${gitName}" | shasum --check --algorithm 256

fi

rm -f ${projectFolder}/git.tar.gz
ln -sf ${projectFolder}/$gitName ${projectFolder}/git.tar.gz
