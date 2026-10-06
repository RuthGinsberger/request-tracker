#!/usr/bin/env bash
# Smoke test of the full Docker Compose stack (see docs/COMPOSE_SMOKE_CHECKLIST.md).
# Run from the repo root on a machine with Docker, curl and jq:  ./ci/compose-smoke.sh
# Always tears the stack down (docker compose down -v) and prints the logs on failure.
set -euo pipefail

UI_URL="${UI_URL:-http://localhost:8080}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-900}"   # image builds + migrate + seed 100k rows

for tool in docker curl jq; do
  command -v "$tool" >/dev/null || { echo "Missing required tool: $tool" >&2; exit 2; }
done

cleanup() {
  status=$?
  if [ "$status" -ne 0 ]; then
    echo "::group::docker compose ps -a"
    docker compose ps -a || true
    echo "::endgroup::"
    echo "::group::docker compose logs"
    docker compose logs --no-color || true
    echo "::endgroup::"
    echo "SMOKE TEST FAILED (exit $status)"
  fi
  docker compose down -v --remove-orphans || true
  exit "$status"
}
trap cleanup EXIT

fail() { echo "FAIL: $*" >&2; exit 1; }
step() { echo; echo "== $*"; }

# curl that records the HTTP status in $HTTP_CODE, the body in $BODY and the headers in $HEADERS_FILE.
HEADERS_FILE="$(mktemp)"
BODY_FILE="$(mktemp)"
request() {
  HTTP_CODE="$(curl -sS -o "$BODY_FILE" -w '%{http_code}' -D "$HEADERS_FILE" "$@")" || fail "curl failed: $*"
  BODY="$(cat "$BODY_FILE")"
}
expect_status() { [ "$HTTP_CODE" = "$1" ] || fail "$2: expected HTTP $1, got $HTTP_CODE. Body: $BODY"; }

step "Build and start (db -> seeder -> api -> ui)"
# Not --wait: the seeder is a one-shot service, so we wait for each piece explicitly below.
docker compose up --build -d

step "Wait for the seeder to exit 0"
deadline=$((SECONDS + TIMEOUT_SECONDS))
while true; do
  state="$(docker compose ps -a --format '{{.State}}' seeder 2>/dev/null || true)"
  if [ "$state" = "exited" ]; then
    code="$(docker compose ps -a --format '{{.ExitCode}}' seeder)"
    [ "$code" = "0" ] || fail "seeder exited with code $code"
    echo "seeder finished with exit code 0"
    break
  fi
  [ "$SECONDS" -lt "$deadline" ] || fail "timeout waiting for the seeder (state: ${state:-unknown})"
  sleep 5
done

step "Wait for api and ui to be healthy"
for service in api ui; do
  while true; do
    cid="$(docker compose ps -q "$service")"
    health=""
    [ -z "$cid" ] || health="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$cid")"
    if [ "$health" = "healthy" ]; then echo "$service is healthy"; break; fi
    [ "$health" != "unhealthy" ] || fail "$service is unhealthy"
    [ "$SECONDS" -lt "$deadline" ] || fail "timeout waiting for $service (state: ${health:-no container})"
    sleep 5
  done
done

step "GET / returns the SPA"
request "$UI_URL/"
expect_status 200 "GET /"
grep -q '<app-root' <<<"$BODY" || fail "GET / does not contain <app-root"

step "GET /requests?status=New (SPA deep link, not 404)"
request "$UI_URL/requests?status=New"
expect_status 200 "SPA deep link"

step "GET /api/requests?pageSize=5 through the UI proxy"
request "$UI_URL/api/requests?pageSize=5"
expect_status 200 "GET /api/requests"
[ "$(jq '.items | length' <<<"$BODY")" = "5" ] || fail "expected 5 items. Body: $BODY"
[ "$(jq '.totalCount' <<<"$BODY")" = "100000" ] || fail "expected totalCount 100000. Body: $BODY"

step "GET /api/requests/summary"
request "$UI_URL/api/requests/summary"
expect_status 200 "GET /api/requests/summary"
[ "$(jq '.totalCount' <<<"$BODY")" = "100000" ] || fail "expected summary totalCount 100000. Body: $BODY"
[ "$(jq '[.byStatus[].count] | add' <<<"$BODY")" = "100000" ] || fail "status counts do not add up to 100000. Body: $BODY"

step "PATCH status with a fresh ETag, then again with the stale one"
# A request in status New can only move to InProgress.
request "$UI_URL/api/requests?status=New&pageSize=1"
expect_status 200 "GET a New request"
id="$(jq -r '.items[0].id' <<<"$BODY")"
[ -n "$id" ] && [ "$id" != "null" ] || fail "no request in status New. Body: $BODY"

request "$UI_URL/api/requests/$id"
expect_status 200 "GET /api/requests/$id"
etag="$(tr -d '\r' <"$HEADERS_FILE" | sed -n 's/^[Ee][Tt]ag: *//p' | head -n 1)"
[ -n "$etag" ] || fail "no ETag header on GET /api/requests/$id"
echo "request $id, ETag $etag"

patch_status() {
  request -X PATCH "$UI_URL/api/requests/$id/status" \
    -H 'Content-Type: application/json' -H 'X-User-Name: smoke' -H "If-Match: $etag" \
    -d '{"status":"InProgress"}'
}
patch_status
expect_status 200 "first PATCH"
[ "$(jq -r '.status' <<<"$BODY")" = "InProgress" ] || fail "status not updated. Body: $BODY"
patch_status
expect_status 409 "second PATCH with the stale ETag"

step "Re-running the seeder is a no-op (no duplicate data)"
docker compose run --rm seeder
request "$UI_URL/api/requests?pageSize=1"
expect_status 200 "GET after re-seed"
[ "$(jq '.totalCount' <<<"$BODY")" = "100000" ] || fail "totalCount changed after re-running the seeder. Body: $BODY"

step "OpenAPI UI is hidden in Production"
request "http://localhost:5280/scalar/v1"
expect_status 404 "GET /scalar/v1"

echo; echo "SMOKE TEST PASSED"
