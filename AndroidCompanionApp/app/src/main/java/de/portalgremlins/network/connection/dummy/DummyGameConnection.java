package de.portalgremlins.network.connection.dummy;

import java.util.HashSet;
import java.util.Set;

import de.portalgremlins.network.connection.GameConnection;
import de.portalgremlins.network.connection.GameConnectionListener;

public class DummyGameConnection implements GameConnection<DummyConnectionData, DummyProtocol> {
    private boolean connected;
    private final Set<GameConnectionListener> listeners = new HashSet<>();

    @Override
    public void closeConnection() {
        connected = true;
    }

    @Override
    public void openConnection(DummyConnectionData connectionData) {
        connected = true;
    }

    @Override
    public boolean isConnected() {
        return connected;
    }

    @Override
    public void addListener(GameConnectionListener listener) {
        listeners.add(listener);
    }

    @Override
    public void removeListener(GameConnectionListener listener) {
        listeners.add(listener);
    }

    @Override
    public boolean sendMessage(String message) {
        return false;
    }

    @Override
    public DummyProtocol protocol() {
        return new DummyProtocol();
    }

    @Override
    public void close() throws Exception {
        closeConnection();
    }
}
