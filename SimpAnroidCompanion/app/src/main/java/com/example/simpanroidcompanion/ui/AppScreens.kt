package com.example.simpanroidcompanion.ui

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.content.pm.ActivityInfo
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.animation.Crossfade
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Switch
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.font.FontWeight
import com.example.simpanroidcompanion.sensor.MotionSensors
import com.example.simpanroidcompanion.sensor.Vec3
import kotlin.math.roundToInt

/** Whether the app currently has a streaming target. */
sealed interface Connection {
    data object Disconnected : Connection
    data class Connected(val ip: String, val port: Int) : Connection
}

@Composable
fun AppRoot(
    sensors: MotionSensors,
    connection: Connection,
    status: String?,
    onScan: () -> Unit,
    onConnect: (String, Int) -> Unit,
    onDisconnect: () -> Unit,
    onCalibrate: () -> Unit,
    onTap: (Int) -> Unit,
) {
    Scaffold { padding ->
        when (connection) {
            is Connection.Disconnected ->
                ConnectScreen(Modifier.padding(padding), status, onScan, onConnect)
            is Connection.Connected ->
                ConnectedScreen(Modifier.padding(padding), sensors, connection, onDisconnect, onCalibrate, onTap)
        }
    }
}

/**
 * The connected experience. A prominent toggle swaps between the full sensor [DashboardScreen] and
 * the minimal [PlayScreen] (centre gyro + corner tap zones). Both run off the same live connection,
 * so flipping the switch is instant — nothing reconnects or restarts the stream.
 *
 * Play mode locks the device to landscape (the [DisposableEffect] below); leaving play mode or the
 * connection restores the normal orientation. The activity already declares
 * `configChanges="orientation|screenSize"`, so this re-layouts in place without dropping the stream.
 */
@Composable
private fun ConnectedScreen(
    modifier: Modifier,
    sensors: MotionSensors,
    connection: Connection.Connected,
    onDisconnect: () -> Unit,
    onCalibrate: () -> Unit,
    onTap: (Int) -> Unit,
) {
    var playMode by rememberSaveable { mutableStateOf(false) }

    val context = LocalContext.current
    DisposableEffect(playMode) {
        val activity = context.findActivity()
        activity?.requestedOrientation =
            if (playMode) ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE
            else ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED
        onDispose {
            activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED
        }
    }

    Column(modifier.fillMaxSize()) {
        ModeToggle(
            playMode = playMode,
            onModeChange = { playMode = it },
            modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp),
        )
        Crossfade(targetState = playMode, modifier = Modifier.weight(1f), label = "mode") { play ->
            if (play) {
                PlayScreen(sensors, onCalibrate, onTap)
            } else {
                DashboardScreen(Modifier, sensors, connection, onDisconnect)
            }
        }
    }
}

/**
 * A big labelled switch flipping between "Sensors" and "Play". The flanking labels are tappable too,
 * giving a generous touch target on either side of the switch.
 */
@Composable
private fun ModeToggle(
    playMode: Boolean,
    onModeChange: (Boolean) -> Unit,
    modifier: Modifier = Modifier,
) {
    Card(modifier) {
        Row(
            Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 14.dp),
            horizontalArrangement = Arrangement.Center,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            ModeLabel("Sensors", active = !playMode) { onModeChange(false) }
            Switch(
                checked = playMode,
                onCheckedChange = onModeChange,
                modifier = Modifier.scale(1.4f).padding(horizontal = 28.dp),
            )
            ModeLabel("Play", active = playMode) { onModeChange(true) }
        }
    }
}

@Composable
private fun ModeLabel(text: String, active: Boolean, onClick: () -> Unit) {
    Text(
        text,
        modifier = Modifier.clickable(onClick = onClick).padding(8.dp),
        style = MaterialTheme.typography.titleMedium,
        fontWeight = if (active) FontWeight.Bold else FontWeight.Normal,
        color = if (active) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outline,
    )
}

