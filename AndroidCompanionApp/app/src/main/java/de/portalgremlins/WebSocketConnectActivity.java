package de.portalgremlins;

import android.annotation.SuppressLint;
import android.content.Intent;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.util.Log;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.GridLayout;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

import com.google.mlkit.vision.barcode.common.Barcode;
import com.google.mlkit.vision.codescanner.GmsBarcodeScannerOptions;
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning;

import java.util.Locale;
import java.util.Objects;

import de.portalgremlins.game.GameSession;
import de.portalgremlins.game.lobby.BlindPick;
import de.portalgremlins.game.lobby.DraftPick;
import de.portalgremlins.game.lobby.LobbyType;
import de.portalgremlins.game.Resonance;
import de.portalgremlins.network.connection.GameConnectionListener;
import de.portalgremlins.network.connection.dummy.DummyConnectionData;
import de.portalgremlins.network.connection.dummy.DummyGameConnection;
import de.portalgremlins.network.connection.socket.WebSocketConnectionData;
import de.portalgremlins.network.connection.socket.WebSocketGameConnection;

public class WebSocketConnectActivity extends AppCompatActivity implements GameConnectionListener {
    private FrameLayout pickDiv;
    private Button scanButton;
    private TextView textConnectionStatus;
    private TextView lobbyTypeText;
    private BlindPickUI blindPickUI;
    private DraftPickUI draftPickUI;
    private LobbyType serverLobbyType;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.websocket);
        scanButton = findViewById(R.id.buttonScanQr);
        textConnectionStatus = findViewById(R.id.textConnectionStatus);
        pickDiv = findViewById(R.id.pick_frame);
        lobbyTypeText = findViewById(R.id.lobby_type_text);
        setErrorState("Waiting for QR Scan...");

        scanButton.setOnClickListener(v -> startQrScanner());
        Button startButton = findViewById(R.id.buttonStartGame);
        startButton.setOnClickListener(v -> {
            GameSession.getCurrentGameSession().getActiveLobby().setPlayerReady();
        });

        Intent intent = getIntent();
        if (intent.getData() != null) {
            Uri uri = intent.getData();
            Log.d("WebSocketConnector", "Received new deep link: " + uri);
            startGameSessionFromUri(uri);
        }
    }

    private void setErrorState(String text) {
        textConnectionStatus.setTextColor(Color.parseColor("#FF5555"));
        textConnectionStatus.setText(text);
    }

    private void setSuccessState(String text) {
        textConnectionStatus.setTextColor(Color.parseColor("#09bd39"));
        textConnectionStatus.setText(text);
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
                            startGameSessionFromUri(uri);
                        } catch (Exception e) {
                            setErrorState("Error parsing QR: " + e.getMessage());
                        }
                    }
                })

                .addOnCanceledListener(() -> {
                            setErrorState("Scan cancelled");
                            var dummyGameConnection = new DummyGameConnection();
                            dummyGameConnection.addListener(this);
                            dummyGameConnection.openConnection(new DummyConnectionData());
                            GameSession.startGameSession(dummyGameConnection);

                            this.draftPickUI = new DraftPickUI();
                            draftPickUI.setupDraftPick(true);
                            //new BlindPickUI().setupBlindPick();
                        }
                )

                .addOnFailureListener(e -> {
                    setErrorState("Error scanning: " + e.getMessage());
                });
    }

    private void startGameSessionFromUri(Uri uri) {
        String webSocketUrl = processUri(uri);
Log.e("connection", webSocketUrl);
        var webSocketGameConnection = new WebSocketGameConnection();
        webSocketGameConnection.addListener(this);
        webSocketGameConnection.openConnection(new WebSocketConnectionData(webSocketUrl));
        GameSession.startGameSession(webSocketGameConnection);
        System.out.println("Started game session!");
    }

    private String processUri(Uri uri) {
        String rawLobbyType = uri.getQueryParameter("lobbyType");
        LobbyType lobbyType = LobbyType.valueOf(rawLobbyType.toUpperCase(Locale.ROOT));
        this.serverLobbyType = lobbyType;

        String wsUrl = uri.getQueryParameter("ws");
        if (wsUrl != null && !wsUrl.isEmpty()) {
            Log.d("WebSocketConnector", "Processing URI: " + wsUrl);
            return wsUrl;
        } else {
            return "";
        }
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);

        setIntent(intent);

        if (intent.getData() != null) {
            Uri uri = intent.getData();
            Log.d("WebSocketConnector", "Received new deep link: " + uri);
            startGameSessionFromUri(uri);
        }
    }

    @Override
    public void onMessage(String message) {
        runOnUiThread(() -> {
            if (message.contains("ENEMY_PICK:") && GameSession.getCurrentGameSession().isLobby() && GameSession.getCurrentGameSession().getLobbyType().equals(LobbyType.DRAFT_PICK_FIRST)) {
                String value = message.replace("ENEMY_PICK:", "");
                try {
                    Resonance resonance = Resonance.valueOf(value.toUpperCase(Locale.ROOT));
                    this.draftPickUI.onEnemyPlayerPicked(GameSession.getCurrentGameSession().getActiveDraftPickLobby().getAndIncrementCurrentPickIndex(), resonance);
                } catch (Exception e) {
                    System.out.println("Enemy picked an unknown resonance: " + value.toUpperCase(Locale.ROOT));
                }
            } else if (message.equals("INITIATE_GAME_STATE")) {
                GameSession.getCurrentGameSession().initiateGameState();
                Intent intent = new Intent(this, GameActivity.class);
                startActivity(intent);
            }
        });
    }

    @Override
    public void onConnect() {
        runOnUiThread(() -> {
            setSuccessState("Connected to lobby.");

            switch (serverLobbyType) {
                case BLIND_PICK:
                    this.blindPickUI = new BlindPickUI();
                    this.blindPickUI.setupBlindPick();
                    break;
                case DRAFT_PICK_FIRST:
                    this.draftPickUI = new DraftPickUI();
                    this.draftPickUI.setupDraftPick(true);
                    break;
                case DRAFT_PICK_LAST:
                    this.draftPickUI = new DraftPickUI();
                    this.draftPickUI.setupDraftPick(false);
                    break;
            }
        });
    }

    @Override
    public void onDisconnect() {
        runOnUiThread(() -> {
            lobbyTypeText.setText("");
            Toast.makeText(this, "The websocket was closed", Toast.LENGTH_LONG * 5).show();
        });
    }

    @Override
    public void onFailure(Throwable throwable) {
        runOnUiThread(() -> {
            lobbyTypeText.setText("");
            Toast.makeText(this, "An internal error occurred", Toast.LENGTH_LONG * 5).show();
        });
    }

    private static final byte[] PICK_ORDER_FIRST_PICK = new byte[]{0, 3, 4};
    private static final byte[] PICK_ORDER_LAST_PICK = new byte[]{1, 2, 5};

    private class BlindPickUI {
        private LinearLayout selectedSlotsContainer;

        private void setupBlindPick() {
            lobbyTypeText.setText("Blind Pick");

            GameSession.getCurrentGameSession().setLobbyType(LobbyType.BLIND_PICK);
            GameSession.getCurrentGameSession().setLobby(true);
            BlindPick blindPickLobby = GameSession.getCurrentGameSession().getActiveBlindPickLobby();
            blindPickLobby.resetLobbyState();

            LinearLayout mainLayout = new LinearLayout(WebSocketConnectActivity.this);
            mainLayout.setOrientation(LinearLayout.VERTICAL);

            selectedSlotsContainer = new LinearLayout(WebSocketConnectActivity.this);
            selectedSlotsContainer.setOrientation(LinearLayout.HORIZONTAL);
            selectedSlotsContainer.setGravity(android.view.Gravity.CENTER);
            LinearLayout.LayoutParams slotContainerParams = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT
            );
            slotContainerParams.bottomMargin = 30;
            selectedSlotsContainer.setLayoutParams(slotContainerParams);

            updateSelectionVisuals(blindPickLobby);
            mainLayout.addView(selectedSlotsContainer);

            GridLayout grid = new GridLayout(WebSocketConnectActivity.this);
            grid.setRowCount(3);
            grid.setColumnCount(3);

            for (Resonance resonance : Resonance.values()) {
                Button button = new Button(WebSocketConnectActivity.this);
                button.setText(resonance.name());
                setButtonVisualState(button, resonance, false);

                button.setOnClickListener(v -> {
                    if (blindPickLobby.hasAlreadyPicked(resonance)) {
                        blindPickLobby.unpickResonance(resonance);
                        setButtonVisualState(button, resonance, false);
                    } else {
                        if (blindPickLobby.canStillPick()) {
                            blindPickLobby.pickResonance(resonance);
                            setButtonVisualState(button, resonance, true);
                        } else {
                            Toast.makeText(WebSocketConnectActivity.this, "Maximal 3 Resonanzen erlaubt!", Toast.LENGTH_SHORT).show();
                        }
                    }
                    updateSelectionVisuals(blindPickLobby);
                });

                GridLayout.LayoutParams params = new GridLayout.LayoutParams();
                params.width = 0;
                params.height = GridLayout.LayoutParams.WRAP_CONTENT;
                params.topMargin = 10;
                params.leftMargin = 10;
                params.rightMargin = 10;
                params.columnSpec = GridLayout.spec(GridLayout.UNDEFINED, 1f);
                button.setLayoutParams(params);

                grid.addView(button);
            }

            mainLayout.addView(grid);

            pickDiv.removeAllViews();
            pickDiv.addView(mainLayout);
        }

        private void updateSelectionVisuals(BlindPick blindPick) {
            if (selectedSlotsContainer == null) return;
            selectedSlotsContainer.removeAllViews();

            for (int i = 0; i < 3; i++) {
                TextView slot = new TextView(WebSocketConnectActivity.this);
                slot.setGravity(android.view.Gravity.CENTER);
                slot.setTextSize(14);
                slot.setTypeface(null, android.graphics.Typeface.BOLD);

                LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(0, 100, 1f);
                p.leftMargin = 10;
                p.rightMargin = 10;
                slot.setLayoutParams(p);

                if (i < blindPick.getAmountSelectedResonances()) {
                    Resonance res = blindPick.getPickedResonanceBySlot(i);
                    slot.setText(res.name());
                    slot.setTextColor(Color.WHITE);
                    slot.setBackgroundColor(Color.parseColor(res.getDominantColorCode()));
                } else {
                    slot.setText("[ Empty ]");
                    slot.setTextColor(Color.parseColor("#444444"));
                    slot.setBackgroundColor(Color.parseColor("#151821"));
                }

                selectedSlotsContainer.addView(slot);
            }

            Button startButton = findViewById(R.id.buttonStartGame);
            if (startButton != null) {
                startButton.setEnabled(!blindPick.canStillPick());
                startButton.setBackgroundColor(!blindPick.canStillPick() ? Color.parseColor("#00C853") : Color.parseColor("#6b6b6b"));
            }
        }

        private void setButtonVisualState(Button button, Resonance resonance, boolean isSelected) {
            if (isSelected) {
                button.setBackgroundColor(Color.parseColor(resonance.getDominantColorCode()));
                button.setTextColor(Color.WHITE);
            } else {
                button.setBackgroundColor(Color.parseColor("#dbcece"));
                button.setTextColor(Color.parseColor(resonance.getDominantColorCode()));
            }
        }
    }

    private class DraftPickUI {
        private LinearLayout myDraftContainer;
        private LinearLayout enemyDraftContainer;
        private GridLayout draftGrid;

        private void setupDraftPick(boolean isFirstPick) {
            GameSession.getCurrentGameSession().setLobbyType(LobbyType.DRAFT_PICK_FIRST);
            GameSession.getCurrentGameSession().setLobby(true);
            lobbyTypeText.setText("Draft Pick");
            GameSession.getCurrentGameSession().getActiveDraftPickLobby().setup(isFirstPick);

            LinearLayout mainLayout = new LinearLayout(WebSocketConnectActivity.this);
            mainLayout.setOrientation(LinearLayout.VERTICAL);

            LinearLayout draftHeader = new LinearLayout(WebSocketConnectActivity.this);
            draftHeader.setOrientation(LinearLayout.HORIZONTAL);
            draftHeader.setWeightSum(2f);
            LinearLayout.LayoutParams headerParams = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
            headerParams.bottomMargin = 30;
            draftHeader.setLayoutParams(headerParams);

            myDraftContainer = new LinearLayout(WebSocketConnectActivity.this);
            myDraftContainer.setOrientation(LinearLayout.VERTICAL);
            draftHeader.addView(myDraftContainer, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

            enemyDraftContainer = new LinearLayout(WebSocketConnectActivity.this);
            enemyDraftContainer.setOrientation(LinearLayout.VERTICAL);
            draftHeader.addView(enemyDraftContainer, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

            mainLayout.addView(draftHeader);

            draftGrid = new GridLayout(WebSocketConnectActivity.this);
            draftGrid.setRowCount(3);
            draftGrid.setColumnCount(3);
            mainLayout.addView(draftGrid);

            updateDraftUi();

            pickDiv.removeAllViews();
            pickDiv.addView(mainLayout);
        }

        @SuppressLint("ClickableViewAccessibility")
        private void updateDraftUi() {
            if (myDraftContainer == null || enemyDraftContainer == null || draftGrid == null)
                return;

            DraftPick draftPick = GameSession.getCurrentGameSession().getActiveDraftPickLobby();

            myDraftContainer.removeAllViews();
            TextView myTitle = new TextView(WebSocketConnectActivity.this);
            myTitle.setText("YOUR PICKS");
            myTitle.setTextColor(Color.parseColor("#1E88E5"));
            myTitle.setTextSize(12);
            myTitle.setPadding(20, 0, 0, 0);
            myTitle.setTypeface(null, android.graphics.Typeface.BOLD);
            myDraftContainer.addView(myTitle);

            for (int i = 0; i < 3; i++) {
                TextView slot = createDraftSlotView(i, draftPick.getPickedResonancesPlayer(), "#151821");
                myDraftContainer.addView(slot);
            }

            enemyDraftContainer.removeAllViews();
            TextView enemyTitle = new TextView(WebSocketConnectActivity.this);
            enemyTitle.setText("ENEMY PICKS");
            enemyTitle.setTextColor(Color.parseColor("#FF5555"));
            enemyTitle.setTextSize(12);
            enemyTitle.setPadding(0, 0, 20, 0);
            enemyTitle.setGravity(android.view.Gravity.END);
            enemyTitle.setTypeface(null, android.graphics.Typeface.BOLD);
            enemyDraftContainer.addView(enemyTitle);

            for (int i = 0; i < 3; i++) {
                TextView slot = createDraftSlotView(i, draftPick.getPickedResonancesEnemy(), "#151821");
                slot.setGravity(android.view.Gravity.END);
                enemyDraftContainer.addView(slot);
            }

            draftGrid.removeAllViews();
            for (Resonance resonance : Resonance.values()) {
                Button button = new Button(WebSocketConnectActivity.this);
                button.setText(resonance.name());

                boolean isPickedByMe = contains(draftPick.getPickedResonancesPlayer(), resonance);
                boolean isPickedByEnemy = contains(draftPick.getPickedResonancesEnemy(), resonance);

                if (isPickedByMe) {
                    button.setBackgroundColor(Color.parseColor(resonance.getDominantColorCode()));
                    button.setTextColor(Color.WHITE);
                    button.setEnabled(false);
                } else if (isPickedByEnemy) {
                    button.setBackgroundColor(Color.parseColor("#221111"));
                    button.setTextColor(Color.parseColor("#553333"));
                    button.setText(resonance.name() + " (Taken)");
                    button.setEnabled(false);
                } else {
                    button.setBackgroundColor(Color.parseColor("#dbcece"));
                    button.setTextColor(Color.parseColor(resonance.getDominantColorCode()));
                    button.setEnabled(true);
                    button.setOnClickListener(null);

                    android.graphics.drawable.GradientDrawable chargeColor = new android.graphics.drawable.GradientDrawable();
                    chargeColor.setColor(Color.parseColor(resonance.getDominantColorCode()));


                    button.setOnClickListener(v -> {
                        if (canPick(draftPick.getMyPickOrders(), draftPick.getCurrentPickIndex())) {
                            onLocalPlayerPicked(draftPick.getAndIncrementCurrentPickIndex(), resonance);
                        }
                    });
                }

                GridLayout.LayoutParams params = new GridLayout.LayoutParams();
                params.width = 0;
                params.height = GridLayout.LayoutParams.WRAP_CONTENT;
                params.topMargin = 10;
                params.leftMargin = 10;
                params.rightMargin = 10;
                params.columnSpec = GridLayout.spec(GridLayout.UNDEFINED, 1f);
                button.setLayoutParams(params);

                draftGrid.addView(button);
            }

            Button startButton = findViewById(R.id.buttonStartGame);
            if (startButton != null) {
                startButton.setEnabled(draftPick.getCurrentPickIndex() >= 6);
            }
        }

        private TextView createDraftSlotView(int index, Resonance[] picks, String emptyBgColor) {
            TextView slot = new TextView(WebSocketConnectActivity.this);
            slot.setTextSize(14);
            slot.setPadding(10, 10, 10, 10);

            LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
            p.topMargin = 6;
            slot.setLayoutParams(p);

            if (index < picks.length && picks[index] != null) {
                Resonance res = picks[index];
                slot.setText(res.name());
                slot.setTextColor(Color.WHITE);
                slot.setBackgroundColor(Color.parseColor(res.getDominantColorCode()));
            } else {
                slot.setText("-");
                slot.setTextColor(Color.parseColor("#444444"));
                slot.setBackgroundColor(Color.parseColor(emptyBgColor));
            }
            return slot;
        }

        public void onLocalPlayerPicked(int slot, Resonance resonance) {
            GameSession.getCurrentGameSession().getConnection().protocol().selectNextDraftElement(resonance);
            runOnUiThread(() -> {
                GameSession.getCurrentGameSession().getActiveDraftPickLobby().getPickedResonancesPlayer()[slot] = resonance;
                updateDraftUi();
            });
        }

        public void onEnemyPlayerPicked(int slot, Resonance resonance) {
            runOnUiThread(() -> {
                GameSession.getCurrentGameSession().getActiveDraftPickLobby().getPickedResonancesEnemy()[slot] = resonance;
                updateDraftUi();
            });
        }

        private boolean contains(Resonance[] picks, Resonance pick) {
            for (Resonance resonance : picks) {
                if (Objects.equals(pick, resonance)) {
                    return true;
                }
            }
            return false;
        }

        private boolean canPick(byte[] pickOrder, int currentPickIndex) {
            for (byte b : pickOrder) {
                if (currentPickIndex == b) {
                    return true;
                }
            }
            return false;
        }
    }
}
