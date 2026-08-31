package uc.adbtv;

import java.lang.reflect.Method;

/**
 * Resolves Android application labels through PackageManager when invoked with app_process.
 *
 * This helper deliberately uses reflection so it can be compiled with a regular JDK without
 * requiring android.jar. Keep this file as the source of truth for the embedded DEX helper.
 */
public final class AppNames {
    private AppNames() {}

    public static void main(String[] args) {
        try {
            // app_process does not prepare a main Looper for arbitrary Java entry points.
            // ActivityThread.systemMain() creates a Handler, so prepare the Looper first.
            Class<?> looperClass = Class.forName("android.os.Looper");
            looperClass.getMethod("prepareMainLooper").invoke(null);

            Class<?> activityThreadClass = Class.forName("android.app.ActivityThread");
            Object activityThread = activityThreadClass.getMethod("systemMain").invoke(null);
            Object context = activityThreadClass.getMethod("getSystemContext").invoke(activityThread);

            Class<?> contextClass = Class.forName("android.content.Context");
            Object packageManager = contextClass.getMethod("getPackageManager").invoke(context);

            Class<?> packageManagerClass = Class.forName("android.content.pm.PackageManager");
            Class<?> applicationInfoClass = Class.forName("android.content.pm.ApplicationInfo");
            Method getApplicationInfo = packageManagerClass.getMethod("getApplicationInfo", String.class, int.class);
            Method getApplicationLabel = packageManagerClass.getMethod("getApplicationLabel", applicationInfoClass);

            for (String packageName : args) {
                try {
                    Object applicationInfo = getApplicationInfo.invoke(packageManager, packageName, 0);
                    Object labelValue = getApplicationLabel.invoke(packageManager, applicationInfo);
                    String label = labelValue == null ? packageName : sanitize(labelValue.toString());
                    if (label.isEmpty()) {
                        label = packageName;
                    }
                    System.out.println(packageName + "\t" + label);
                } catch (Throwable ignored) {
                    System.out.println(packageName + "\t" + packageName);
                }
            }
        } catch (Throwable ignored) {
            for (String packageName : args) {
                System.out.println(packageName + "\t" + packageName);
            }
        }
    }

    private static String sanitize(String value) {
        return value.replace('\t', ' ').replace('\r', ' ').replace('\n', ' ').trim();
    }
}
