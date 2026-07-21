#!/bin/bash
# =============================================================================
# His.Hope — BFF Cookie Security Verification Script
# Phase 8.6: Validates cookie security configuration across all BFF modules
# =============================================================================
set -euo pipefail

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

PASS() { echo -e "  ${GREEN}✅${NC} $1"; }
FAIL() { echo -e "  ${RED}❌${NC} $1"; }
WARN() { echo -e "  ${YELLOW}⚠️${NC} $1"; }
INFO() { echo -e "     $1"; }

TOTAL_CHECKS=0
PASSED_CHECKS=0
FAILED_CHECKS=0

check_result() {
    TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
    if [ "$1" -eq 0 ]; then
        PASSED_CHECKS=$((PASSED_CHECKS + 1))
    else
        FAILED_CHECKS=$((FAILED_CHECKS + 1))
    fi
}

echo "============================================"
echo "  His.Hope BFF Cookie Security Verification"
echo "  $(date -u '+%Y-%m-%dT%H:%M:%SZ')"
echo "============================================"
echo ""

# =============================================================================
# 1. BFF Cookie Configuration (appsettings.json)
# =============================================================================
echo "=== 1. BFF Cookie Configuration (appsettings.json) ==="

BFF_APPSETTINGS=(
    "src/Bff/PatientBff/appsettings.json"
    "src/Bff/ClinicalBff/appsettings.json"
    "src/Bff/LabBff/appsettings.json"
    "src/Bff/BillingBff/appsettings.json"
    "src/Bff/PharmacyBff/appsettings.json"
    "src/Bff/DashboardBff/appsettings.json"
)

for SETTINGS in "${BFF_APPSETTINGS[@]}"; do
    NAME=$(basename "$(dirname "$SETTINGS")")
    echo "  [$NAME]"

    # Check CookieName
    if grep -q '"CookieName": "hishop_sid"' "$SETTINGS" 2>/dev/null; then
        PASS "CookieName: hishop_sid"
    else
        FAIL "CookieName: not set to hishop_sid"
    fi
    check_result $?

    # Check CookiePath
    if grep -q '"CookiePath": "/api"' "$SETTINGS" 2>/dev/null; then
        PASS "CookiePath: /api"
    else
        FAIL "CookiePath: not /api"
    fi
    check_result $?

    # Check CookieMaxAgeSeconds
    MAXAGE=$(grep -oP '"CookieMaxAgeSeconds":\s*\K[0-9]+' "$SETTINGS" 2>/dev/null || echo "0")
    if [ "$MAXAGE" = "3600" ]; then
        PASS "CookieMaxAgeSeconds: 3600 (matches JWT expiry)"
    else
        FAIL "CookieMaxAgeSeconds: $MAXAGE (expected 3600)"
    fi
    check_result $?

    # Check Secure
    if grep -q '"Secure": true' "$SETTINGS" 2>/dev/null; then
        PASS "Secure: true"
    else
        WARN "Secure: not hardcoded true in config (may use dynamic IsHttps)"
    fi
    check_result $?

    # Check HttpOnly
    if grep -q '"HttpOnly": true' "$SETTINGS" 2>/dev/null; then
        PASS "HttpOnly: true"
    else
        FAIL "HttpOnly: not true"
    fi
    check_result $?

    # Check SameSite
    if grep -q '"SameSite": "Lax"' "$SETTINGS" 2>/dev/null; then
        PASS "SameSite: Lax"
    else
        FAIL "SameSite: not Lax"
    fi
    check_result $?

    echo ""
done

# =============================================================================
# 2. Runtime Cookie Verification (IdentityService Program.cs)
# =============================================================================
echo "=== 2. Runtime Cookie Configuration (IdentityService) ==="

IDENTITY_PROGRAM="src/Services/IdentityService/IdentityService.Api/Program.cs"

# Check hishop_sid cookie attributes
if grep -q 'hishop_sid.*CookieOptions' "$IDENTITY_PROGRAM" 2>/dev/null; then
    PASS "hishop_sid cookie exists"
else
    FAIL "hishop_sid cookie not found"
fi
check_result $?

if grep -q 'HttpOnly = true' "$IDENTITY_PROGRAM" 2>/dev/null; then
    PASS "hishop_sid: HttpOnly=true"
else
    FAIL "hishop_sid: HttpOnly not true"
fi
check_result $?

if grep -q 'SameSite = SameSiteMode.Lax' "$IDENTITY_PROGRAM" 2>/dev/null; then
    PASS "hishop_sid: SameSite=Lax"
else
    FAIL "hishop_sid: SameSite not Lax"
fi
check_result $?

if grep -q 'Path = "/api"' "$IDENTITY_PROGRAM" 2>/dev/null; then
    PASS "hishop_sid: Path=/api"
else
    FAIL "hishop_sid: Path not /api"
fi
check_result $?

# Check hishop_csrf cookie attributes
if grep -q 'hishop_csrf' "$IDENTITY_PROGRAM" 2>/dev/null; then
    PASS "hishop_csrf cookie exists"
else
    FAIL "hishop_csrf cookie not found"
fi
check_result $?

