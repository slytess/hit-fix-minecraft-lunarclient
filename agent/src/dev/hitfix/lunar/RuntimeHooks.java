package dev.hitfix.lunar;

import java.lang.reflect.Constructor;
import java.lang.reflect.Field;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.nio.charset.StandardCharsets;
import java.util.Queue;
import java.util.function.Consumer;

public final class RuntimeHooks {
    private static Access access;
    private static boolean disabled = !Boolean.parseBoolean(System.getProperty("hitfix.enabled", "true"));
    private static boolean sawFrame;
    private static boolean sawTick;
    private static boolean sawPackets;
    private static Object announced;
    private static Object activeNetwork;

    public static Consumer<?>[] createHooks() {
        Consumer<Object> frame = RuntimeHooks::beforeFrame;
        Consumer<Object> tick = RuntimeHooks::beforeTick;
        Consumer<Object> end = RuntimeHooks::afterTick;
        return new Consumer<?>[] {frame, tick, end};
    }

    public static void beforeFrame(Object minecraft) {
        if (!disabled && !sawFrame) {
            sawFrame = true;
            HitFixLog.write("Frame hook is executing");
        }
        drain(minecraft);
    }

    public static void beforeTick(Object minecraft) {
        if (!disabled && !sawTick) {
            sawTick = true;
            HitFixLog.write("Tick hook is executing");
        }
        drain(minecraft);
    }

    private static Access getAccess(Object minecraft) throws Exception {
        if (access == null) {
            access = new Access(minecraft.getClass());
            HitFixLog.write("Minecraft 1.7.10 networking methods resolved");
        }
        return access;
    }

    private static void drain(Object minecraft) {
        if (disabled) {
            return;
        }
        try {
            Access methods = getAccess(minecraft);
            if (!Boolean.TRUE.equals(methods.onMainThread.invoke(minecraft))) {
                throw new IllegalStateException("Packet processing attempted outside Minecraft thread");
            }
            if (methods.world.get(minecraft) == null || methods.player.get(minecraft) == null) {
                activeNetwork = null;
                return;
            }
            Object screen = methods.screen.get(minecraft);
            if (Boolean.TRUE.equals(methods.singleplayer.invoke(minecraft)) && screen != null
                    && Boolean.TRUE.equals(methods.pauses.invoke(screen))) {
                return;
            }
            Object handler = methods.handler.invoke(minecraft);
            if (handler == null) {
                return;
            }
            Object network = methods.network.invoke(handler);
            if (network == null || !Boolean.TRUE.equals(methods.channelOpen.invoke(network))) {
                return;
            }
            boolean pending = !((Queue<?>) methods.queue.get(network)).isEmpty();
            methods.process.invoke(network);
            if (network != activeNetwork) {
                activeNetwork = network;
                HitFixLog.write("Per-frame packet drain is active on a world connection");
            }
            if (pending && !sawPackets) {
                sawPackets = true;
                HitFixLog.write("Verified: queued inbound packets processed by an extra HitFix drain");
            }
        } catch (Throwable failure) {
            disable(failure);
        }
    }

    public static void afterTick(Object minecraft) {
        if (disabled) {
            return;
        }
        try {
            Access methods = getAccess(minecraft);
            Object handler = methods.handler.invoke(minecraft);
            if (handler == null || methods.player.get(minecraft) == null) {
                announced = null;
                return;
            }
            if (handler == announced) {
                return;
            }
            Object register = methods.customPayload.newInstance("REGISTER", "1337".getBytes(StandardCharsets.UTF_8));
            Object announcement = methods.customPayload.newInstance("1337", "HitFix/1.0".getBytes(StandardCharsets.UTF_8));
            methods.send.invoke(handler, register);
            methods.send.invoke(handler, announcement);
            announced = handler;
            HitFixLog.write("Sent original HitFix/1.0 plugin-channel announcement");
        } catch (Throwable failure) {
            disable(failure);
        }
    }

    private static void disable(Throwable failure) {
        disabled = true;
        Throwable cause = failure instanceof InvocationTargetException
            ? ((InvocationTargetException) failure).getCause() : failure;
        HitFixLog.write("DISABLED after error: " + cause);
    }

    private static final class Access {
        final Field world;
        final Field player;
        final Field screen;
        final Field queue;
        final Method handler;
        final Method singleplayer;
        final Method onMainThread;
        final Method pauses;
        final Method network;
        final Method channelOpen;
        final Method process;
        final Method send;
        final Constructor<?> customPayload;

        Access(Class<?> minecraft) throws Exception {
            world = minecraft.getField("theWorld");
            player = minecraft.getField("thePlayer");
            screen = minecraft.getField("currentScreen");
            handler = minecraft.getMethod("getNetHandler");
            singleplayer = minecraft.getMethod("isSingleplayer");
            onMainThread = minecraft.getMethod("isCallingFromMinecraftThread");
            pauses = screen.getType().getMethod("doesGuiPauseGame");
            network = handler.getReturnType().getMethod("getNetworkManager");
            channelOpen = network.getReturnType().getMethod("isChannelOpen");
            process = network.getReturnType().getMethod("processReceivedPackets");
            queue = network.getReturnType().getField("receivedPacketsQueue");
            ClassLoader loader = minecraft.getClassLoader();
            Class<?> packet = Class.forName("net.minecraft.network.Packet", false, loader);
            Class<?> payload = Class.forName("net.minecraft.network.play.client.C17PacketCustomPayload", false, loader);
            customPayload = payload.getConstructor(String.class, byte[].class);
            send = handler.getReturnType().getMethod("addToSendQueue", packet);
        }
    }
}
