package com.winnigames2024.geodash.setup;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageInstaller;

/**
 * Ответ системы на установку. Телефон сначала спрашивает разрешение у игрока
 * (STATUS_PENDING_USER_ACTION) — его окно мы и показываем, — а потом сообщает,
 * получилось или нет.
 */
public class InstallReceiver extends BroadcastReceiver {

    public static final String ACTION = "com.winnigames2024.geodash.setup.INSTALL_STATUS";

    @Override public void onReceive(Context ctx, Intent intent) {
        int status = intent.getIntExtra(PackageInstaller.EXTRA_STATUS, -1);
        String msg = intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE);

        if (status == PackageInstaller.STATUS_PENDING_USER_ACTION) {
            Intent confirm = intent.getParcelableExtra(Intent.EXTRA_INTENT);
            if (confirm != null) {
                confirm.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                try { ctx.startActivity(confirm); } catch (Exception ignored) { }
            }
            return;
        }

        if (SetupActivity.live != null) SetupActivity.live.installDone(status, msg);
    }
}
