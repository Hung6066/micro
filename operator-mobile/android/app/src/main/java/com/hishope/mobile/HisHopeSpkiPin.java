package com.hishope.mobile;

import android.util.Base64;
import java.security.MessageDigest;
import java.security.cert.X509Certificate;

/** RFC 7469 SPKI pin derived from the leaf certificate (matches iOS HisHopeSecurityPlugin). */
final class HisHopeSpkiPin {
    private HisHopeSpkiPin() {}

    static String sha256SpkiPin(X509Certificate certificate) throws java.security.cert.CertificateException {
        byte[] spki = subjectPublicKeyInfo(certificate.getEncoded());
        if (spki == null) {
            throw new java.security.cert.CertificateException("Unable to extract SubjectPublicKeyInfo");
        }
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256").digest(spki);
            return "sha256/" + Base64.encodeToString(digest, Base64.NO_WRAP);
        } catch (java.security.NoSuchAlgorithmException ex) {
            throw new java.security.cert.CertificateException("SHA-256 unavailable", ex);
        }
    }

    private static byte[] subjectPublicKeyInfo(byte[] certificateDer) {
        DerElement certificate = derElement(certificateDer, 0);
        if (certificate == null) return null;
        DerElement tbs = derElement(certificateDer, certificate.contentStart);
        if (tbs == null) return null;
        int offset = tbs.contentStart;
        for (int index = 0; index < 6 && offset < tbs.end; index++) {
            DerElement element = derElement(certificateDer, offset);
            if (element == null) return null;
            if (index == 5) {
                return copyRange(certificateDer, element.start, element.end);
            }
            offset = element.end;
        }
        return null;
    }

    private static byte[] copyRange(byte[] source, int start, int end) {
        byte[] copy = new byte[end - start];
        System.arraycopy(source, start, copy, 0, copy.length);
        return copy;
    }

    private static DerElement derElement(byte[] bytes, int offset) {
        if (offset >= bytes.length) return null;
        int start = offset;
        int index = offset + 1;
        if (index >= bytes.length) return null;
        int length = bytes[index] & 0xFF;
        index++;
        if ((length & 0x80) != 0) {
            int lengthBytes = length & 0x7F;
            if (lengthBytes == 0 || index + lengthBytes > bytes.length) return null;
            length = 0;
            for (int byteIndex = 0; byteIndex < lengthBytes; byteIndex++) {
                length = (length << 8) | (bytes[index + byteIndex] & 0xFF);
            }
            index += lengthBytes;
        }
        int contentStart = index;
        int end = contentStart + length;
        if (end > bytes.length) return null;
        return new DerElement(start, contentStart, end);
    }

    private static final class DerElement {
        final int start;
        final int contentStart;
        final int end;

        DerElement(int start, int contentStart, int end) {
            this.start = start;
            this.contentStart = contentStart;
            this.end = end;
        }
    }
}
