package com.hishope.mobile;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.Build;
import android.os.CancellationSignal;
import android.util.Base64;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import androidx.credentials.CredentialManager;
import androidx.credentials.CreateCredentialRequest;
import androidx.credentials.CreateCredentialResponse;
import androidx.credentials.CreatePublicKeyCredentialRequest;
import androidx.credentials.CreatePublicKeyCredentialResponse;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.GetPublicKeyCredentialOption;
import androidx.credentials.exceptions.CreateCredentialException;
import androidx.credentials.exceptions.CreateCredentialCancellationException;
import androidx.credentials.exceptions.CreateCredentialProviderConfigurationException;
import androidx.credentials.exceptions.CreateCredentialUnsupportedException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialProviderConfigurationException;
import androidx.credentials.exceptions.GetCredentialUnsupportedException;
import androidx.credentials.exceptions.NoCredentialException;
import java.io.File;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.security.cert.X509Certificate;
import java.util.Arrays;
import java.util.HashMap;
import java.util.Map;
import javax.net.ssl.HttpsURLConnection;
import javax.net.ssl.SSLContext;
import javax.net.ssl.SSLSocketFactory;
import javax.net.ssl.TrustManager;
import javax.net.ssl.TrustManagerFactory;
import javax.net.ssl.X509TrustManager;
import org.json.JSONArray;
import org.json.JSONObject;
import javax.crypto.SecretKeyFactory;
import javax.crypto.spec.PBEKeySpec;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

@CapacitorPlugin(name = "HisHopeSecurity")
public final class HisHopeSecurityPlugin extends Plugin {
    private static final String LEGACY_PREFS = "his_hope_security";
    private static final String PIN_HASH = "pin_hash";
    private static final String PIN_SALT = "pin_salt";
    private static final String PIN_FAIL_COUNT = "pin_fail_count";
    private static final String PIN_LOCK_UNTIL_MS = "pin_lock_until_ms";
    private static final int MAX_PIN_ATTEMPTS = 5;
    private static final long PIN_LOCK_BASE_MS = 30_000L;
    private final ExecutorService credentialExecutor = Executors.newSingleThreadExecutor();

    @PluginMethod
    public void deviceSecurity(PluginCall call) {
        boolean rooted = hasRootIndicators();
        boolean debuggable =
            (getContext().getApplicationInfo().flags
                    & android.content.pm.ApplicationInfo.FLAG_DEBUGGABLE)
                != 0;
        JSObject result = new JSObject();
        result.put("status", rooted ? "compromised" : "secure");
        result.put("rootedOrJailbroken", rooted);
        result.put("emulator", isEmulator());
        result.put("debuggable", debuggable);
        if (rooted) result.put("reason", "native_root_indicator");
        call.resolve(result);
    }

    @PluginMethod
    public void deviceAttestation(PluginCall call) {
        boolean rooted = hasRootIndicators();
        boolean debuggable =
            (getContext().getApplicationInfo().flags
                    & android.content.pm.ApplicationInfo.FLAG_DEBUGGABLE)
                != 0;
        HisHopeDeviceAttestation.collect(
            getContext(),
            rooted,
            isEmulator(),
            debuggable,
            new HisHopeDeviceAttestation.Callback() {
                @Override
                public void onSuccess(JSObject result) {
                    call.resolve(result);
                }

                @Override
                public void onFailure(String message) {
                    call.reject(message);
                }
            });
    }

    @PluginMethod
    public void configureCertificatePins(PluginCall call) {
        JSONArray submitted = call.getArray("pins");
        if (submitted == null) {
            call.reject("At least one valid certificate pin is required");
            return;
        }
        JSONArray bundled = bundledCertificatePins();
        if (bundled.length() == 0 && !isDebuggable()) {
            call.reject("Bundled certificate pins are required in release builds");
            return;
        }
        if (bundled.length() > 0
                && !containsPlaceholderPin(bundled)
                && !canonicalPins(submitted).equals(canonicalPins(bundled))) {
            call.reject("Certificate pins must match the bundled release allow-list");
            return;
        }
        if (containsPlaceholderPin(submitted) && !isDebuggable()) {
            call.reject("Release builds cannot use placeholder certificate pins");
            return;
        }
        getContext()
            .getSharedPreferences(LEGACY_PREFS, 0)
            .edit()
            .putString("certificate_pins", submitted.toString())
            .apply();
        call.resolve();
    }

