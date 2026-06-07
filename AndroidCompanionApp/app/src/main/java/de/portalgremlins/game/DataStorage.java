package de.portalgremlins.game;

import android.content.Context;
import android.content.SharedPreferences;

public class DataStorage {
    public static final DataStorage instance = new DataStorage();
    private static final String prefsName = "RiftBornAppData";
    private static final String KEY_USERNAME = "";

    public static DataStorage getInstance() {
        return instance;
    }

    private DataStorage() {
    }

    public String getStoredUserName(Context context) {
        return storage(context).getString(KEY_USERNAME, "Guest");
    }

    public void setStoredUserName(Context context, String newName) {
        storage(context).edit().putString(KEY_USERNAME, newName).apply();
    }

    private SharedPreferences storage(Context context) {
        return context.getSharedPreferences(prefsName, Context.MODE_PRIVATE);
    }
}
