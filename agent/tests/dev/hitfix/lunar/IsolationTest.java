package dev.hitfix.lunar;

import java.net.URL;
import java.net.URLClassLoader;
import java.nio.file.Paths;

public final class IsolationTest {
    public static void main(String[] arguments) throws Exception {
        URL classes = Paths.get(arguments[0]).toUri().toURL();
        ClassLoader isolated = new URLClassLoader(new URL[] {classes}, ClassLoader.getSystemClassLoader()) {
            protected synchronized Class<?> loadClass(String name, boolean resolve) throws ClassNotFoundException {
                if (name.startsWith("dev.hitfix.lunar.RuntimeHooks") || name.startsWith("dev.hitfix.lunar.HitFixLog")) {
                    throw new ClassNotFoundException("Agent helpers are intentionally isolated from the game");
                }
                if (name.startsWith("net.minecraft.")) {
                    Class<?> loaded = findLoadedClass(name);
                    if (loaded == null) {
                        loaded = findClass(name);
                    }
                    if (resolve) {
                        resolveClass(loaded);
                    }
                    return loaded;
                }
                return super.loadClass(name, resolve);
            }
        };
        Class<?> minecraftClass = isolated.loadClass("net.minecraft.client.Minecraft");
        Object minecraft = minecraftClass.newInstance();
        minecraftClass.getField("theWorld").set(minecraft, new Object());
        minecraftClass.getField("thePlayer").set(minecraft, new Object());
        Class<?> handlerClass = isolated.loadClass("net.minecraft.client.Minecraft$Handler");
        Object handler = handlerClass.newInstance();
        minecraftClass.getField("handler").set(minecraft, handler);
        minecraftClass.getMethod("runGameLoop").invoke(minecraft);
        Object network = handlerClass.getField("network").get(handler);
        int calls = network.getClass().getField("calls").getInt(network);
        if (calls != 1) {
            throw new AssertionError("Isolated game loader did not execute the frame hook");
        }
        minecraftClass.getMethod("runTick").invoke(minecraft);
        if (!minecraftClass.getField("tickSawEmptyQueue").getBoolean(minecraft)) {
            throw new AssertionError("Isolated tick hook did not execute");
        }
        System.out.println("PASS: hooks execute when the game classloader cannot resolve agent classes");
    }
}
