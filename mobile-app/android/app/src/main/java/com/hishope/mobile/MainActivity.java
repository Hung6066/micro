package com.hishope.mobile;

import android.content.pm.ApplicationInfo;
import android.os.Bundle;
import android.view.WindowManager;
import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {
    public MainActivity() {
        registerPlugin(HisHopeSecurityPlugin.class);
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        // Release builds hide the window from screenshots, recents, and the
        // Android Studio emulator capture stream. Debug installs stay visible
        // so local emulator runs are not a blank white screen.
        if ((getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) == 0) {
            getWindow().setFlags(
                WindowManager.LayoutParams.FLAG_SECURE,
                WindowManager.LayoutParams.FLAG_SECURE);
        }
    }
}