    @PluginMethod
    public void isPinConfigured(PluginCall call) {
        migrateLegacyPinIfNeeded();
        boolean configured = securePrefs().contains(PIN_HASH);
        call.resolve(new JSObject().put("configured", configured));
    }

    @PluginMethod
    public void setAppPin(PluginCall call) {
        String pin = call.getString("pin", "");
        if (pin.length() < 6 || pin.length() > 12 || !pin.matches("\\d+")) {
            call.reject("PIN must contain 6-12 digits");
            return;
        }
        byte[] salt = new byte[16];
        new SecureRandom().nextBytes(salt);
        byte[] hash = derive(pin, salt);
        securePrefs().edit()
            .putString(PIN_SALT, Base64.encodeToString(salt, Base64.NO_WRAP))
            .putString(PIN_HASH, Base64.encodeToString(hash, Base64.NO_WRAP))
            .remove(PIN_FAIL_COUNT)
            .remove(PIN_LOCK_UNTIL_MS)
            .apply();
        getContext().getSharedPreferences(LEGACY_PREFS, 0).edit()
            .remove(PIN_HASH)
            .remove(PIN_SALT)
            .apply();
        call.resolve();
    }

    @PluginMethod
    public void verifyAppPin(PluginCall call) {
        migrateLegacyPinIfNeeded();
        long lockedUntil = securePrefs().getLong(PIN_LOCK_UNTIL_MS, 0L);
        if (lockedUntil > System.currentTimeMillis()) {
            call.reject("PIN entry is temporarily locked", "pin_locked");
            return;
        }
        String pin = call.getString("pin", "");
        SharedPreferences prefs = securePrefs();
        String saltValue = prefs.getString(PIN_SALT, null);
        String hashValue = prefs.getString(PIN_HASH, null);
        boolean valid = false;
        if (saltValue != null && hashValue != null) {
            valid = MessageDigest.isEqual(
                derive(pin, Base64.decode(saltValue, Base64.NO_WRAP)),
                Base64.decode(hashValue, Base64.NO_WRAP));
        }
        SharedPreferences.Editor editor = prefs.edit();
        if (valid) {
            editor.remove(PIN_FAIL_COUNT).remove(PIN_LOCK_UNTIL_MS);
        } else if (saltValue != null && hashValue != null) {
            int failures = prefs.getInt(PIN_FAIL_COUNT, 0) + 1;
            editor.putInt(PIN_FAIL_COUNT, failures);
            if (failures >= MAX_PIN_ATTEMPTS) {
                long backoff = PIN_LOCK_BASE_MS * (1L << Math.min(failures - MAX_PIN_ATTEMPTS, 4));
                editor.putLong(PIN_LOCK_UNTIL_MS, System.currentTimeMillis() + backoff);
            }
        }
        editor.apply();
        call.resolve(new JSObject().put("valid", valid));
    }

    @PluginMethod
    public void clearAppPin(PluginCall call) {
        securePrefs().edit()
            .remove(PIN_HASH)
            .remove(PIN_SALT)
            .remove(PIN_FAIL_COUNT)
            .remove(PIN_LOCK_UNTIL_MS)
            .apply();
        getContext().getSharedPreferences(LEGACY_PREFS, 0).edit()
            .remove(PIN_HASH)
            .remove(PIN_SALT)
            .apply();
        call.resolve();
    }

    @PluginMethod
    public void isPasskeySupported(PluginCall call) {
        boolean supported = Build.VERSION.SDK_INT >= Build.VERSION_CODES.P;
        call.resolve(new JSObject().put("supported", supported));
    }

