package de.portalgremlins.network.connection.socket.message;

import de.portalgremlins.game.Resonance;
import de.portalgremlins.game.lobby.LobbyType;

public interface WebSocketMessage {
    WebSocketMessage.Param3<Resonance, Resonance, Resonance> SELECT_ELEMENTS_BLIND_PICK =
            define("SELECT_BLIND_PICK")
                    .parameter(Resonance.class)
                    .parameter(Resonance.class)
                    .parameter(Resonance.class)
                    .build();

    WebSocketMessage.Param1<Resonance> SELECT_DRAFT_PICK =
            define("SELECT_DRAFT_PICK")
                    .parameter(Resonance.class)
                    .build();

    WebSocketMessage.Param1<Resonance> REMOTE_SELECT_DRAFT_PICK =
            define("REMOTE_SELECT_DRAFT_PICK")
                    .parameter(Resonance.class)
                    .build();

    WebSocketMessage.Param2<LobbyType, Boolean> SET_LOBBY_TYPE =
            define("SET_LOBBY_TYPE")
                    .parameter(LobbyType.class)
                    .parameter(Boolean.class)
                    .build();

    WebSocketMessage INIT_GAME = define("INITIATE_GAME_STATE").build();
    WebSocketMessage ENEMY_PLAYER_READY = define("ENEMY_PLAYER_READY").build();
    WebSocketMessage READY = define("READY").build();

    static Param0Builder define(String tag) {
        return new Param0Builder(tag);
    }

    String encode();

    interface MessageBuilder<MESSAGE> {
        MESSAGE build();
    }

    class Param0Builder implements MessageBuilder<WebSocketMessage> {
        protected final String tag;

        private Param0Builder(String tag) {
            this.tag = tag;
        }

        public <P1> Param1.Builder<P1> parameter(Class<P1> p1Type) {
            return new Param1.Builder<>(tag, p1Type);
        }

        @Override
        public WebSocketMessage build() {
            return () -> tag;
        }
    }

    interface Param1<P1> {
        class Builder<P1> implements MessageBuilder<Param1<P1>> {
            protected final String tag;
            private final Class<P1> param1Type;

            protected Builder(String tag, Class<P1> param1Type) {
                this.tag = tag;
                this.param1Type = param1Type;
            }

            public <P2> Param2.Builder<P1, P2> parameter(Class<P2> p2Type) {
                return new Param2.Builder<>(tag, param1Type, p2Type);
            }

            @Override
            public Param1<P1> build() {
                return (p1) -> tag + ":" + p1.toString();
            }
        }

        String encode(P1 param1);
        //P1 parseP1(String encoded);
    }

    interface Param2<P1, P2> {
        class Builder<P1, P2> implements MessageBuilder<Param2<P1, P2>> {
            protected final String tag;
            protected final Class<P1> param1Type;
            protected final Class<P2> param2Type;

            protected Builder(String tag, Class<P1> param1Type, Class<P2> param2Type) {
                this.tag = tag;
                this.param1Type = param1Type;
                this.param2Type = param2Type;
            }

            public <P3> Param3.Builder<P1, P2, P3> parameter(Class<P3> p3Type) {
                return new Param3.Builder<>(tag, param1Type, param2Type, p3Type);
            }

            @Override
            public Param2<P1, P2> build() {
                return (p1, p2) -> tag + ":" + p1.toString() + ":" + p2.toString();
            }
        }

        String encode(P1 param1, P2 param2);

        //P1 parseP1(String encoded);
        //P2 parseP2(String encoded);
    }

    interface Param3<P1, P2, P3> {
        class Builder<P1, P2, P3> implements MessageBuilder<Param3<P1, P2, P3>> {
            protected final String tag;
            protected final Class<P1> param1Type;
            protected final Class<P2> param2Type;
            protected final Class<P3> param3Type;

            protected Builder(String tag, Class<P1> param1Type, Class<P2> param2Type, Class<P3> param3Type) {
                this.tag = tag;
                this.param1Type = param1Type;
                this.param2Type = param2Type;
                this.param3Type = param3Type;
            }

            public <P4> Param4.Builder<P1, P2, P3, P4> parameter(Class<P4> p4Type) {
                return new Param4.Builder<>(tag, param1Type, param2Type, param3Type, p4Type);
            }

            @Override
            public Param3<P1, P2, P3> build() {
                return (p1, p2, p3) -> tag + ":" + p1.toString() + ":" + p2.toString() + ":" + p3.toString();
            }
        }

        String encode(P1 param1, P2 param2, P3 param3);

        //P1 parseP1(String encoded);
        //P2 parseP2(String encoded);
        //P3 parseP3(String encoded);
    }

    interface Param4<P1, P2, P3, P4> {
        class Builder<P1, P2, P3, P4> implements MessageBuilder<Param4<P1, P2, P3, P4>> {
            protected final String tag;
            protected final Class<P1> param1Type;
            protected final Class<P2> param2Type;
            protected final Class<P3> param3Type;
            protected final Class<P4> param4Type;

            protected Builder(String tag, Class<P1> param1Type, Class<P2> param2Type, Class<P3> param3Type, Class<P4> param4Type) {
                this.tag = tag;
                this.param1Type = param1Type;
                this.param2Type = param2Type;
                this.param3Type = param3Type;
                this.param4Type = param4Type;
            }

            @Override
            public Param4<P1, P2, P3, P4> build() {
                return (p1, p2, p3, p4) -> tag + ":" +
                        p1.toString() + ":" +
                        p2.toString() + ":" +
                        p3.toString() + ":" +
                        p4.toString();
            }
        }

        String encode(P1 param1, P2 param2, P3 param3, P4 param4);

        //P1 parseP1(String encoded);
        //P2 parseP2(String encoded);
        //P3 parseP3(String encoded);
        //P4 parseP4(String encoded);
    }
}
