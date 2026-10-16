package net.minecraft.network.play.client;

import net.minecraft.network.Packet;

public class C17PacketCustomPayload extends Packet {
    public final String channel;
    public final byte[] payload;

    public C17PacketCustomPayload(String channel, byte[] payload) {
        this.channel = channel;
        this.payload = payload;
    }
}
