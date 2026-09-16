#!/usr/bin/env bash
#
# install.sh — register (or remove) Overmind user autostart on Linux/macOS.
#
# Usage: install.sh [--unregister]
#
# Run from inside the unpacked Overmind folder. Registers an XDG autostart entry (Linux) or a
# per-user LaunchAgent (macOS). Writes nothing outside the autostart directory of the user.

set -euo pipefail

app_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
run_script=$app_dir/run.sh

[[ ${1:-} == "" || ${1:-} == "--unregister" ]] || { echo "usage: install.sh [--unregister]" >&2; exit 2; }
unregister=${1:-}

[[ -f $run_script ]] || { echo "install.sh: $run_script not found; run this from the unpacked folder" >&2; exit 1; }

desktop_escape() {
    printf '%s' "$1" | sed 's/ /\\ /g'
}

xml_escape() {
    sed 's/&/\&amp;/g; s/</\&lt;/g; s/>/\&gt;/g' <<<"$1"
}

print_hint() {
    echo "Overmind starts at login; UI: http://127.0.0.1:4761"
    echo "The live port is written to ~/.overmind/host.port once the host is up."
}

case $(uname -s) in
    Linux)
        entry_dir=$HOME/.config/autostart
        entry=$entry_dir/overmind.desktop
        if [[ $unregister == "--unregister" ]]; then
            rm -f "$entry"
            echo "Removed $entry"
            exit 0
        fi
        mkdir -p "$entry_dir"
        cat >"$entry" <<EOF
[Desktop Entry]
Type=Application
Name=Overmind
Comment=Overmind host with the bundled crew kit
Exec=$(desktop_escape "$run_script")
Terminal=false
X-GNOME-Autostart-enabled=true
EOF
        echo "Registered $entry"
        ;;
    Darwin)
        agents_dir=$HOME/Library/LaunchAgents
        plist=$agents_dir/app.overmind.host.plist
        if [[ $unregister == "--unregister" ]]; then
            launchctl bootout "gui/$(id -u)/app.overmind.host" 2>/dev/null || true
            rm -f "$plist"
            echo "Removed $plist"
            exit 0
        fi
        mkdir -p "$agents_dir"
        cat >"$plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>app.overmind.host</string>
    <key>ProgramArguments</key>
    <array>
        <string>$(xml_escape "$run_script")</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>
EOF
        echo "Registered $plist"
        echo "Load it now with: launchctl bootstrap gui/$(id -u) $plist"
        ;;
    *)
        echo "install.sh: unsupported OS $(uname -s)" >&2
        exit 1
        ;;
esac

print_hint
