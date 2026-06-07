package de.portalgremlins;

import android.content.Intent;
import android.os.Bundle;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;

import de.portalgremlins.game.DataStorage;

public class MenuView extends AppCompatActivity {
    public static final String PLAYER_NAME_PREFIX = "Player: ";
    private TextView nameTextView;
    private Button webSocketModeButton;
    private Button blueToothModeButton;
    private Button changeName;
    private Button cardCollection;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.main_menu);

        nameTextView = findViewById(R.id.textUsername);
        webSocketModeButton = findViewById(R.id.buttonWebsocket);
        blueToothModeButton = findViewById(R.id.buttonBluetooth);
        changeName = findViewById(R.id.buttonChangeName);
        cardCollection = findViewById(R.id.buttonCollection);

        webSocketModeButton.setOnClickListener(v -> {
            Intent intent = new Intent(this, WebSocketConnectActivity.class);
            startActivity(intent);
        });

        blueToothModeButton.setOnClickListener(v -> {
            Toast.makeText(this, "Bluetooth Play is currently in development.", Toast.LENGTH_SHORT).show();
        });

        changeName.setOnClickListener(v -> {
            showEditNameDialog();
        });

        cardCollection.setOnClickListener(v -> {
            Toast.makeText(this, "Card collection is currently in development.", Toast.LENGTH_SHORT).show();
        });

        this.nameTextView.setText(PLAYER_NAME_PREFIX + DataStorage.getInstance().getStoredUserName(this));
    }

    private void showEditNameDialog() {

        AlertDialog.Builder builder = new AlertDialog.Builder(this);

        builder.setTitle("Enter Username");

        EditText input = new EditText(this);
        input.setHint("Name");

        String currentName = nameTextView
                .getText()
                .toString()
                .replace(PLAYER_NAME_PREFIX, "");

        input.setText(currentName);

        builder.setView(input);

        builder.setPositiveButton("Confirm", (dialog, which) -> {
            String newName = input.getText().toString();
            nameTextView.setText(PLAYER_NAME_PREFIX + newName);
            DataStorage.getInstance().setStoredUserName(this, newName);
        });

        builder.setNegativeButton("Cancel", (dialog, which) -> dialog.cancel());

        builder.show();
    }
}
