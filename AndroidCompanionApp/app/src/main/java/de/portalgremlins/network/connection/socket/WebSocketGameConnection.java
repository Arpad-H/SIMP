package de.portalgremlins.network.connection.socket;

import android.util.Log;

import java.util.LinkedHashSet;
import java.util.Set;

import de.portalgremlins.network.connection.GameConnection;
import de.portalgremlins.network.connection.GameConnectionListener;
import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.Response;
import okhttp3.WebSocket;
import okhttp3.WebSocketListener;

public class WebSocketGameConnection implements GameConnection<WebSocketConnectionData, WebSocketProtocol> {
    private final OkHttpClient client = new OkHttpClient();
    private WebSocket currentSocket;
    private final Set<GameConnectionListener> listeners = new LinkedHashSet<>();
    private final WebSocketProtocol webSocketProtocol = new WebSocketProtocol(this);



    @Override
    public void closeConnection() {
        if (currentSocket == null) {
            return;
        }
        currentSocket.close(1000, "Game Connection closed");
    }

    @Override
    public void openConnection(WebSocketConnectionData connectionData) {
        if (currentSocket != null) {
            currentSocket.close(1000, "Opening new connection");
            currentSocket = null;
        }
        Request request = new Request.Builder().url(connectionData.getSocketUrl()).build();
        currentSocket = client.newWebSocket(request,
                new WebSocketListener() {
                    @Override
                    public void onOpen(
                            WebSocket webSocket,
                            Response response
                    ) {
                        for (GameConnectionListener listener : listeners) {
                            listener.onConnect();
                        }
                    }

                    @Override
                    public void onMessage(
                            WebSocket webSocket,
                            String text
                    ) {
                        for (GameConnectionListener listener : listeners) {
                            listener.onMessage(text);
                        }
                    }

                    @Override
                    public void onClosing(
                            WebSocket webSocket,
                            int code,
                            String reason
                    ) {
                        webSocket.close(1000, null);

                        for (GameConnectionListener listener : listeners) {
                            listener.onDisconnect();
                        }
                    }

                    @Override
                    public void onFailure(
                            WebSocket webSocket,
                            Throwable t,
                            Response response
                    ) {
                        Log.e("WebSocket", "Connection failed", t);
                        for (GameConnectionListener listener : listeners) {
                            listener.onFailure(t);
                        }
                    }
                });
    }

    @Override
    public boolean isConnected() {
        return currentSocket != null;
    }

    @Override
    public void addListener(GameConnectionListener listener) {
        listeners.add(listener);
    }

    @Override
    public void removeListener(GameConnectionListener listener) {
        listeners.remove(listener);
    }

    @Override
    public boolean sendMessage(String message) {
        if (currentSocket != null) {
            return currentSocket.send(message);
        }
        return false;
    }

    @Override
    public WebSocketProtocol protocol() {
        return webSocketProtocol;
    }

    @Override
    public void close() {
        closeConnection();
    }
}
