package de.portalgremlins.network.connection.socket;

import de.portalgremlins.network.connection.ConnectionData;

public class WebSocketConnectionData implements ConnectionData {
    private final String socketUrl;

    public WebSocketConnectionData(String socketUrl) {
        this.socketUrl = socketUrl;
    }

    public String getSocketUrl() {
        return socketUrl;
    }
}
