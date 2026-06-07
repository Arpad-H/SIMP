package de.portalgremlins.network.connection;

import de.portalgremlins.network.connection.socket.WebSocketGameConnection;

public class GameConnectionService {
    private GameConnection<?,?> gameConnection = new WebSocketGameConnection();

    public GameConnection<?,?> getGameConnection() {
        return gameConnection;
    }
}
