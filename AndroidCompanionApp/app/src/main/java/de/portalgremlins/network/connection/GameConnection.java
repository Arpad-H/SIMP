package de.portalgremlins.network.connection;

import de.portalgremlins.network.connection.message.MessageProtocol;

public interface GameConnection<DATA extends ConnectionData, PROTOCOL extends MessageProtocol> extends AutoCloseable {
    void closeConnection();

    void openConnection(DATA connectionData);

    boolean isConnected();

    void addListener(GameConnectionListener listener);

    void removeListener(GameConnectionListener listener);

    /**
     * Please use {@link #protocol()} instead
     *
     * @param message The message to send
     * @return
     */
    @Deprecated
    boolean sendMessage(String message);

    PROTOCOL protocol();
}
