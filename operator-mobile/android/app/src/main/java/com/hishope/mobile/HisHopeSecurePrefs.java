package com.hishope.mobile;

import android.content.Context;
import android.content.SharedPreferences;
import androidx.security.crypto.EncryptedSharedPreferences;
import androidx.security.crypto.MasterKey;
import java.io.IOException;
import java.security.GeneralSecurityException;

/** Encrypted preferences for PIN material and other device-local secrets. */
final class HisHopeSecurePrefs {
    private static final String FILE = "his_hope_secure";

    private HisHopeSecurePrefs() {}

    static SharedPreferences open(Context context) {
        try {
            MasterKey masterKey = new MasterKey.Builder(context)
                .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
                .build();
            return EncryptedSharedPreferences.create(
                context,
                FILE,
                masterKey,
                EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM);
        } catch (GeneralSecurityException | IOException ex) {
            throw new IllegalStateException("Unable to open encrypted preferences", ex);
        }
    }
}
