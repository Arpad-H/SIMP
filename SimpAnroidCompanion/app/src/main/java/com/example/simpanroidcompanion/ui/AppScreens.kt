package com.example.simpanroidcompanion.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
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
import com.example.simpanroidcompanion.sensor.MotionSensors
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
) {
    Scaffold { padding ->
        when (connection) {
            is Connection.Disconnected ->
                ConnectScreen(Modifier.padding(padding), status, onScan, onConnect)
            is Connection.Connected ->
                DashboardScreen(Modifier.padding(padding), sensors, connection, onDisconnect)
        }
    }
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

/** Centres a square dial at ~70% width within a card. */
@Composable
private fun CenteredDial(content: @Composable () -> Unit) {
    Box(Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
        Box(Modifier.fillMaxWidth(0.7f)) { content() }
    }
}
