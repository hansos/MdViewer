#!/usr/bin/env bash
#
# Installs the icon theme and desktop entry for the current user.
# Packaged builds (AppImage, Flatpak, .deb) copy the same tree into their
# own prefix instead of calling this.
#
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
prefix="${1:-$HOME/.local}"

echo "Installing to $prefix"

# Icons. The hicolor theme is the fallback every desktop reads, so shipping
# there means GNOME, KDE, XFCE and the rest all find it without special cases.
for dir in "$here"/hicolor/*/apps; do
    size_dir="$(basename "$(dirname "$dir")")"
    target="$prefix/share/icons/hicolor/$size_dir/apps"
    mkdir -p "$target"
    cp "$dir"/* "$target/"
done

# Desktop entry
mkdir -p "$prefix/share/applications"
cp "$here/mdviewer.desktop" "$prefix/share/applications/"

# Refresh the caches. Both are best-effort: a missing tool is not a failure,
# the icon simply appears after the next login instead of immediately.
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t "$prefix/share/icons/hicolor" 2>/dev/null || true
fi

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$prefix/share/applications" 2>/dev/null || true
fi

echo "Done. If MdViewer does not appear yet, log out and back in."
