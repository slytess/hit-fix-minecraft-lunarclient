package dev.hitfix.lunar;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import net.minecraft.client.Minecraft;
import net.minecraft.network.play.client.C17PacketCustomPayload;
import org.objectweb.asm.ClassReader;
import org.objectweb.asm.ClassVisitor;
import org.objectweb.asm.MethodVisitor;
import org.objectweb.asm.Opcodes;

public final class IntegrationTest {
    private static void require(boolean condition, String message) {
        if (!condition) {
            throw new AssertionError(message);
        }
    }

    public static void main(String[] arguments) throws Exception {
        Minecraft minecraft = new Minecraft();
        minecraft.runGameLoop();
        minecraft.handler = new Minecraft.Handler();
        minecraft.runGameLoop();
        require(minecraft.handler.network.calls == 0, "No drain outside a world");
        minecraft.theWorld = new Object();
        minecraft.thePlayer = new Object();
        minecraft.handler.network.receivedPacketsQueue.add(new Object());
        minecraft.runGameLoop();
        require(minecraft.handler.network.calls == 1, "Frame processes pending packets");
        require(minecraft.handler.network.receivedPacketsQueue.isEmpty(), "Frame emptied queue");
        minecraft.handler.network.receivedPacketsQueue.add(new Object());
        minecraft.runTick();
        require(minecraft.tickSawEmptyQueue, "Packets processed before tick physics");
        require(minecraft.handler.sent.size() == 2, "Original two announcement packets sent");
        C17PacketCustomPayload register = (C17PacketCustomPayload) minecraft.handler.sent.get(0);
        C17PacketCustomPayload announcement = (C17PacketCustomPayload) minecraft.handler.sent.get(1);
        require(register.channel.equals("REGISTER"), "REGISTER channel unchanged");
        require(new String(register.payload, StandardCharsets.UTF_8).equals("1337"), "Registered original channel");
        require(announcement.channel.equals("1337"), "Announcement channel unchanged");
        require(new String(announcement.payload, StandardCharsets.UTF_8).equals("HitFix/1.0"), "Original announcement unchanged");
        minecraft.runTick();
        require(minecraft.handler.sent.size() == 2, "Announcement is once per connection");
        minecraft.singleplayer = true;
        minecraft.currentScreen = new Minecraft.Screen();
        minecraft.currentScreen.pauses = true;
        int previous = minecraft.handler.network.calls;
        minecraft.runGameLoop();
        require(minecraft.handler.network.calls == previous, "Paused singleplayer does not drain");
        minecraft.singleplayer = false;
        minecraft.runGameLoop();
        require(minecraft.handler.network.calls == previous + 1, "Multiplayer menu still drains");
        minecraft.handler.network.open = false;
        previous = minecraft.handler.network.calls;
        minecraft.runGameLoop();
        require(minecraft.handler.network.calls == previous, "Closed channel does not drain");
        minecraft.handler = null;
        minecraft.runGameLoop();
        minecraft.runTick();
        minecraft.handler = new Minecraft.Handler();
        minecraft.runTick();
        require(minecraft.handler.sent.size() == 2, "Reconnect announces again");
        minecraft.mainThread = false;
        previous = minecraft.handler.network.calls;
        minecraft.runGameLoop();
        require(minecraft.handler.network.calls == previous, "Off-thread processing fails closed");
        minecraft.mainThread = true;
        minecraft.runGameLoop();
        require(minecraft.handler.network.calls == previous, "Failure disables further processing");
        if (arguments.length != 0) {
            verifyInstalledClass(arguments[0]);
        }
        System.out.println("PASS: frame/tick ordering, pause, disconnect, reconnect, payloads and thread guard");
    }

    private static void verifyInstalledClass(String path) throws Exception {
        byte[] original = Files.readAllBytes(Paths.get(path));
        HitFixAgent.MinecraftTransformer transformer = new HitFixAgent.MinecraftTransformer();
        byte[] transformed = transformer.transform(null, "net/minecraft/client/Minecraft", null, null, original);
        require(transformed != null, "Installed Lunar Minecraft class is compatible");
        final int[] counts = new int[3];
        new ClassReader(transformed).accept(new ClassVisitor(Opcodes.ASM9) {
            public MethodVisitor visitMethod(int access, String name, String descriptor,
                                             String signature, String[] exceptions) {
                return new MethodVisitor(Opcodes.ASM9) {
                    public void visitFieldInsn(int opcode, String owner, String field, String descriptor) {
                        if (opcode == Opcodes.GETSTATIC && owner.equals("net/minecraft/client/Minecraft")) {
                            if (field.equals("hitfix$frameHook")) { counts[0]++; }
                            if (field.equals("hitfix$tickHook")) { counts[1]++; }
                            if (field.equals("hitfix$endHook")) { counts[2]++; }
                        }
                    }
                };
            }
        }, 0);
        require(counts[0] == 1 && counts[1] == 1 && counts[2] >= 1, "Hooks installed in actual Lunar bytecode");
        require(transformer.transform(null, "net/minecraft/client/Minecraft", null, null, transformed) == null,
            "Duplicate installation refused");
        System.out.println("PASS: installed Lunar Minecraft class transforms and duplicate hooks are rejected");
    }
}
