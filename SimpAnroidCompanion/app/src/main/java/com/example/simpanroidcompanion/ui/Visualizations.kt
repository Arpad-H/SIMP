package com.example.simpanroidcompanion.ui

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.example.simpanroidcompanion.sensor.Vec3
import kotlin.math.max
import kotlin.math.min
import kotlin.math.roundToInt

// Per-axis colours, reused across every visualization.
private val AxisX = Color(0xFFE53935) // red
private val AxisY = Color(0xFF43A047) // green
private val AxisZ = Color(0xFF1E88E5) // blue

private fun fmt(v: Float) = String.format("%+.2f", v)

/** Card shell shared by every sensor tile: title, units subtitle, and an "unavailable" state. */
@Composable
fun SensorCard(
    title: String,
    units: String,
    available: Boolean,
    content: @Composable () -> Unit,
) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Row(
                Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.Bottom,
            ) {
                Text(title, style = MaterialTheme.typography.titleMedium)
                Text(units, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.outline)
            }
            if (available) {
                content()
            } else {
                Text(
                    "Sensor not available on this device",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.error,
                )
            }
        }
    }
}

/**
 * The "dot in a circle" tilt indicator. The dot is the gravity vector projected onto the screen
 * plane, so it slides toward whichever edge of the phone is lower (like a ball on a tray).
 */
@Composable
fun TiltDial(gravity: Vec3, modifier: Modifier = Modifier) {
    val grid = MaterialTheme.colorScheme.surfaceVariant
    val dotColor = MaterialTheme.colorScheme.primary
    val g = 9.81f
    Canvas(modifier.aspectRatio(1f)) {
        val c = Offset(size.width / 2f, size.height / 2f)
        val radius = min(size.width, size.height) / 2f - 6.dp.toPx()

        drawCircle(grid, radius, c, style = Stroke(2.dp.toPx()))
        drawCircle(grid, radius * 0.66f, c, style = Stroke(1.dp.toPx()))
        drawCircle(grid, radius * 0.33f, c, style = Stroke(1.dp.toPx()))
        drawLine(grid, Offset(c.x - radius, c.y), Offset(c.x + radius, c.y), 1.dp.toPx())
        drawLine(grid, Offset(c.x, c.y - radius), Offset(c.x, c.y + radius), 1.dp.toPx())

        val nx = (gravity.x / g).coerceIn(-1f, 1f)
        val ny = (gravity.y / g).coerceIn(-1f, 1f)
        drawCircle(dotColor, 9.dp.toPx(), Offset(c.x + nx * radius, c.y + ny * radius))
    }
}

/**
 * The large tilt "ball" for the play screen. Like [TiltDial] but drawn relative to a captured
 * [neutral] pose: right after calibration the ball sits dead-centre (on the ring marker) and rolls
 * toward whichever way you tilt away from that pose. Both [source] and [neutral] are gravity
 * vectors in m/s²; before the first calibration [neutral] is zero, so it shows absolute tilt.
 */
@Composable
fun GyroBall(source: Vec3, neutral: Vec3, modifier: Modifier = Modifier) {
    val grid = MaterialTheme.colorScheme.surfaceVariant
    val centerMark = MaterialTheme.colorScheme.outline
    val dotColor = MaterialTheme.colorScheme.primary
    val g = 9.81f
    Canvas(modifier.aspectRatio(1f)) {
        val c = Offset(size.width / 2f, size.height / 2f)
        val radius = min(size.width, size.height) / 2f - 8.dp.toPx()

        drawCircle(grid, radius, c, style = Stroke(2.dp.toPx()))
        drawCircle(grid, radius * 0.66f, c, style = Stroke(1.dp.toPx()))
        drawCircle(grid, radius * 0.33f, c, style = Stroke(1.dp.toPx()))
        drawLine(grid, Offset(c.x - radius, c.y), Offset(c.x + radius, c.y), 1.dp.toPx())
        drawLine(grid, Offset(c.x, c.y - radius), Offset(c.x, c.y + radius), 1.dp.toPx())

        // Ring marking the calibrated neutral, where the ball rests once you hold that pose.
        drawCircle(centerMark, 6.dp.toPx(), c, style = Stroke(1.5.dp.toPx()))

        val nx = ((source.x - neutral.x) / g).coerceIn(-1f, 1f)
        val ny = ((source.y - neutral.y) / g).coerceIn(-1f, 1f)
        drawCircle(dotColor, 13.dp.toPx(), Offset(c.x + nx * radius, c.y + ny * radius))
    }
}

/** Three signed, centre-anchored bars (X red / Y green / Z blue) with numeric readouts. */
@Composable
fun Vector3Bars(v: Vec3, range: Float, modifier: Modifier = Modifier) {
    Column(modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        AxisRow("X", v.x, range, AxisX)
        AxisRow("Y", v.y, range, AxisY)
        AxisRow("Z", v.z, range, AxisZ)
    }
}

