package com.hishope.mobile;

import android.annotation.SuppressLint;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.net.Uri;
import android.os.Bundle;
import android.view.WindowManager;
import android.webkit.CookieManager;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import androidx.appcompat.app.AppCompatActivity;
import java.util.Arrays;
import java.util.HashSet;
import java.util.Set;

/**
 * In-app OIDC browser. Chrome Custom Tabs follow Identity's Docker
 * {@code Location} headers ({@code identityservice:5003}) which the emulator
 * cannot resolve. Debug builds rewrite those hosts onto the public gateway
 * origin the app used to start login (typically {@code 10.0.2.2:5000}).
 */
public final class OidcAuthActivity extends AppCompatActivity {
    static final String EXTRA_URL = "url";
    private static final Set<String> DOCKER_HOSTS = new HashSet<>(
        Arrays.asList("identityservice", "identity", "his-hope-identity"));

    private Uri publicOrigin;

    @SuppressLint("SetJavaScriptEnabled")
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (!isDebuggable()) {
            getWindow().setFlags(
                WindowManager.LayoutParams.FLAG_SECURE,
                WindowManager.LayoutParams.FLAG_SECURE);
        }
        setTitle(com.hishope.operator.mobile.R.string.title_activity_main);

        String raw = getIntent().getStringExtra(EXTRA_URL);
        Uri start = raw == null ? Uri.EMPTY : Uri.parse(raw);
        publicOrigin = publicOriginOf(start);

        WebView webView = new WebView(this);
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        if (isDebuggable()) {
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        } else {
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        }
        CookieManager cookies = CookieManager.getInstance();
        cookies.setAcceptCookie(true);
        cookies.setAcceptThirdPartyCookies(webView, isDebuggable());
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                return handleUrl(view, request.getUrl());
            }

            @Override
            public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
                Uri current = Uri.parse(url);
                Uri rewritten = rewrite(current);
                if (!same(rewritten, current)) view.loadUrl(rewritten.toString());
            }

            @Override
            public void onReceivedError(
                WebView view,
                WebResourceRequest request,
                android.webkit.WebResourceError error) {
                if (!request.isForMainFrame()) return;
                Uri rewritten = rewrite(request.getUrl());
                if (!same(rewritten, request.getUrl())) view.loadUrl(rewritten.toString());
            }
        });
        setContentView(webView);
        webView.loadUrl(rewrite(start).toString());
    }

    private boolean handleUrl(WebView view, Uri uri) {
        if (uri == null) return true;
        if ("hishope".equalsIgnoreCase(uri.getScheme())) {
            String path = uri.getPath() == null ? "" : uri.getPath();
            if (!"/callback".equals(path) && !"/logout-callback".equals(path)) {
                finish();
                return true;
            }
            Intent callback = new Intent(Intent.ACTION_VIEW, uri);
            callback.setPackage(getPackageName());
            callback.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
            startActivity(callback);
            finish();
            return true;
        }
        Uri rewritten = rewrite(uri);
        if (!same(rewritten, uri)) {
            view.loadUrl(rewritten.toString());
            return true;
        }
        return false;
    }

    private static boolean same(Uri left, Uri right) {
        if (left == right) return true;
        if (left == null || right == null) return false;
        return left.toString().equals(right.toString());
    }

    private Uri rewrite(Uri uri) {
        if (!isDebuggable() || uri == null || publicOrigin == null || uri.getHost() == null) {
            return uri;
        }
        String host = uri.getHost();
        int port = uri.getPort();
        boolean docker = DOCKER_HOSTS.contains(host.toLowerCase());
        boolean localOidc =
            ("localhost".equalsIgnoreCase(host) || "127.0.0.1".equals(host))
                && (port == 5001 || port == 5003);
        if (!docker && !localOidc) return uri;
        return uri.buildUpon()
            .scheme(publicOrigin.getScheme())
            .encodedAuthority(publicOrigin.getEncodedAuthority())
            .build();
    }

    private static Uri publicOriginOf(Uri uri) {
        if (uri == null || uri.getHost() == null) return uri;
        String host = uri.getHost();
        if (DOCKER_HOSTS.contains(host.toLowerCase())) {
            return Uri.parse("http://10.0.2.2:5000");
        }
        return uri.buildUpon().path("/").query(null).fragment(null).build();
    }

    private boolean isDebuggable() {
        return (getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) != 0;
    }
}