    @PluginMethod
    public void createPasskey(PluginCall call) {
        String requestJson = call.getString("requestJson", "");
        if (requestJson.isBlank()) { call.reject("Passkey creation options are required"); return; }
        CredentialManager manager = CredentialManager.create(getContext());
        CreateCredentialRequest request = new CreatePublicKeyCredentialRequest(requestJson);
        manager.createCredentialAsync(getActivity(), request, new CancellationSignal(), credentialExecutor,
                new androidx.credentials.CredentialManagerCallback<CreateCredentialResponse, CreateCredentialException>() {
                    @Override public void onResult(CreateCredentialResponse result) {
                        if (!(result instanceof CreatePublicKeyCredentialResponse response)) { rejectUnsupported(call, "Credential Manager returned an unsupported credential"); return; }
                        call.resolve(new JSObject().put("responseJson", response.getRegistrationResponseJson()));
                    }
                    @Override public void onError(CreateCredentialException error) {
                        String detail = error.getMessage();
                        if (error instanceof CreateCredentialCancellationException) {
                            call.reject("Passkey registration was cancelled", "native_cancelled");
                            return;
                        }
                        if (error instanceof CreateCredentialUnsupportedException || error instanceof CreateCredentialProviderConfigurationException) {
                            rejectUnsupported(call, "Passkey registration is not supported by this Android credential provider");
                            return;
                        }
                        if (detail != null && detail.toLowerCase(java.util.Locale.ROOT).contains("cannot be validated")) {
                            call.reject("Passkey registration failed: Android cannot validate the RP domain. Configure Digital Asset Links for the passkey domain.", "native_rejected");
                            return;
                        }
                        call.reject("Passkey registration failed", "native_rejected");
                    }
                });
    }