@Composable
private fun AxisRow(label: String, value: Float, range: Float, color: Color) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        Text(label, modifier = Modifier.width(16.dp), color = color, fontWeight = FontWeight.Bold)
        SignedBar(value, range, color, Modifier.weight(1f).height(16.dp))
        Text(
            fmt(value),
            modifier = Modifier.width(72.dp),
            textAlign = TextAlign.End,
            fontFamily = FontFamily.Monospace,
            style = MaterialTheme.typography.bodyMedium,
        )
    }
}

@Composable
private fun SignedBar(value: Float, range: Float, color: Color, modifier: Modifier) {
    val track = MaterialTheme.colorScheme.surfaceVariant
    val tick = MaterialTheme.colorScheme.outline
    Canvas(modifier) {
        val r = size.height / 2f
        val cx = size.width / 2f
        drawRoundRect(track, cornerRadius = CornerRadius(r, r))
        drawLine(tick, Offset(cx, 0f), Offset(cx, size.height), 1.dp.toPx())

        val frac = (value / range).coerceIn(-1f, 1f)
        val end = cx + frac * (size.width / 2f)
        val left = min(cx, end)
        drawRoundRect(
            color = color,
            topLeft = Offset(left, 0f),
            size = Size(max(2f, kotlin.math.abs(end - cx)), size.height),
            cornerRadius = CornerRadius(r, r),
        )
    }
}

/** A 2D dot showing lateral (x/y) movement as a vector from the centre. */
@Composable
fun MotionDot(v: Vec3, range: Float, modifier: Modifier = Modifier) {
    val grid = MaterialTheme.colorScheme.surfaceVariant
    val dotColor = MaterialTheme.colorScheme.tertiary
    Canvas(modifier.aspectRatio(1f)) {
        val c = Offset(size.width / 2f, size.height / 2f)
        val radius = min(size.width, size.height) / 2f - 6.dp.toPx()

        drawCircle(grid, radius, c, style = Stroke(2.dp.toPx()))
        drawLine(grid, Offset(c.x - radius, c.y), Offset(c.x + radius, c.y), 1.dp.toPx())
        drawLine(grid, Offset(c.x, c.y - radius), Offset(c.x, c.y + radius), 1.dp.toPx())

        val nx = (v.x / range).coerceIn(-1f, 1f)
        val ny = (v.y / range).coerceIn(-1f, 1f)
        val dot = Offset(c.x + nx * radius, c.y - ny * radius) // screen y is inverted
        drawLine(dotColor, c, dot, 3.dp.toPx())
        drawCircle(dotColor, 8.dp.toPx(), dot)
    }
}

/** Compass: a fixed top pointer (where the phone faces), a rotating ring with a red north mark,
 *  and the numeric heading in the centre. */
@Composable
fun Compass(headingDeg: Float, modifier: Modifier = Modifier) {
    val grid = MaterialTheme.colorScheme.surfaceVariant
    val north = Color(0xFFE53935)
    val pointer = MaterialTheme.colorScheme.primary
    Box(modifier.aspectRatio(1f), contentAlignment = Alignment.Center) {
        Canvas(Modifier.fillMaxSize()) {
            val c = Offset(size.width / 2f, size.height / 2f)
            val radius = min(size.width, size.height) / 2f - 6.dp.toPx()
            drawCircle(grid, radius, c, style = Stroke(2.dp.toPx()))

            // Fixed pointer at the very top = direction the phone is facing.
            val ptr = Path().apply {
                moveTo(c.x, c.y - radius - 2.dp.toPx())
                lineTo(c.x - 7.dp.toPx(), c.y - radius + 12.dp.toPx())
                lineTo(c.x + 7.dp.toPx(), c.y - radius + 12.dp.toPx())
                close()
            }
            drawPath(ptr, pointer)

            // Rotating card: ticks every 30°, plus a red wedge marking true north.
            rotate(-headingDeg, c) {
                for (i in 0 until 12) {
                    val long = i % 3 == 0
                    rotate(i * 30f, c) {
                        drawLine(
                            grid,
                            Offset(c.x, c.y - radius),
                            Offset(c.x, c.y - radius + if (long) 16.dp.toPx() else 9.dp.toPx()),
                            2.dp.toPx(),
                        )
                    }
                }
                val needle = Path().apply {
                    moveTo(c.x, c.y - radius * 0.7f)
                    lineTo(c.x - 8.dp.toPx(), c.y)
                    lineTo(c.x + 8.dp.toPx(), c.y)
                    close()
                }
                drawPath(needle, north)
            }
        }
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Text("${headingDeg.roundToInt()}°", style = MaterialTheme.typography.headlineSmall)
            Text(cardinal(headingDeg), style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.outline)
        }
    }
}

private fun cardinal(deg: Float): String {
    val dirs = arrayOf("N", "NE", "E", "SE", "S", "SW", "W", "NW")
    val idx = ((((deg % 360f) + 360f) % 360f) / 45f).roundToInt() % 8
    return dirs[idx]
}
