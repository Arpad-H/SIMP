package de.portalgremlins.game.lobby;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

import de.portalgremlins.game.GameSession;
import de.portalgremlins.game.Resonance;

public class BlindPick implements LobbyPhase {
    private final List<Resonance> selectedResonances = new ArrayList<>();

    @Override
    public void resetLobbyState() {
        selectedResonances.clear();
    }

    @Override
    public boolean setPlayerReady() {
        if (getAmountSelectedResonances() != 3) {
            return false;
        }
        GameSession.getCurrentGameSession().getConnection().protocol().selectElements(selectedResonances.stream().limit(3).toArray(Resonance[]::new));
        return true;
    }

    public void pickResonance(Resonance resonance) {
        if (!canStillPick() || hasAlreadyPicked(resonance)) {
            return;
        }
        selectedResonances.add(resonance);
    }

    public void unpickResonance(Resonance resonance) {
        if (!hasAlreadyPicked(resonance)) {
            return;
        }
        selectedResonances.remove(resonance);
    }

    public Resonance getPickedResonanceBySlot(int pickIndex) {
        return selectedResonances.get(pickIndex);
    }

    public int getAmountSelectedResonances() {
        return selectedResonances.size();
    }

    public boolean hasAlreadyPicked(Resonance resonance) {
        return selectedResonances.contains(resonance);
    }

    public boolean canStillPick() {
        return selectedResonances.size() < 3;
    }
}
