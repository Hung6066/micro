package com.hishope.mobile;

import android.content.Context;
import com.getcapacitor.JSObject;
import com.google.android.play.core.integrity.IntegrityManager;
import com.google.android.play.core.integrity.IntegrityManagerFactory;
import com.google.android.play.core.integrity.IntegrityTokenRequest;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.Base64;
import java.util.UUID;

/** Maps Play Integrity verdict availability into normalized posture signals (no raw tokens). */
final class HisHopeDeviceAttestation {
    interface Callback {
        void onSuccess(JSObject result);

        void onFailure(String message);
    }

    private HisHopeDeviceAttestation() {}

    static void collect(
            Context context,
            boolean rooted,
            boolean emulator,
            boolean debuggable,
            Callback callback) {
        JSObject signals = baseSignals(rooted, emulator, debuggable);
        long cloudProject = BuildConfig.PLAY_INTEGRITY_CLOUD_PROJECT_NUMBER;
        if (cloudProject <= 0L || !hasGooglePlayServices(context)) {
            signals.put("play_integrity_available", false);
            signals.put("play_integrity_verdict", false);
            callback.onSuccess(wrap(signals));
            return;
        }

        IntegrityManager manager = IntegrityManagerFactory.create(context);
        IntegrityTokenRequest request =
                IntegrityTokenRequest.builder()
                        .setCloudProjectNumber(cloudProject)
                        .setNonce(hashNonce(UUID.randomUUID().toString()))
                        .build();
        manager.requestIntegrityToken(request)
                .addOnSuccessListener(
                        response -> {
                            signals.put("play_integrity_available", true);
                            signals.put(
                                    "play_integrity_verdict",
                                    response.token() != null && !response.token().isEmpty());
                            callback.onSuccess(wrap(signals));
                        })
                .addOnFailureListener(
                        error -> {
                            signals.put("play_integrity_available", true);
                            signals.put("play_integrity_verdict", false);
                            callback.onSuccess(wrap(signals));
                        });
    }

    private static JSObject wrap(JSObject signals) {
        JSObject result = new JSObject();
        result.put("provider", "play-integrity");
        result.put("signals", signals);
        return result;
    }

    private static JSObject baseSignals(boolean rooted, boolean emulator, boolean debuggable) {
        JSObject signals = new JSObject();
        signals.put("device_secure", !rooted && !emulator);
        signals.put("not_rooted", !rooted);
        signals.put("not_emulator", !emulator);
        signals.put("not_debuggable", !debuggable);
        return signals;
    }

    private static boolean hasGooglePlayServices(Context context) {
        try {
            return com.google.android.gms.common.GoogleApiAvailability.getInstance()
                            .isGooglePlayServicesAvailable(context)
                    == com.google.android.gms.common.ConnectionResult.SUCCESS;
        } catch (Throwable ignored) {
            return false;
        }
    }

    private static String hashNonce(String nonce) {
        try {
            byte[] digest =
                    MessageDigest.getInstance("SHA-256")
                            .digest(nonce.getBytes(StandardCharsets.UTF_8));
            return Base64.getUrlEncoder().withoutPadding().encodeToString(digest);
        } catch (Exception error) {
            throw new IllegalStateException("Unable to hash Play Integrity nonce.", error);
        }
    }
}
