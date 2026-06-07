package de.portalgremlins.network.connection;

public interface GameConnectionListener {
    void onMessage(String message);

    void onConnect();

    void onDisconnect();

    void onFailure(Throwable throwable);
}