@Composable
private fun ConnectScreen(
    modifier: Modifier,
    status: String?,
    onScan: () -> Unit,
    onConnect: (String, Int) -> Unit,
) {
    var ip by rememberSaveable { mutableStateOf("") }
    var port by rememberSaveable { mutableStateOf("9000") }
    val portNum = port.toIntOrNull()
    val canConnect = ip.isNotBlank() && portNum != null && portNum in 1..65535

    Column(
        modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Spacer(Modifier.height(24.dp))
        Text("SIMP Companion", style = MaterialTheme.typography.headlineMedium)
        Text(
            "Connect to the SIMP app to stream your phone's motion sensors over the network.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.outline,
            textAlign = TextAlign.Center,
        )

        Button(onClick = onScan, modifier = Modifier.fillMaxWidth().height(56.dp)) {
            Text("Scan QR code")
        }

        Text("or enter the address manually", style = MaterialTheme.typography.labelMedium)

        OutlinedTextField(
            value = ip,
            onValueChange = { ip = it.trim() },
            label = { Text("IP address") },
            placeholder = { Text("192.168.1.42") },
            singleLine = true,
            modifier = Modifier.fillMaxWidth(),
        )
        OutlinedTextField(
            value = port,
            onValueChange = { port = it.filter(Char::isDigit) },
            label = { Text("Port") },
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
            modifier = Modifier.fillMaxWidth(),
        )
        OutlinedButton(
            onClick = { portNum?.let { onConnect(ip, it) } },
            enabled = canConnect,
            modifier = Modifier.fillMaxWidth(),
        ) {
            Text("Connect")
        }

        if (status != null) {
            Text(status, color = MaterialTheme.colorScheme.error, textAlign = TextAlign.Center)
        }
    }
}

