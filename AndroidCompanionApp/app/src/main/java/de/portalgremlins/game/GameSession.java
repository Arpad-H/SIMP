package de.portalgremlins.game;

import de.portalgremlins.game.lobby.BlindPick;
import de.portalgremlins.game.lobby.DraftPick;
import de.portalgremlins.game.lobby.LobbyPhase;
import de.portalgremlins.game.lobby.LobbyType;
import de.portalgremlins.network.connection.GameConnection;

public class GameSession {
    private static GameSession currentGameSession;

    public static GameSession getCurrentGameSession() {
        return currentGameSession;
    }

    public static GameSession startGameSession(GameConnection<?, ?> gameConnection) {
        if (currentGameSession != null) {
            currentGameSession.connection.closeConnection();
        }
        currentGameSession = new GameSession(gameConnection);
        return currentGameSession;
    }

    private final GameConnection<?, ?> connection;
    private LobbyType lobbyType;
    private boolean isLobby = true;
    private LobbyPhase lobbyPhase;

    private GameSession(GameConnection<?, ?> connection) {
        this.connection = connection;
    }

    public GameConnection<?, ?> getConnection() {
        return connection;
    }

    public LobbyType getLobbyType() {
        return lobbyType;
    }

    public boolean isLobby() {
        return isLobby;
    }

    public void setLobbyType(LobbyType lobbyType) {
        this.lobbyType = lobbyType;
        if (this.lobbyType.equals(LobbyType.BLIND_PICK)) {
            this.lobbyPhase = new BlindPick();
        } else if (this.lobbyType.equals(LobbyType.DRAFT_PICK_FIRST)) {
            this.lobbyPhase = new DraftPick();
        }
    }

    public void setLobby(boolean lobby) {
        isLobby = lobby;
    }

    public DraftPick getActiveDraftPickLobby() {
        return (DraftPick) lobbyPhase;
    }

    public BlindPick getActiveBlindPickLobby() {
        return (BlindPick) lobbyPhase;
    }

    public LobbyPhase getActiveLobby() {
        return lobbyPhase;
    }

    public void initiateGameState() {
        this.isLobby = false;
    }
}


