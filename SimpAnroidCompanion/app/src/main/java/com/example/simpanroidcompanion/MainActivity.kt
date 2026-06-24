package com.example.simpanroidcompanion

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.example.simpanroidcompanion.net.OscClient
import com.example.simpanroidcompanion.sensor.MotionSensors
import com.example.simpanroidcompanion.ui.AppRoot
import com.example.simpanroidcompanion.ui.Connection
import com.example.simpanroidcompanion.ui.theme.SimpAnroidCompanionTheme
import com.google.mlkit.vision.codescanner.GmsBarcodeScannerOptions
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning
import com.google.mlkit.vision.barcode.common.Barcode

/**
 * Single-activity Compose app. Two ways to connect, both funnelled through [tryConnectFromUri]:
 *  - the `simpconnect://connect?ip=&port=` deep link (system-camera QR scan), and
 *  - the in-app "Scan QR code" button backed by Google's ML Kit Code Scanner.
 *
 * While connected and in the foreground it registers every motion sensor and streams their latest
 * values over OSC/UDP at ~60 Hz.
 */
class MainActivity : ComponentActivity() {

    private lateinit var sensors: MotionSensors
    private val osc = OscClient()

    private var connection by mutableStateOf<Connection>(Connection.Disconnected)
    private var status by mutableStateOf<String?>(null)

    private var resumed = false
    private var streaming = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        sensors = MotionSensors(this)

        // Launched via the simpconnect:// deep link?
        intent?.data?.let { tryConnectFromUri(it) }

        setContent {
            SimpAnroidCompanionTheme {
                AppRoot(
                    sensors = sensors,
                    connection = connection,
                    status = status,
                    onScan = ::startScan,
                    onConnect = { ip, port -> connect(Connection.Connected(ip, port)) },
                    onDisconnect = ::disconnect,
                )
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        intent.data?.let { tryConnectFromUri(it) }
    }

    override fun onResume() {
        super.onResume()
        resumed = true
        if (connection is Connection.Connected) startStreaming()
    }

    override fun onPause() {
        super.onPause()
        resumed = false
        stopStreaming()
    }

    override fun onDestroy() {
        super.onDestroy()
        osc.stop()
    }

    // ---- Connection control ----

    private fun connect(target: Connection.Connected) {
        status = null
        connection = target
        if (resumed) startStreaming()
    }

    private fun disconnect() {
        stopStreaming()
        connection = Connection.Disconnected
    }

    private fun startStreaming() {
        val target = connection as? Connection.Connected ?: return
        if (streaming) return
        streaming = true
        sensors.register()
        osc.start(target.ip, target.port) { sensors.oscSnapshot() }
    }

    private fun stopStreaming() {
        if (!streaming) return
        streaming = false
        osc.stop()
        sensors.unregister()
    }

    // ---- QR scanning ----

    private fun startScan() {
        val options = GmsBarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
            .build()
        GmsBarcodeScanning.getClient(this, options)
            .startScan()
            .addOnSuccessListener { barcode ->
                val raw = barcode.rawValue
                if (raw == null || !tryConnectFromText(raw)) {
                    status = "That QR code didn't contain a SIMP address."
                }
            }
            .addOnCanceledListener { /* user dismissed the scanner */ }
            .addOnFailureListener { e ->
                status = "Scanner unavailable: ${e.localizedMessage ?: "is Google Play Services installed?"}"
            }
    }

    private fun tryConnectFromText(text: String): Boolean =
        tryConnectFromUri(runCatching { Uri.parse(text) }.getOrNull())

    /** Parse `ip` and `port` from a simpconnect:// (or any) URI and connect. Returns success. */
    private fun tryConnectFromUri(uri: Uri?): Boolean {
        if (uri == null) return false
        val ip = uri.getQueryParameter("ip") ?: return false
        val port = uri.getQueryParameter("port")?.toIntOrNull() ?: return false
        connect(Connection.Connected(ip, port))
        return true
    }
}