    @PluginMethod
    public void authenticatePasskey(PluginCall call) {
        String requestJson = call.getString("requestJson", "");
        if (requestJson.isBlank()) { call.reject("Passkey request options are required"); return; }
        CredentialManager manager = CredentialManager.create(getContext());
        GetPublicKeyCredentialOption option = new GetPublicKeyCredentialOption(requestJson);
        GetCredentialRequest request = new GetCredentialRequest.Builder().addCredentialOption(option).build();
        manager.getCredentialAsync(getActivity(), request, new CancellationSignal(), credentialExecutor,
                new androidx.credentials.CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override public void onResult(GetCredentialResponse result) {
                        if (!(result.getCredential() instanceof androidx.credentials.PublicKeyCredential credential)) { rejectUnsupported(call, "Credential Manager returned an unsupported credential"); return; }
                        call.resolve(new JSObject().put("responseJson", credential.getAuthenticationResponseJson()));
                    }
                    @Override public void onError(GetCredentialException error) {
                        if (error instanceof GetCredentialCancellationException) {
                            call.reject("Passkey authentication was cancelled", "native_cancelled");
                            return;
                        }
                        if (error instanceof GetCredentialUnsupportedException || error instanceof GetCredentialProviderConfigurationException || error instanceof NoCredentialException) {
                            rejectUnsupported(call, "Native passkey authentication is not available on this Android device");
                            return;
                        }
                        call.reject("Passkey authentication failed", "native_rejected");
                    }
                });
    }

    private void rejectUnsupported(PluginCall call, String message) {
        call.reject(message, "native_unsupported");
    }

    @PluginMethod
    public void openPinnedAuthBrowser(PluginCall call) {
        String url = call.getString("url", "");
        if (url.isBlank()) {
            call.reject("Authorization URL is required");
            return;
        }
        getActivity().runOnUiThread(() -> {
            android.content.Intent intent =
                new android.content.Intent(getActivity(), OidcAuthActivity.class);
            intent.putExtra(OidcAuthActivity.EXTRA_URL, url);
            getActivity().startActivity(intent);
            call.resolve();
        });
    }

    @PluginMethod
    public void request(PluginCall call) {
        String rawUrl = call.getString("url", "");
        String method = call.getString("method", "GET");
        if (rawUrl.isBlank() || !(method.matches("GET|POST|PUT|PATCH|DELETE|HEAD"))) {
            call.reject("Invalid native HTTP request");
            return;
        }
        new Thread(() -> executeRequest(call, rawUrl, method)).start();
    }

    private void executeRequest(PluginCall call, String rawUrl, String method) {
        HttpURLConnection connection = null;
        try {
            URL url = new URL(rawUrl);
            connection = (HttpURLConnection) url.openConnection();
            if (connection instanceof HttpsURLConnection https) {
                https.setSSLSocketFactory(pinnedSocketFactory(url.getHost()));
            }
            connection.setRequestMethod(method);
            connection.setConnectTimeout(15_000);
            connection.setReadTimeout(30_000);
            connection.setInstanceFollowRedirects(false);
            JSObject headers = call.getObject("headers");
            if (headers != null) {
                java.util.Iterator<String> keys = headers.keys();
                while (keys.hasNext()) {
                    String key = keys.next();
                    connection.setRequestProperty(key, headers.optString(key, ""));
                }
            }
            String body = call.getString("body", null);
            if (body != null && !body.isEmpty() && !method.equals("GET") && !method.equals("HEAD")) {
                connection.setDoOutput(true);
                try (OutputStream output = connection.getOutputStream()) {
                    output.write(body.getBytes(StandardCharsets.UTF_8));
                }
            }
            int status = connection.getResponseCode();
            InputStream stream = status >= 400 ? connection.getErrorStream() : connection.getInputStream();
            String responseBody = stream == null ? "" : new String(stream.readAllBytes(), StandardCharsets.UTF_8);
            JSObject result = new JSObject();
            result.put("status", status);
            result.put("body", responseBody);
            JSObject responseHeaders = new JSObject();
            for (Map.Entry<String, java.util.List<String>> entry : connection.getHeaderFields().entrySet()) {
                if (entry.getKey() != null && entry.getValue() != null) responseHeaders.put(entry.getKey(), String.join(", ", entry.getValue()));
            }
            result.put("headers", responseHeaders);
            call.resolve(result);
        } catch (Exception ex) {
            call.reject("Native HTTP request failed", ex.getMessage());
        } finally {
            if (connection != null) connection.disconnect();
        }
    }

    private SSLSocketFactory pinnedSocketFactory(String host) throws Exception {
        TrustManagerFactory factory = TrustManagerFactory.getInstance(TrustManagerFactory.getDefaultAlgorithm());
        factory.init((java.security.KeyStore) null);
        X509TrustManager systemTrust = (X509TrustManager) Arrays.stream(factory.getTrustManagers())
                .filter(manager -> manager instanceof X509TrustManager)
                .findFirst()
                .orElseThrow(() -> new IllegalStateException("No system trust manager"));
        String[] pins = pinsForHost(host);
        if (pins.length == 0) {
            if (isDebuggable()) {
                SSLContext context = SSLContext.getInstance("TLS");
                context.init(null, new TrustManager[] { systemTrust }, new SecureRandom());
                return context.getSocketFactory();
            }
            throw new IllegalStateException("No certificate pin configured for " + host);
        }
        X509TrustManager pinningTrust = new X509TrustManager() {
            public X509Certificate[] getAcceptedIssuers() { return systemTrust.getAcceptedIssuers(); }
            public void checkClientTrusted(X509Certificate[] chain, String authType) throws java.security.cert.CertificateException { systemTrust.checkClientTrusted(chain, authType); }
            public void checkServerTrusted(X509Certificate[] chain, String authType) throws java.security.cert.CertificateException {
                systemTrust.checkServerTrusted(chain, authType);
                if (chain == null || chain.length == 0) throw new java.security.cert.CertificateException("Empty server certificate chain");
                String actual;
                try {
                    actual = HisHopeSpkiPin.sha256SpkiPin(chain[0]);
                } catch (java.security.cert.CertificateException ex) {
                    throw ex;
                }
                if (!Arrays.asList(pins).contains(actual)) throw new java.security.cert.CertificateException("Certificate pin mismatch");
            }
        };
        SSLContext context = SSLContext.getInstance("TLS");
        context.init(null, new TrustManager[] { pinningTrust }, new SecureRandom());
        return context.getSocketFactory();
    }

    private String[] pinsForHost(String host) {
        String raw = getContext().getSharedPreferences(LEGACY_PREFS, 0).getString("certificate_pins", "[]");
        try {
            JSONArray array = new JSONArray(raw);
            java.util.List<String> pins = new java.util.ArrayList<>();
            for (int index = 0; index < array.length(); index++) {
                JSONObject item = array.optJSONObject(index);
                if (item != null && host.equalsIgnoreCase(item.optString("host"))) pins.add(item.optString("sha256Spki"));
            }
            return pins.toArray(new String[0]);
        } catch (Exception ex) {
            throw new IllegalStateException("Invalid certificate pin configuration", ex);
        }
    }

    private static byte[] derive(String pin, byte[] salt) {
        try {
            PBEKeySpec spec = new PBEKeySpec(pin.toCharArray(), salt, 120_000, 256);
            return SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256").generateSecret(spec).getEncoded();
        } catch (Exception ex) {
            throw new IllegalStateException("Unable to derive PIN hash", ex);
        }
    }

    private boolean hasRootIndicators() {
        String tags = Build.TAGS == null ? "" : Build.TAGS;
        if (tags.contains("test-keys")) return true;
        String[] paths = { "/system/bin/su", "/system/xbin/su", "/sbin/su", "/data/adb/magisk" };
        return Arrays.stream(paths).anyMatch(path -> new File(path).exists());
    }

    private boolean isEmulator() {
        return Build.FINGERPRINT.startsWith("generic") || Build.MODEL.contains("Emulator") ||
                Build.MODEL.contains("Android SDK built for");
    }

    private boolean isDebuggable() {
        return (getContext().getApplicationInfo().flags & android.content.pm.ApplicationInfo.FLAG_DEBUGGABLE) != 0;
    }

    private SharedPreferences securePrefs() {
        return HisHopeSecurePrefs.open(getContext());
    }

    private void migrateLegacyPinIfNeeded() {
        SharedPreferences legacy = getContext().getSharedPreferences(LEGACY_PREFS, 0);
        String legacyHash = legacy.getString(PIN_HASH, null);
        String legacySalt = legacy.getString(PIN_SALT, null);
        if (legacyHash == null || legacySalt == null) return;
        SharedPreferences secure = securePrefs();
        if (secure.contains(PIN_HASH)) {
            legacy.edit().remove(PIN_HASH).remove(PIN_SALT).apply();
            return;
        }
        secure.edit().putString(PIN_HASH, legacyHash).putString(PIN_SALT, legacySalt).apply();
        legacy.edit().remove(PIN_HASH).remove(PIN_SALT).apply();
    }

    private JSONArray bundledCertificatePins() {
        try (InputStream stream = getContext().getResources().openRawResource(com.hishope.operator.mobile.R.raw.certificate_pins)) {
            return new JSONArray(new String(stream.readAllBytes(), StandardCharsets.UTF_8));
        } catch (Exception ex) {
            return new JSONArray();
        }
    }

    private static boolean containsPlaceholderPin(JSONArray pins) {
        for (int index = 0; index < pins.length(); index++) {
            JSONObject item = pins.optJSONObject(index);
            if (item == null) continue;
            String spki = item.optString("sha256Spki", "");
            if (spki.contains("REPLACE_IN_RELEASE")) return true;
        }
        return false;
    }

    private static String canonicalPins(JSONArray pins) {
        java.util.List<String> entries = new java.util.ArrayList<>();
        for (int index = 0; index < pins.length(); index++) {
            JSONObject item = pins.optJSONObject(index);
            if (item == null) continue;
            entries.add(item.optString("host", "").toLowerCase(java.util.Locale.ROOT) + "=" + item.optString("sha256Spki"));
        }
        java.util.Collections.sort(entries);
        return String.join("|", entries);
    }
}
