package de.portalgremlins;

import android.app.GameState;
import android.content.Context;
import android.content.Intent;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.media.AudioManager;
import android.media.ToneGenerator;
import android.nfc.NdefMessage;
import android.nfc.NdefRecord;
import android.nfc.NfcAdapter;
import android.nfc.Tag;
import android.nfc.tech.Ndef;
import android.os.Build;
import android.os.Bundle;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.util.Log;
import android.view.View;
import android.widget.ImageView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import java.io.UnsupportedEncodingException;
import java.nio.charset.Charset;
import java.util.Arrays;

import de.portalgremlins.game.GameSession;
import de.portalgremlins.game.PlayerGameState;
import de.portalgremlins.network.connection.GameConnectionListener;

public class GameActivity extends AppCompatActivity implements NfcAdapter.ReaderCallback, GameConnectionListener {
    private TextView tvInstruction;
    private TextView tvSubInstruction;
    private TextView tvLastScannedCard;
    private ImageView ivNfcIcon;
    private View viewPulseRing;
    private NfcAdapter nfcAdapter;
    private PlayerGameState state = PlayerGameState.WAIT;

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.game_view);

        tvInstruction = findViewById(R.id.tv_instruction);
        tvSubInstruction = findViewById(R.id.tv_sub_instruction);
        tvLastScannedCard = findViewById(R.id.tv_last_scanned_card);
        ivNfcIcon = findViewById(R.id.iv_nfc_icon);
        viewPulseRing = findViewById(R.id.view_pulse_ring);

        updateGameUI(PlayerGameState.PLAY_CARD, "");
        GameSession.getCurrentGameSession().getConnection().addListener(this);

        nfcAdapter = NfcAdapter.getDefaultAdapter(this);

        if (nfcAdapter == null) {
            //TODO: Error handling
        }
    }

    public void updateGameUI(PlayerGameState state, String extraInfo) {
        this.state = state;
        int color;

        switch (state) {
            case DRAW_CARD:
                tvInstruction.setText("Draw a card!");
                tvSubInstruction.setText("Draw a card from your deck then scan the new drawn card.");
                color = Color.parseColor("#00BCD4");
                applyColorTheme(color);
                break;

            case PLAY_CARD:
                tvInstruction.setText("Play a card!");
                tvSubInstruction.setText("Scan a card at the back of your phone to play it.");
                color = Color.parseColor("#9C27B0");
                applyColorTheme(color);
                triggerAttentionCue();
                break;

            case ERROR:
                playErrorSound(); // Direkt den Fehlerton abspielen
                tvInstruction.setText("Error!");
                if (extraInfo == null || extraInfo.isEmpty()) {
                    tvSubInstruction.setText("This card cannot be played right now.");
                } else {
                    tvSubInstruction.setText(extraInfo);
                }
                color = Color.parseColor("#E91E63");
                applyColorTheme(color);
                break;

            case WAIT:
                tvInstruction.setText("Enemys turn!");
                tvSubInstruction.setText("Please wait!");
                color = Color.parseColor("#616161");
                applyColorTheme(color);
                break;

            case CONNECTING:
                tvInstruction.setText("Connecting to game");
                tvSubInstruction.setText("Please wait....");
                color = Color.parseColor("#616161");
                applyColorTheme(color);
                break;
        }
    }

    private void applyColorTheme(int color) {
        ivNfcIcon.setColorFilter(color);
        viewPulseRing.setBackgroundTintList(ColorStateList.valueOf(color));
    }

    public void onCardScannedSuccessfully(String cardName) {
        playSuccessSound();
        tvLastScannedCard.setText(cardName);
    }

    private void triggerAttentionCue() {
        ToneGenerator toneGen = new ToneGenerator(AudioManager.STREAM_MUSIC, 100);
        toneGen.startTone(ToneGenerator.TONE_CDMA_ALERT_INCALL_LITE, 200);

        Vibrator vibrator = (Vibrator) getSystemService(Context.VIBRATOR_SERVICE);
        if (vibrator != null && vibrator.hasVibrator()) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                vibrator.vibrate(VibrationEffect.createOneShot(150, VibrationEffect.DEFAULT_AMPLITUDE));
            }
        }
    }

    private void playSuccessSound() {
        ToneGenerator toneGen = new ToneGenerator(AudioManager.STREAM_MUSIC, 100);
        toneGen.startTone(ToneGenerator.TONE_PROP_BEEP, 150);
    }

    private void playErrorSound() {
        ToneGenerator toneGen = new ToneGenerator(AudioManager.STREAM_MUSIC, 100);
        toneGen.startTone(ToneGenerator.TONE_CDMA_SOFT_ERROR_LITE, 300);
    }

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

    @Override
    public void onTagDiscovered(Tag tag) {
        if (tag == null) return;

        Log.d("GameActivity", "NFC Tag Discovered!");

        byte[] tagIdBytes = tag.getId();
        StringBuilder builder = new StringBuilder();
        for (byte b : tagIdBytes) {
            builder.append(String.format("%02x", b));
        }
        String uid = builder.toString();

        String payload = readNdefMessage(tag);
        NfcCardData cardData = new NfcCardData(uid, payload);

        runOnUiThread(() -> {
            if (state == PlayerGameState.PLAY_CARD) {
                String cardIdentifier = cardData.getUid();

                GameSession.getCurrentGameSession().getConnection().protocol().playCard(cardData.payload);
                playSuccessSound();
            } else {
                playErrorSound();
            }
        });
    }

    public String readNdefMessage(Tag tag) {
        Ndef ndef = Ndef.get(tag);
        if (ndef == null) {
            return null; // Tag is not NDEF formatted
        }

        NdefMessage ndefMessage = ndef.getCachedNdefMessage();
        if (ndefMessage == null) return null;

        // Get the first record (usually where the text is)
        NdefRecord[] records = ndefMessage.getRecords();
        for (NdefRecord record : records) {
            // Check if the record is a Well-Known Text record
            if (record.getTnf() == NdefRecord.TNF_WELL_KNOWN &&
                    Arrays.equals(record.getType(), NdefRecord.RTD_TEXT)) {
                try {
                    return parseTextRecord(record);
                } catch (Exception e) {
                    Log.e("NFC", "Error parsing text record", e);
                }
            }
        }
        return null;
    }
    private String parseTextRecord(NdefRecord record) throws UnsupportedEncodingException {
        byte[] payload = record.getPayload();

        // The first byte (status byte) contains the encoding and language code length
        // Bit 7: 0 = UTF-8, 1 = UTF-16
        // Bits 5..0: length of the IANA language code (e.g., "en")
        int statusByte = payload[0];
        String textEncoding = ((statusByte & 0x80) == 0) ? "UTF-8" : "UTF-16";
        int languageCodeLength = statusByte & 0x3F;

        // Actual text starts after the status byte and the language code
        return new String(payload,
                1 + languageCodeLength,
                payload.length - 1 - languageCodeLength,
                textEncoding);
    }

    public static class NfcCardData {
        private final String uid;
        private final String payload;

        public NfcCardData(String uid, String payload) {
            this.uid = uid;
            this.payload = payload;
        }

        public String getUid() {
            return uid;
        }

        public String getPayload() {
            return payload;
        }
    }

    @Override
    public void onMessage(String message) {
        runOnUiThread(() -> {
            if (message.equals("ACTION_DRAW_A_CARD")) {
                updateGameUI(PlayerGameState.DRAW_CARD, "");
            } else if (message.equals("ACTION_PLAY_A_CARD")) {
                updateGameUI(PlayerGameState.PLAY_CARD, "");
            } else if (message.equals("ACTION_WAIT")) {
                updateGameUI(PlayerGameState.WAIT, "");
            }
        });
    }

    @Override
    public void onConnect() {

    }

    @Override
    public void onDisconnect() {
        runOnUiThread(() -> {
            updateGameUI(PlayerGameState.ERROR, "");
            Intent intent = new Intent(this, MenuView.class);
            startActivity(intent);
        });
    }

    @Override
    public void onFailure(Throwable throwable) {
        runOnUiThread(() -> {
            updateGameUI(PlayerGameState.ERROR, "");
            Intent intent = new Intent(this, MenuView.class);
            startActivity(intent);
            Toast.makeText(this, throwable.getMessage(), Toast.LENGTH_LONG).show();
        });
    }
}
