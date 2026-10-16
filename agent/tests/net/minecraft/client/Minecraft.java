package net.minecraft.client;

import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.List;
import java.util.Queue;
import net.minecraft.network.Packet;

public class Minecraft {
    public Object theWorld;
    public Object thePlayer;
    public Screen currentScreen;
    public Handler handler;
    public boolean singleplayer;
    public boolean mainThread = true;
    public boolean tickSawEmptyQueue;

    public void runGameLoop() {
    }

    public void runTick() {
        tickSawEmptyQueue = handler == null || handler.network.receivedPacketsQueue.isEmpty();
    }

    public Handler getNetHandler() { return handler; }
    public boolean isSingleplayer() { return singleplayer; }
    public boolean isCallingFromMinecraftThread() { return mainThread; }

    public static final class Screen {
        public boolean pauses;
        public boolean doesGuiPauseGame() { return pauses; }
    }

    public static final class Handler {
        public Network network = new Network();
        public List<Packet> sent = new ArrayList<Packet>();
        public Network getNetworkManager() { return network; }
        public void addToSendQueue(Packet packet) { sent.add(packet); }
    }

    public static final class Network {
        public Queue<Object> receivedPacketsQueue = new ArrayDeque<Object>();
        public int calls;
        public boolean open = true;
        public boolean isChannelOpen() { return open; }
        public void processReceivedPackets() {
            calls++;
            receivedPacketsQueue.clear();
        }
    }
}
