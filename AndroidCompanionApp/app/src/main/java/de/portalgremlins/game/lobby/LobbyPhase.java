package de.portalgremlins.game.lobby;

public interface LobbyPhase {
    void resetLobbyState();
    boolean setPlayerReady();
}
