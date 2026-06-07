package de.portalgremlins.network.connection.socket;

import java.util.Arrays;

import de.portalgremlins.game.Resonance;
import de.portalgremlins.network.connection.message.MessageProtocol;

public class WebSocketProtocol implements MessageProtocol {
    private final WebSocketGameConnection webSocketGameConnection;

    WebSocketProtocol(WebSocketGameConnection webSocketGameConnection) {
        this.webSocketGameConnection = webSocketGameConnection;
    }

    @Override
    public void selectElements(Resonance... threeResonances) {
        String message = WebSocketMessages.SELECT_BLIND_PICK_ELEMENTS + ":" + String.join(",", Arrays.stream(threeResonances).map(Resonance::getName).toArray(String[]::new));
        this.webSocketGameConnection.sendMessage(message);
    }

    @Override
    public void selectNextDraftElement(Resonance resonance) {
        this.webSocketGameConnection.sendMessage(WebSocketMessages.SELECT_SINGLE_ELEMENT + ":" + resonance.toString());
    }

    @Override
    public void playCard(String cardIdentifier) {
        this.webSocketGameConnection.sendMessage(WebSocketMessages.PLAY_CARD + ":" + cardIdentifier);
    }
}