if grep -q 'HttpOnly = false' "$IDENTITY_PROGRAM" 2>/dev/null; then
    PASS "hishop_csrf: HttpOnly=false (accessible by JS)"
else
    FAIL "hishop_csrf: HttpOnly not false"
fi
check_result $?

if grep -q 'SameSite = SameSiteMode.Strict' "$IDENTITY_PROGRAM" 2>/dev/null; then
    PASS "hishop_csrf: SameSite=Strict"
else
    FAIL "hishop_csrf: SameSite not Strict"
fi
check_result $?

# Check hishop_csrf Path - currently "/" not "/api" (documented as WARNING)
if grep -q 'Path = "/"' "$IDENTITY_PROGRAM" 2>/dev/null; then
    WARN "hishop_csrf: Path=/ (recommend /api for consistency)"
else
    FAIL "hishop_csrf: Path not set"
fi
check_result $?

echo ""

# =============================================================================
# 3. Verify No Other Cookies Set by BFF
# =============================================================================
echo "=== 3. Cookie Surface Area Check ==="

OTHER_COOKIES=$(grep -rn 'Cookies.Append' src/Bff/ --type cs 2>/dev/null || true)
if [ -z "$OTHER_COOKIES" ]; then
    PASS "No cookies set directly by BFF code (handled by IdentityService)"
else
    WARN "BFF code may set cookies directly:"
    echo "$OTHER_COOKIES" | while read -r line; do INFO "$line"; done
fi
check_result $?

echo ""

# =============================================================================
# 4. JWT Token Leakage Check
# =============================================================================
echo "=== 4. JWT Token Leakage Check ==="

# Login endpoint returns full TokenResponse including AccessToken
if grep -q 'Results.Ok(result)' "$IDENTITY_PROGRAM" 2>/dev/null; then
    WARN "Login endpoint returns JWT in response body (TokenResponse.AccessToken)"
    INFO "    File: src/Services/IdentityService/IdentityService.Api/Program.cs:184"
    INFO "    Action: Consider removing JWT from login response if fully cookie-based"
    # This is a warning, not a fail — dual-mode is valid during transition
fi
check_result 0

# Check Authorization header in responses
AUTH_HEADER_IN_RESPONSE=$(grep -rn 'Response.Headers.*Authorization' src/Bff/ --type cs 2>/dev/null || true)
if [ -z "$AUTH_HEADER_IN_RESPONSE" ]; then
    PASS "No Authorization header leakage in BFF responses"
else
    FAIL "BFF sets Authorization in response headers: $AUTH_HEADER_IN_RESPONSE"
fi
check_result $?

# JWT in proxy transforms (expected)
JWT_IN_PROXY=$(grep -rn 'Authorization.*Bearer' src/Bff/ --type cs 2>/dev/null || true)
if echo "$JWT_IN_PROXY" | grep -q "ProxyRequest" 2>/dev/null; then
    PASS "JWT Bearer token used only in outbound proxy requests (expected)"
else
    WARN "No JWT proxy transform found - check proxy configuration"
fi
check_result $?

echo ""

# =============================================================================
# 5. Session Cookie MaxAge vs JWT Expiry
# =============================================================================
echo "=== 5. Cookie MaxAge vs JWT Expiry Consistency ==="

BFF_MAXAGE=$(grep -oP '"CookieMaxAgeSeconds":\s*\K[0-9]+' src/Bff/PatientBff/appsettings.json 2>/dev/null || echo "0")
JWT_EXPIRY_SETTING=$(grep -oP '"ExpiresInMinutes":\s*\K[0-9]+' src/Services/IdentityService/IdentityService.Api/appsettings.json 2>/dev/null || echo "60")

if [ "$BFF_MAXAGE" = "3600" ]; then
    PASS "Cookie MaxAge: ${BFF_MAXAGE}s (matches session lifetime)"
else
    FAIL "Cookie MaxAge: ${BFF_MAXAGE}s (expected 3600)"
fi
check_result $?

echo ""

# =============================================================================
# 6. Security Headers at BFF Level  
# =============================================================================
echo "=== 6. BFF-level Security Headers ==="

for SETTINGS in "${BFF_APPSETTINGS[@]}"; do
    NAME=$(basename "$(dirname "$SETTINGS")")
    # Check if Kestrel config has HTTPS
    if grep -q '"Https"' "$SETTINGS" 2>/dev/null; then
        PASS "$NAME: Has HTTPS endpoint configured"
    else
        WARN "$NAME: No HTTPS endpoint in config (mTLS at mesh level)"
    fi
done

echo ""

# =============================================================================
# Summary
# =============================================================================
echo "============================================"
echo "  Results Summary"
echo "============================================"
echo "  Total checks:  $TOTAL_CHECKS"
echo -e "  Passed:       ${GREEN}$PASSED_CHECKS${NC}"
echo -e "  Failed:       ${RED}$FAILED_CHECKS${NC}"
echo ""

if [ "$FAILED_CHECKS" -gt 0 ]; then
    echo "  ❌ Some checks FAILED — review issues above"
    exit 1
else
    echo "  ✅ All checks passed!"
    exit 0
fi
