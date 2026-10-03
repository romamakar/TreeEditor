#!/usr/bin/env bash
# Start both projects in background (Unix-like systems)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
API_DIR="$ROOT/TreeEditor.Api"
WEB_DIR="$ROOT/TreeEditor.Web"

API_URL="https://localhost:5001"
WEB_URL="https://localhost:5003"

LOG_DIR="$ROOT/logs"
mkdir -p "$LOG_DIR"

echo "Starting TreeEditor.Api on $API_URL..."
nohup dotnet run --project "$API_DIR" --no-launch-profile --urls "$API_URL" > "$LOG_DIR/api.log" 2>&1 &
API_PID=$!
echo "API pid=$API_PID (logs: $LOG_DIR/api.log)"

sleep 1

echo "Starting TreeEditor.Web on $WEB_URL..."
nohup dotnet run --project "$WEB_DIR" --no-launch-profile --urls "$WEB_URL" -- ApiBase="$API_URL" > "$LOG_DIR/web.log" 2>&1 &
WEB_PID=$!
echo "Web pid=$WEB_PID (logs: $LOG_DIR/web.log)"

echo "Launched both processes. Tail logs with: tail -f $LOG_DIR/api.log $LOG_DIR/web.log"

echo "Note: these background processes will continue running after this script exits. Use 'kill $API_PID $WEB_PID' to stop them."