@Composable
private fun DashboardScreen(
    modifier: Modifier,
    sensors: MotionSensors,
    connection: Connection.Connected,
    onDisconnect: () -> Unit,
) {
    Column(
        modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Card(Modifier.fillMaxWidth()) {
            Row(
                Modifier.fillMaxWidth().padding(16.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column {
                    Text("Streaming", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.outline)
                    Text("${connection.ip}:${connection.port}", style = MaterialTheme.typography.titleMedium)
                }
                OutlinedButton(onClick = onDisconnect) { Text("Disconnect") }
            }
        }

        // Orientation as the "dot in a circle" tilt dial (gravity, or accelerometer as fallback).
        SensorCard("Orientation", "tilt", sensors.hasGravity || sensors.hasAccel) {
            CenteredDial {
                TiltDial(if (sensors.hasGravity) sensors.gravity else sensors.accel)
            }
            val o = sensors.orientation
            Text(
                "pitch ${o.pitchDeg.roundToInt()}°   roll ${o.rollDeg.roundToInt()}°",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.outline,
            )
        }

        SensorCard("Heading", "° from north", sensors.hasRotation) {
            CenteredDial { Compass(sensors.orientation.headingDeg) }
        }

        SensorCard("Gyroscope", "rad/s · angular velocity", sensors.hasGyro) {
            Vector3Bars(sensors.gyro, range = 10f)
        }

        SensorCard("Accelerometer", "m/s² · incl. gravity", sensors.hasAccel) {
            Vector3Bars(sensors.accel, range = 20f)
        }

        SensorCard("Linear acceleration", "m/s² · gravity removed", sensors.hasLinAccel) {
            CenteredDial { MotionDot(sensors.linAccel, range = 10f) }
            Vector3Bars(sensors.linAccel, range = 10f)
        }

        SensorCard("Magnetometer", "µT", sensors.hasMag) {
            Vector3Bars(sensors.magnetic, range = 80f)
        }

        Spacer(Modifier.height(8.dp))
    }
}

/**
 * Minimal in-game interface for a phone held in landscape (play mode locks the orientation). A small
 * gyro ball sits in the centre and doubles as the Calibrate control: tapping it captures the current
 * pose as neutral — recentring the ball and (via [onCalibrate]) telling Unity to rebase all
 * subsequent input on this orientation. Two triangular tap zones in the bottom corners fire [onTap]
 * with the zone that was hit (0 = left, 1 = right), each sending a one-off `/tap` to Unity.
 */
@Composable
private fun PlayScreen(sensors: MotionSensors, onCalibrate: () -> Unit, onTap: (Int) -> Unit) {
    val haptics = LocalHapticFeedback.current
    var neutral by remember { mutableStateOf(Vec3.ZERO) }
    val source = if (sensors.hasGravity) sensors.gravity else sensors.accel

    val calibrate: () -> Unit = {
        neutral = source
        onCalibrate()
        haptics.performHapticFeedback(HapticFeedbackType.LongPress)
    }

    BoxWithConstraints(Modifier.fillMaxSize().padding(16.dp)) {
        val ball = minOf(maxWidth, maxHeight) * 0.42f

        // Tap zones hug the two bottom corners; only taps inside the triangle count.
        CornerTapZone(
            left = true,
            onTap = { onTap(0) },
            modifier = Modifier.align(Alignment.BottomStart).fillMaxWidth(0.42f).fillMaxHeight(0.55f),
        )
        CornerTapZone(
            left = false,
            onTap = { onTap(1) },
            modifier = Modifier.align(Alignment.BottomEnd).fillMaxWidth(0.42f).fillMaxHeight(0.55f),
        )

        // Instructions across the top.
        Text(
            "Tilt the phone to steer · tap a bottom corner to act",
            style = MaterialTheme.typography.titleSmall,
            color = MaterialTheme.colorScheme.outline,
            textAlign = TextAlign.Center,
            modifier = Modifier.align(Alignment.TopCenter).fillMaxWidth(),
        )

        // Centre gyro ball doubles as the Calibrate control.
        Column(
            Modifier.align(Alignment.Center),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            GyroBall(
                source,
                neutral,
                Modifier
                    .size(ball)
                    .pointerInput(Unit) { detectTapGestures { calibrate() } },
            )
            Spacer(Modifier.height(12.dp))
            Text(
                "Tap the gyro to calibrate",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.outline,
                textAlign = TextAlign.Center,
            )
        }
    }
}

/**
 * A triangular tap target hugging one bottom corner of the play screen ([left] = bottom-left, else
 * bottom-right). The wedge fills the corner half of the composable's bounds, and only taps that land
 * inside that triangle fire [onTap] (with a light haptic tick), so the centre stays free for the gyro.
 */
@Composable
private fun CornerTapZone(left: Boolean, onTap: () -> Unit, modifier: Modifier = Modifier) {
    val haptics = LocalHapticFeedback.current
    val fill = MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)
    val edge = MaterialTheme.colorScheme.primary.copy(alpha = 0.45f)
    Box(
        modifier.pointerInput(left) {
            detectTapGestures { offset ->
                val nx = offset.x / size.width
                val ny = offset.y / size.height
                // Lower-left triangle: ny >= nx; lower-right: ny >= 1 - nx.
                val inside = if (left) ny >= nx else ny >= 1f - nx
                if (inside) {
                    onTap()
                    haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                }
            }
        },
        contentAlignment = if (left) Alignment.BottomStart else Alignment.BottomEnd,
    ) {
        Canvas(Modifier.fillMaxSize()) {
            val path = Path().apply {
                if (left) {
                    moveTo(0f, 0f)
                    lineTo(0f, size.height)
                    lineTo(size.width, size.height)
                } else {
                    moveTo(size.width, 0f)
                    lineTo(size.width, size.height)
                    lineTo(0f, size.height)
                }
                close()
            }
            drawPath(path, fill)
            drawPath(path, edge, style = Stroke(width = 2.dp.toPx()))
        }
        Text(
            "TAP",
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(18.dp),
        )
    }
}

/** Centres a square dial at ~70% width within a card. */
@Composable
private fun CenteredDial(content: @Composable () -> Unit) {
    Box(Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
        Box(Modifier.fillMaxWidth(0.7f)) { content() }
    }
}

/** Walk the context wrappers to find the hosting [Activity], for play-mode orientation locking. */
private fun Context.findActivity(): Activity? {
    var ctx: Context = this
    while (ctx is ContextWrapper) {
        if (ctx is Activity) return ctx
        ctx = ctx.baseContext
    }
    return null
}
