#!/usr/bin/env bash
while tasklist //FI "PID eq 34972" 2>/dev/null | grep -q 34972; do sleep 20; done
exec .calisma/T116/ab-kos.sh
