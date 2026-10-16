package dev.hitfix.lunar;

import java.io.File;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.time.Instant;

public final class HitFixLog {
    public static synchronized void write(String message) {
        String line = "[HitFix-Lunar] " + message;
        System.out.println(line);
        String path = System.getProperty("hitfix.log");
        if (path != null) {
            try (PrintWriter output = new PrintWriter(new FileWriter(new File(path), true))) {
                output.println(Instant.now() + " " + line);
            } catch (Exception failure) {
                System.err.println("[HitFix-Lunar] Cannot write log: " + failure.getClass().getSimpleName());
            }
        }
    }
}
