package com.example.simpanroidcompanion;

import android.content.Context;
import android.hardware.Sensor;
import android.hardware.SensorEvent;
import android.hardware.SensorEventListener;
import android.hardware.SensorManager;
import android.net.Uri;
import android.os.Bundle;

import androidx.appcompat.app.AppCompatActivity;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;

public class MainActivity extends AppCompatActivity implements SensorEventListener
{
    private final java.util.concurrent.ExecutorService networkExecutor =
            java.util.concurrent.Executors.newSingleThreadExecutor();
    // Network
    private DatagramSocket socket;
    private InetAddress address;
    private int port;

    // Sensor
    private SensorManager sensorManager;
    private Sensor rotationSensor;

    // Target
    private String ip;

    // Rate limit (~60Hz)
    private long lastSendTime = 0;
    private static final long SEND_INTERVAL_NS = 16_000_000;

    @Override
    protected void onCreate(Bundle savedInstanceState)
    {
        super.onCreate(savedInstanceState);

        handleDeepLink(getIntent().getData());

        setupUDP();
        setupSensors();
        sendHello();
    }

    private void handleDeepLink(Uri data)
    {
        if (data == null) return;

        ip = data.getQueryParameter("ip");
        String portStr = data.getQueryParameter("port");

        if (ip == null || portStr == null) return;

        port = Integer.parseInt(portStr);
    }

    private void setupUDP()
    {
        try
        {
            address = InetAddress.getByName(ip);
            socket = new DatagramSocket();
        }
        catch (Exception e)
        {
            e.printStackTrace();
        }
    }

    private void setupSensors()
    {
        sensorManager = (SensorManager) getSystemService(Context.SENSOR_SERVICE);

        rotationSensor =
                sensorManager.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR);
    }

    @Override
    protected void onResume()
    {
        super.onResume();

        sensorManager.registerListener(
                this,
                rotationSensor,
                SensorManager.SENSOR_DELAY_GAME
        );
    }

    @Override
    protected void onPause()
    {
        super.onPause();
        sensorManager.unregisterListener(this);
    }

    @Override
    public void onSensorChanged(SensorEvent event)
    {
        if (event.sensor.getType() != Sensor.TYPE_ROTATION_VECTOR)
            return;

        long now = System.nanoTime();
        if (now - lastSendTime < SEND_INTERVAL_NS)
            return;

        lastSendTime = now;

        float[] q = new float[4];
        SensorManager.getQuaternionFromVector(q, event.values);

        float w = q[0];
        float x = q[1];
        float y = q[2];
        float z = q[3];

        sendAttitude(x, y, z, w);
    }

    private void sendHello()
    {
        sendOscMessage("/hello", new float[]{1});
    }

    private void sendAttitude(float x, float y, float z, float w)
    {
        sendOscMessage("/attitude", new float[]{x, y, z, w});
    }

    private void sendOscMessage(String address, float[] values)
    {
        if (socket == null || this.address == null)
            return;

        try
        {
            byte[] data = buildOscPacket(address, values);

            DatagramPacket packet = new DatagramPacket(
                    data,
                    data.length,
                    this.address,
                    port
            );

            networkExecutor.execute(() -> {
                try
                {
                    socket.send(packet);
                }
                catch (Exception e)
                {
                    e.printStackTrace();
                }
            });
        }
        catch (Exception e)
        {
            e.printStackTrace();
        }
    }

    private byte[] buildOscPacket(String address, float[] values)
    {
        ByteBuffer buffer = ByteBuffer.allocate(1024);
        buffer.order(ByteOrder.BIG_ENDIAN);

        writeOscString(buffer, address);

        // type tag string
        StringBuilder types = new StringBuilder(",");
        for (int i = 0; i < values.length; i++)
            types.append("f");

        writeOscString(buffer, types.toString());

        for (float v : values)
            buffer.putFloat(v);

        int size = buffer.position();
        byte[] out = new byte[size];

        buffer.rewind();
        buffer.get(out);

        return out;
    }

    private void writeOscString(ByteBuffer buffer, String s)
    {
        byte[] bytes = s.getBytes();

        buffer.put(bytes);
        buffer.put((byte) 0);

        int pad = 4 - ((bytes.length + 1) % 4);
        if (pad == 4) pad = 0;

        for (int i = 0; i < pad; i++)
            buffer.put((byte) 0);
    }

    @Override
    public void onAccuracyChanged(Sensor sensor, int accuracy)
    {
        // not used
    }
}