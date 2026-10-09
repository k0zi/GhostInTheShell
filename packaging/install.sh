#!/usr/bin/env bash
# Builds the system package for this distribution and installs it with the native package manager
# (apt on Debian/Ubuntu, dnf on Fedora/RHEL, zypper on openSUSE/SUSE).
#   /usr/lib/ghostintheshell/   application
#   /usr/bin/ghostintheshell    launcher
# Usage: packaging/install.sh [--no-build] | --uninstall
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/packages"
SUDO=""
[[ $EUID -eq 0 ]] || SUDO=sudo

if command -v apt-get >/dev/null; then
    format=deb
    install_cmd=(apt-get install -y --reinstall)
    remove_cmd=(apt-get remove -y ghostintheshell)
elif command -v dnf >/dev/null; then
    format=rpm
    install_cmd=(dnf install -y)
    remove_cmd=(dnf remove -y ghostintheshell)
elif command -v zypper >/dev/null; then
    format=rpm
    install_cmd=(zypper --non-interactive install --allow-unsigned-rpm --force)
    remove_cmd=(zypper --non-interactive remove ghostintheshell)
else
    echo "Unsupported distribution: apt-get, dnf or zypper is required." >&2
    exit 1
fi

if [[ "${1:-}" == "--uninstall" ]]; then
    $SUDO "${remove_cmd[@]}"
    echo "Ghost in the Shell removed."
    exit 0
fi

[[ "${1:-}" == "--no-build" ]] || "$ROOT/packaging/build-packages.sh" "$format"

package="$(ls -t "$OUT"/ghostintheshell*."$format" 2>/dev/null | head -n 1)"
if [[ -z "$package" ]]; then
    echo "No .$format package in $OUT; run packaging/build-packages.sh first." >&2
    exit 1
fi

# apt needs an absolute (or ./) path to treat the argument as a file; $package is absolute.
$SUDO "${install_cmd[@]}" "$package"

echo "Installed: $(basename "$package")"
