package de.portalgremlins;

import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.net.Uri;
import android.nfc.NfcAdapter;
import android.nfc.Tag;
import android.nfc.tech.Ndef;
import android.os.Bundle;
import android.util.Log;
import android.widget.Button;
import android.widget.EditText;
import android.widget.GridLayout;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;

import com.google.mlkit.vision.barcode.common.Barcode;
import com.google.mlkit.vision.codescanner.GmsBarcodeScannerOptions;
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning;

import java.nio.charset.Charset;
import java.util.ArrayList;
import java.util.List;

import de.portalgremlins.game.Resonance;
import de.portalgremlins.network.connection.GameConnectionService;
import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.Response;
import okhttp3.WebSocket;
import okhttp3.WebSocketListener;

@Deprecated
public class OldMenu extends AppCompatActivity implements NfcAdapter.ReaderCallback {

    private static final String PREFS_NAME = "UserPrefs";
    private static final String KEY_USERNAME = "username";
    private static final String TAG = "WebSocketClient";
    private final GameConnectionService gameConnectionService = new GameConnectionService();

    private String username;
    private WebSocket webSocket;

    private TextView textView;
    private Button scanButton;
    private NfcAdapter nfcAdapter;

    // --- Select Element Methods ---
    private final List<String> selectedElements = new ArrayList<>();
    private final int maxSelection = 3;

    private GridLayout grid;
    private TextView statusText;
    private TextView displayNameText;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        textView = findViewById(R.id.textView);
        scanButton = findViewById(R.id.sendButton);
        grid = findViewById(R.id.elementGrid);
        statusText = findViewById(R.id.selectedElementsText);
        displayNameText = findViewById(R.id.displayNameText);

        Button editButton = findViewById(R.id.editNameButton);

        // Initialize NFC
        nfcAdapter = NfcAdapter.getDefaultAdapter(this);

        if (nfcAdapter == null) {
            textView.setText("NFC is not supported on this device.");
        }

        // Handle deep link only once
        if (savedInstanceState == null) {
            Uri uri = getIntent().getData();
            if (uri != null) {
                processUri(uri);
            }
        }

        grid.removeAllViews();
        setupElementGrid();

        // QR Scan button
        scanButton.setOnClickListener(v -> startQrScanner());



        // Load saved username
        Context context = this;
        String savedName = context
                .getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
                .getString(KEY_USERNAME, "Guest");

        displayNameText.setText("User: " + savedName);

