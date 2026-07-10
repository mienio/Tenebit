#!/usr/bin/env bash
set -Eeuo pipefail

DOMAIN="${1:-${TENEBIT_DOMAIN:-https://skinny-tiger4900.byst.re}}"
STAMP="$(date +%s)"
TEST_EMAIL="smoketest+${STAMP}@tenebit-internal.test"
TEST_PASSWORD="SmokeTest-${STAMP}-Pwd9!"

echo "=== TENEBIT SMOKE TEST ($DOMAIN) ==="

echo "  [1/3] /api/health/ready..."
READY="$(curl -fsS "$DOMAIN/api/health/ready" 2>&1)" || {
  echo "FAIL: backend/baza danych niedostępne."
  echo "  Odpowiedź: $READY"
  exit 1
}
if [[ "$READY" != *'"status":"ready"'* ]]; then
  echo "FAIL: /api/health/ready nie zwróciło statusu ready."
  echo "  Odpowiedź: $READY"
  exit 1
fi
echo "  OK"

echo "  [2/3] Rejestracja testowego konta (${TEST_EMAIL})..."
REGISTER_BODY="{\"organizationName\":\"Smoke Test ${STAMP}\",\"email\":\"${TEST_EMAIL}\",\"password\":\"${TEST_PASSWORD}\",\"displayName\":\"Smoke Test\",\"currency\":\"PLN\"}"
REGISTER_RESPONSE="$(curl -fsS -X POST "$DOMAIN/api/auth/register" -H "Content-Type: application/json" -d "$REGISTER_BODY" 2>&1)" || {
  echo "FAIL: rejestracja nie powiodła się."
  echo "  Odpowiedź: $REGISTER_RESPONSE"
  exit 1
}
if [[ "$REGISTER_RESPONSE" != *'"token"'* ]]; then
  echo "FAIL: rejestracja nie zwróciła tokenu."
  echo "  Odpowiedź: $REGISTER_RESPONSE"
  exit 1
fi
echo "  OK"

echo "  [3/3] Logowanie na testowe konto..."
LOGIN_BODY="{\"email\":\"${TEST_EMAIL}\",\"password\":\"${TEST_PASSWORD}\"}"
LOGIN_RESPONSE="$(curl -fsS -X POST "$DOMAIN/api/auth/login" -H "Content-Type: application/json" -d "$LOGIN_BODY" 2>&1)" || {
  echo "FAIL: logowanie nie powiodło się."
  echo "  Odpowiedź: $LOGIN_RESPONSE"
  exit 1
}
if [[ "$LOGIN_RESPONSE" != *'"token"'* ]]; then
  echo "FAIL: logowanie nie zwróciło tokenu."
  echo "  Odpowiedź: $LOGIN_RESPONSE"
  exit 1
fi
echo "  OK"

echo "=== SMOKE TEST PASSED ==="
echo "(konto testowe ${TEST_EMAIL} zostało utworzone w bazie — rozpoznawalne po prefiksie smoketest+)"
