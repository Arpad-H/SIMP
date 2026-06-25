package com.example.simpanroidcompanion.sensor

import android.content.Context
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.example.simpanroidcompanion.net.OscMessage

/** A 3-axis sensor reading (x, y, z). */
data class Vec3(val x: Float, val y: Float, val z: Float) {
    companion object { val ZERO = Vec3(0f, 0f, 0f) }
}

/** Device orientation in degrees, derived from the rotation vector. */
data class Orientation(val pitchDeg: Float, val rollDeg: Float, val headingDeg: Float)

/**
 * Captures every motion-in-3D-space sensor the device exposes and keeps the latest values in two
 * forms:
 *  - Compose [androidx.compose.runtime.State] fields for the live UI (written on the main thread
 *    from sensor callbacks, so only the visualization reading a given field recomposes).
 *  - `@Volatile` snapshots consumed off the main thread by the OSC sender via [oscSnapshot].
 *
 * Sensors absent on the device are simply never registered; their `has*` flag is false and they
 * are omitted from [oscSnapshot].
 */
class MotionSensors(context: Context) : SensorEventListener {

    private val sensorManager =
        context.getSystemService(Context.SENSOR_SERVICE) as SensorManager

    private val rotationSensor = sensorManager.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR)
    private val gyroSensor = sensorManager.getDefaultSensor(Sensor.TYPE_GYROSCOPE)
    private val accelSensor = sensorManager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER)
    private val linAccelSensor = sensorManager.getDefaultSensor(Sensor.TYPE_LINEAR_ACCELERATION)
    private val gravitySensor = sensorManager.getDefaultSensor(Sensor.TYPE_GRAVITY)
    private val magSensor = sensorManager.getDefaultSensor(Sensor.TYPE_MAGNETIC_FIELD)

    // ---- Availability (for the UI) ----
    val hasRotation = rotationSensor != null
    val hasGyro = gyroSensor != null
    val hasAccel = accelSensor != null
    val hasLinAccel = linAccelSensor != null
    val hasGravity = gravitySensor != null
    val hasMag = magSensor != null

    // ---- Live readings for the UI (written on the main thread) ----
    var quaternion by mutableStateOf(floatArrayOf(0f, 0f, 0f, 1f)) // x, y, z, w
        private set
    var orientation by mutableStateOf(Orientation(0f, 0f, 0f))
        private set
    var gyro by mutableStateOf(Vec3.ZERO)
        private set
    var accel by mutableStateOf(Vec3.ZERO)
        private set
    var linAccel by mutableStateOf(Vec3.ZERO)
        private set
    var gravity by mutableStateOf(Vec3.ZERO)
        private set
    var magnetic by mutableStateOf(Vec3.ZERO)
        private set

    // ---- Snapshots read by the OSC sender thread ----
    @Volatile private var snapAttitude: FloatArray? = null
    @Volatile private var snapGyro: Vec3? = null
    @Volatile private var snapAccel: Vec3? = null
    @Volatile private var snapLinAccel: Vec3? = null
    @Volatile private var snapGravity: Vec3? = null
    @Volatile private var snapMag: Vec3? = null

    private var registered = false

    // Reused scratch buffers (rotation-vector maths runs on the main thread only).
    private val rotationMatrix = FloatArray(9)
    private val orientationTmp = FloatArray(3)
    private val quatTmp = FloatArray(4)

    fun register() {
        if (registered) return
        registered = true
        val rate = SensorManager.SENSOR_DELAY_GAME
        rotationSensor?.let { sensorManager.registerListener(this, it, rate) }
        gyroSensor?.let { sensorManager.registerListener(this, it, rate) }
        accelSensor?.let { sensorManager.registerListener(this, it, rate) }
        linAccelSensor?.let { sensorManager.registerListener(this, it, rate) }
        gravitySensor?.let { sensorManager.registerListener(this, it, rate) }
        magSensor?.let { sensorManager.registerListener(this, it, rate) }
    }

    fun unregister() {
        if (!registered) return
        registered = false
        sensorManager.unregisterListener(this)
    }

    override fun onSensorChanged(event: SensorEvent) {
        when (event.sensor.type) {
            Sensor.TYPE_ROTATION_VECTOR -> {
                // getQuaternionFromVector returns [w, x, y, z]; we store/send [x, y, z, w].
                SensorManager.getQuaternionFromVector(quatTmp, event.values)
                val q = floatArrayOf(quatTmp[1], quatTmp[2], quatTmp[3], quatTmp[0])
                quaternion = q
                snapAttitude = q

                SensorManager.getRotationMatrixFromVector(rotationMatrix, event.values)
                SensorManager.getOrientation(rotationMatrix, orientationTmp)
                val heading = (Math.toDegrees(orientationTmp[0].toDouble()).toFloat() + 360f) % 360f
                val pitch = Math.toDegrees(orientationTmp[1].toDouble()).toFloat()
                val roll = Math.toDegrees(orientationTmp[2].toDouble()).toFloat()
                orientation = Orientation(pitch, roll, heading)
            }
            Sensor.TYPE_GYROSCOPE -> event.toVec3().let { gyro = it; snapGyro = it }
            Sensor.TYPE_ACCELEROMETER -> event.toVec3().let { accel = it; snapAccel = it }
            Sensor.TYPE_LINEAR_ACCELERATION -> event.toVec3().let { linAccel = it; snapLinAccel = it }
            Sensor.TYPE_GRAVITY -> event.toVec3().let { gravity = it; snapGravity = it }
            Sensor.TYPE_MAGNETIC_FIELD -> event.toVec3().let { magnetic = it; snapMag = it }
        }
    }

    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) { /* not used */ }

    /** Latest values for every available sensor, as OSC messages (safe to call off-thread). */
    fun oscSnapshot(): List<OscMessage> {
        val msgs = ArrayList<OscMessage>(6)
        snapAttitude?.let { msgs.add(OscMessage("/attitude", it)) }
        snapGyro?.let { msgs.add(OscMessage("/gyro", it.toArray())) }
        snapAccel?.let { msgs.add(OscMessage("/accel", it.toArray())) }
        snapLinAccel?.let { msgs.add(OscMessage("/linaccel", it.toArray())) }
        snapGravity?.let { msgs.add(OscMessage("/gravity", it.toArray())) }
        snapMag?.let { msgs.add(OscMessage("/magnetometer", it.toArray())) }
        return msgs
    }

    /**
     * A one-off `/calibrate` message carrying the current attitude quaternion ([x, y, z, w]).
     * Unity should treat this orientation as the new neutral/rest pose and express all subsequent
     * `/attitude` input relative to it. Safe to read on the main thread (button handler).
     */
    fun calibrationMessage(): OscMessage = OscMessage("/calibrate", quaternion)

    private fun SensorEvent.toVec3() = Vec3(values[0], values[1], values[2])
    private fun Vec3.toArray() = floatArrayOf(x, y, z)
}
