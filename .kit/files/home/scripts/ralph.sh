#!/bin/bash
# Ralph Wiggum - Long-running AI agent loop
# Usage: ./ralph.sh [--tool claude] [max_iterations]

set -e

# Parse arguments
TOOL="claude"
MAX_ITERATIONS=10

while [[ $# -gt 0 ]]; do
  case $1 in
    --tool)
      TOOL="$2"
      shift 2
      ;;
    --tool=*)
      TOOL="${1#*=}"
      shift
      ;;
    *)
      # Assume it's max_iterations if it's a number
      if [[ "$1" =~ ^[0-9]+$ ]]; then
        MAX_ITERATIONS="$1"
      fi
      shift
      ;;
  esac
done

# Validate tool choice
if [[ "$TOOL" != "claude" ]]; then
  echo "Error: Invalid tool '$TOOL'. Must be 'claude'."
  exit 1
fi
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ "$TOOL" == "claude" ]]; then
CLAUDE_CREDENTIALS="${HOME}/.claude/.credentials.json"
  if [ -f "${CLAUDE_CREDENTIALS}" ]; then
      echo "Claude already logged in, skipping /login."
  else
      echo "Logging into Claude..."
      claude /login
  fi
fi

echo "Starting Ralph - Tool: $TOOL - Max iterations: $MAX_ITERATIONS"

PROMPT="/execute-iteration"

for i in $(seq 1 $MAX_ITERATIONS); do
  echo ""
  echo "==============================================================="
  echo "  Ralph Iteration $i of $MAX_ITERATIONS ($TOOL)"
  echo "==============================================================="

  # Stop gracefully if beads has no issues at all, or none are ready.
  # `bd ... --json` wraps results as {data:[...], schema_version:N}, not a
  # bare array, so unwrap `.data` before counting.
  READY_COUNT=$(bd ready --json 2>/dev/null | jq '(.data // .) | length' 2>/dev/null) || true
  TOTAL_COUNT=$(bd list --json 2>/dev/null | jq '(.data // .) | length' 2>/dev/null) || true

  if [[ -z "${TOTAL_COUNT}" || "${TOTAL_COUNT}" == "0" ]]; then
    echo ""
    echo "No issues exist in beads. Nothing to do. Stopping Ralph gracefully."
    exit 0
  fi

  if [[ -z "${READY_COUNT}" || "${READY_COUNT}" == "0" ]]; then
    echo ""
    echo "No ready tasks found. Stopping Ralph."
    exit 0
  fi

  ITER_START="$(date -u +%Y-%m-%dT%H:%M:%S)"

  # Run the selected tool with the ralph prompt
  OUTPUT=$(claude --dangerously-skip-permissions --print "$PROMPT" 2>&1 | tee /dev/stderr) || true

  if echo "$OUTPUT" | grep -q "<promise>COMPLETE</promise>"; then
    echo ""
    echo "Agent signaled completion and no beads issue changed. Stopping Ralph gracefully."
    exit 0
  fi

  # Verify against beads: find the issue touched during this iteration (most
  # recently updated one), rather than trusting a text signal from the tool.
  TOUCHED_JSON=$(bd list --json 2>/dev/null | jq -c \
    --arg since "$ITER_START" \
    '(.data // .) | [.[] | select((.updated_at // .updated // "") > $since)] | sort_by(.updated_at // .updated) | last // empty') || true

  if [[ -z "$TOUCHED_JSON" || "$TOUCHED_JSON" == "null" ]]; then
    echo ""
    echo "No beads issue was updated this iteration (likely closed with no trace, or no-op). Continuing to next ready check."
    PROMPT="/execute-iteration

Previous iteration touched no detectable issue (may have closed one with no remaining trace). Re-check ready work."
  else
    TOUCHED_ID=$(echo "$TOUCHED_JSON" | jq -r '.id')
    TOUCHED_STATUS=$(echo "$TOUCHED_JSON" | jq -r '.status')
    TOUCHED_NOTES=$(bd show "$TOUCHED_ID" --json 2>/dev/null | jq -r '(.data // .) | (.notes // .reason // "")')

    echo ""
    echo "Verified via beads: ${TOUCHED_ID} -> ${TOUCHED_STATUS}"

    if [[ "$TOUCHED_STATUS" == "closed" ]]; then
      echo "Issue ${TOUCHED_ID} closed. Checking for more ready work..."
    else
      echo "Issue ${TOUCHED_ID} left as '${TOUCHED_STATUS}' (not closed). Carrying details to next iteration."
    fi

    # Feed the outcome of this iteration into the next one so the agent has
    # continuity instead of re-discovering state from scratch.
    PROMPT="/execute-iteration

Previous iteration touched ${TOUCHED_ID} (status: ${TOUCHED_STATUS}).
Notes: ${TOUCHED_NOTES:-none}"
  fi

  echo "Iteration $i complete. Continuing..."
  sleep 2
done

echo ""
echo "Ralph reached max iterations ($MAX_ITERATIONS) without completing all tasks."
echo "Check beads for status."
exit 1