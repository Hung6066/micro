package com.hishope.operator.mobile;

import com.getcapacitor.BridgeActivity;
import com.hishope.mobile.HisHopeSecurityPlugin;

public class MainActivity extends BridgeActivity {
    public MainActivity() {
        registerPlugin(HisHopeSecurityPlugin.class);
    }
}
