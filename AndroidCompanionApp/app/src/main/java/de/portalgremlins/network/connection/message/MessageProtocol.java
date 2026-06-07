package de.portalgremlins.network.connection.message;

import de.portalgremlins.game.Resonance;

public interface MessageProtocol {

    void selectElements(Resonance... threeResonances);

    void selectNextDraftElement(Resonance resonance);

    void playCard(String card);

}
