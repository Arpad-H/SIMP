package de.portalgremlins.network.connection.dummy;

import java.util.Arrays;

import de.portalgremlins.game.Resonance;
import de.portalgremlins.network.connection.message.MessageProtocol;

public class DummyProtocol implements MessageProtocol {
    @Override
    public void selectElements(Resonance... threeResonances) {
        System.out.println("Client selects resonances for blind pick: " + Arrays.toString(threeResonances));
    }

    @Override
    public void selectNextDraftElement(Resonance resonance) {
        System.out.println("Client selects next resonance in draft pick: " + resonance);
    }

    @Override
    public void playCard(String card) {

    }
}