        // Edit username button
        editButton.setOnClickListener(v -> showEditNameDialog());
    }

    private void setupElementGrid() {

        for (Resonance value : Resonance.values()) {
            Button button = new Button(this);
            button.setText(value.name());
            button.setTextColor(Color.parseColor(value.getDominantColorCode()));
            button.setBackgroundColor(Color.parseColor("#dbcece"));

            button.setOnClickListener(v -> {
                Toast.makeText(this,
                        "Gewählt: " + value.name(),
                        Toast.LENGTH_SHORT).show();
            });

            GridLayout.LayoutParams params = new GridLayout.LayoutParams();

            params.width = 0;
            params.height = GridLayout.LayoutParams.WRAP_CONTENT;
            params.topMargin = 20;
            params.leftMargin = 20;
            params.rightMargin = 20;
            params.columnSpec = GridLayout.spec(GridLayout.UNDEFINED, 1f);

            button.setLayoutParams(params);

            // Button hinzufügen
            grid.addView(button);
        }
    }

    private void showEditNameDialog() {

        AlertDialog.Builder builder = new AlertDialog.Builder(this);

        builder.setTitle("Enter Username");

        EditText input = new EditText(this);
        input.setHint("Name");

        String currentName = displayNameText
                .getText()
                .toString()
                .replace("User: ", "");

        input.setText(currentName);

        builder.setView(input);

        builder.setPositiveButton("Confirm", (dialog, which) -> {
            String newName = input.getText().toString();
            saveName(newName);
        });

        builder.setNegativeButton("Cancel", (dialog, which) -> dialog.cancel());

        builder.show();
    }

    private void saveName(String name) {

        displayNameText.setText("User: " + name);

        getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
                .edit()
                .putString(KEY_USERNAME, name)
                .apply();
    }

    private void handleElementClick(Button button, TextView statusText) {

        String element = button.getText().toString();

        if (button.isActivated()) {

            // Deselect
            button.setActivated(false);
            selectedElements.remove(element);

        } else {

            // Select
            if (selectedElements.size() < maxSelection) {

                button.setActivated(true);
                selectedElements.add(element);

            } else {

                Toast.makeText(
                        this,
                        "Maximum 3 elements allowed",
                        Toast.LENGTH_SHORT
                ).show();
            }
        }

        // Update status text
        if (selectedElements.isEmpty()) {
            statusText.setText("Selected: None");
        } else {
            statusText.setText(
                    "Selected: " + String.join(", ", selectedElements)
            );
        }

        sendElementsToUnity();
    }

    private void sendElementsToUnity() {

        gameConnectionService.getGameConnection().protocol().selectElements();

        if (webSocket != null) {

            String message =
                    "SELECT_ELEMENTS:" +
                            String.join(",", selectedElements);

            boolean success = webSocket.send(message);

            if (!success) {
                Log.e(TAG, "Failed to send elements. Socket might be closed.");
            }
        }
    }

    private void startQrScanner() {

        GmsBarcodeScannerOptions options =
                new GmsBarcodeScannerOptions.Builder()
                        .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
                        .build();

        var scanner = GmsBarcodeScanning.getClient(this, options);

        scanner.startScan()
                .addOnSuccessListener(barcode -> {

                    String rawValue = barcode.getRawValue();

                    if (rawValue != null) {

                        try {

                            Uri uri = Uri.parse(rawValue);
                            processUri(uri);

                        } catch (Exception e) {

                            textView.setText(
                                    "Error parsing QR: " + e.getMessage()
                            );
                        }
                    }
                })

                .addOnCanceledListener(() ->
                        textView.setText("Scan cancelled")
                )

                .addOnFailureListener(e -> {

                    textView.setText(
                            "Error scanning: " + e.getMessage()
                    );

                    Log.e(TAG, "Barcode scanning failed", e);
                });
    }

    private void processUri(Uri uri) {

        String wsUrl = uri.getQueryParameter("ws");

        if (wsUrl != null && !wsUrl.isEmpty()) {

            Log.d(TAG, "Processing URI: " + wsUrl);

            runOnUiThread(() ->
                    textView.setText("Connecting to: " + wsUrl)
            );

            connectWebSocket(wsUrl);

        } else {

            runOnUiThread(() ->
                    textView.setText("Invalid QR code format.")
            );
        }
    }

    @Override
    protected void onNewIntent(Intent intent) {

        super.onNewIntent(intent);

        setIntent(intent);

        if (intent != null && intent.getData() != null) {

            Uri uri = intent.getData();

            Log.d(TAG, "Received new deep link: " + uri);

            processUri(uri);
        }
    }

    // --- NFC Lifecycle Methods ---

    @Override
    protected void onResume() {

        super.onResume();

        int flags =
                NfcAdapter.FLAG_READER_NFC_A |
                        NfcAdapter.FLAG_READER_NFC_B |
                        NfcAdapter.FLAG_READER_NFC_F |
                        NfcAdapter.FLAG_READER_NFC_V;

        if (nfcAdapter != null) {
            nfcAdapter.enableReaderMode(this, this, flags, null);
        }
    }

    @Override
    protected void onPause() {

        super.onPause();

        if (nfcAdapter != null) {
            nfcAdapter.disableReaderMode(this);
        }
    }

    // --- NFC Callback ---

    @Override
    public void onTagDiscovered(Tag tag) {
        if (tag == null) return;

        Log.d(TAG, "NFC Tag Discovered!");

        String cardData = readNdefMessage(tag);

        if (cardData == null) {

            byte[] tagIdBytes = tag.getId();

            StringBuilder builder = new StringBuilder();

            for (byte b : tagIdBytes) {
                builder.append(String.format("%02x", b));
            }

            cardData = "UID:" + builder.toString();
        }

        boolean success = webSocket != null && webSocket.send(cardData);

        String finalCardData = cardData;

        runOnUiThread(() -> {

            if (success) {
                textView.setText("Sent to Unity: " + finalCardData);
            } else {
                textView.setText(
                        "Failed to send. Is WebSocket connected?"
                );
            }
        });
    }

    private String readNdefMessage(Tag tag) {

        Ndef ndef = Ndef.get(tag);

        if (ndef == null) return null;

        try {

            ndef.connect();

            android.nfc.NdefMessage ndefMessage = ndef.getNdefMessage();

            if (ndefMessage == null) return null;

            android.nfc.NdefRecord[] records =
                    ndefMessage.getRecords();

            if (records.length > 0) {

                byte[] payload = records[0].getPayload();

                String textEncoding =
                        ((payload[0] & 128) == 0)
                                ? "UTF-8"
                                : "UTF-16";

                int languageCodeLength = payload[0] & 51;

                return new String(
                        payload,
                        languageCodeLength + 1,
                        payload.length - languageCodeLength - 1,
                        Charset.forName(textEncoding)
                );
            }

        } catch (Exception e) {

            Log.e(TAG, "Error reading NDEF", e);

        } finally {

            try {
                ndef.close();
            } catch (Exception ignored) {
            }
        }

        return null;
    }

    // --- WebSocket Methods ---

    private void connectWebSocket(String wsUrl) {

        if (webSocket != null) {
            webSocket.close(1000, "Opening new connection");
        }

        OkHttpClient client = new OkHttpClient();

        Request request =
                new Request.Builder()
                        .url(wsUrl)
                        .build();

        webSocket = client.newWebSocket(request,
                new WebSocketListener() {

                    @Override
                    public void onOpen(
                            WebSocket webSocket,
                            Response response
                    ) {

                        runOnUiThread(() -> {

                            textView.setText(
                                    "Connected! Ready to scan cards."
                            );

                            scanButton.setText("Scan Again");
                        });
                    }

                    @Override
                    public void onMessage(
                            WebSocket webSocket,
                            String text
                    ) {

                        runOnUiThread(() ->
                                textView.setText(
                                        "Unity says: " + text
                                )
                        );
                    }

                    @Override
                    public void onClosing(
                            WebSocket webSocket,
                            int code,
                            String reason
                    ) {

                        webSocket.close(1000, null);
                    }

                    @Override
                    public void onFailure(
                            WebSocket webSocket,
                            Throwable t,
                            Response response
                    ) {

                        runOnUiThread(() ->
                                textView.setText(
                                        "Connection failed: " +
                                                t.getMessage()
                                )
                        );
                    }
                });
    }

    @Override
    protected void onDestroy() {

        super.onDestroy();

        if (webSocket != null) {
            webSocket.close(1000, "App destroyed");
        }
    }
}