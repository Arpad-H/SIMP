package com.example.simpanroidcompanion.net

import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.concurrent.Executors
import java.util.concurrent.ScheduledExecutorService
import java.util.concurrent.TimeUnit

/** A single OSC message: an address pattern plus float arguments. */
class OscMessage(val address: String, val args: FloatArray)

/**
 * Minimal OSC-over-UDP client matching the format Unity's uOSC server expects
 * (big-endian, ",fff..." type tag, 4-byte aligned strings).
 *
 * All socket I/O runs on a single background thread. [start] opens the socket and then
 * streams the latest sensor snapshot at ~60 Hz by polling the supplied snapshot provider.
 */
class OscClient {

    private val sendIntervalMs = 16L // ~60 Hz

    @Volatile private var socket: DatagramSocket? = null
    @Volatile private var address: InetAddress? = null
    @Volatile private var port: Int = 0
    private var scheduler: ScheduledExecutorService? = null

    /** True once a socket has been opened for the current connection. */
    val isConnected: Boolean get() = socket != null

    /**
     * Open the UDP socket to [ip]:[port] and begin streaming. Any previous connection is torn
     * down first. Safe to call from the main thread: the socket setup and all sends happen on
     * the scheduler thread.
     */
    fun start(ip: String, port: Int, snapshotProvider: () -> List<OscMessage>) {
        stop()
        this.port = port

        val exec = Executors.newSingleThreadScheduledExecutor()
        scheduler = exec

        exec.execute {
            try {
                address = InetAddress.getByName(ip)
                socket = DatagramSocket()
                rawSend("/hello", floatArrayOf(1f))
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }

        exec.scheduleAtFixedRate({
            try {
                if (socket != null && address != null) {
                    for (msg in snapshotProvider()) rawSend(msg.address, msg.args)
                }
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }, sendIntervalMs, sendIntervalMs, TimeUnit.MILLISECONDS)
    }

    /** Stop streaming and release the socket. */
    fun stop() {
        scheduler?.shutdownNow()
        scheduler = null
        socket?.close()
        socket = null
        address = null
    }

    /** Build and transmit one OSC message. Must run on the scheduler thread. */
    private fun rawSend(oscAddress: String, values: FloatArray) {
        val sock = socket ?: return
        val dest = address ?: return
        val data = buildOscPacket(oscAddress, values)
        sock.send(DatagramPacket(data, data.size, dest, port))
    }

    private fun buildOscPacket(oscAddress: String, values: FloatArray): ByteArray {
        val buffer = ByteBuffer.allocate(1024).order(ByteOrder.BIG_ENDIAN)

        writeOscString(buffer, oscAddress)

        // type tag string, e.g. ",fff"
        val types = StringBuilder(",")
        repeat(values.size) { types.append('f') }
        writeOscString(buffer, types.toString())

        for (v in values) buffer.putFloat(v)

        val out = ByteArray(buffer.position())
        buffer.rewind()
        buffer.get(out)
        return out
    }

    private fun writeOscString(buffer: ByteBuffer, s: String) {
        val bytes = s.toByteArray(Charsets.US_ASCII)
        buffer.put(bytes)
        buffer.put(0.toByte())

        var pad = 4 - ((bytes.size + 1) % 4)
        if (pad == 4) pad = 0
        repeat(pad) { buffer.put(0.toByte()) }
    }
}
