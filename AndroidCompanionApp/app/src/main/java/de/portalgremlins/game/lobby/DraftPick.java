package de.portalgremlins.game.lobby;

import java.util.Arrays;

import de.portalgremlins.game.Resonance;

public class DraftPick implements LobbyPhase {
    private static final byte[] PICK_ORDER_FIRST_PICK = new byte[]{0, 3, 4};
    private static final byte[] PICK_ORDER_LAST_PICK = new byte[]{1, 2, 5};

    private byte[] myPickOrders = new byte[3];
    private byte[] enemyPickOrders = new byte[3];
    private Resonance[] pickedResonancesPlayer = new Resonance[3];
    private Resonance[] pickedResonancesEnemy = new Resonance[3];
    private int currentPickIndex = 0;

    public void setup(boolean hasFirstPick) {
        resetLobbyState();
        if (hasFirstPick) {
            myPickOrders = PICK_ORDER_FIRST_PICK;
            enemyPickOrders = PICK_ORDER_LAST_PICK;
        } else {
            enemyPickOrders = PICK_ORDER_FIRST_PICK;
            myPickOrders = PICK_ORDER_LAST_PICK;
        }
    }

    @Override
    public void resetLobbyState() {
        System.out.println("RESET DRAFT LOBBY STATE");
        Arrays.fill(this.pickedResonancesEnemy, null);
        Arrays.fill(this.pickedResonancesPlayer, null);
        currentPickIndex = 0;
    }

    @Override
    public boolean setPlayerReady() {
        System.out.println("Send DRAFT READY TO SERVER");
        return true;
    }

    public Resonance[] getPickedResonancesPlayer() {
        return pickedResonancesPlayer;
    }

    public Resonance[] getPickedResonancesEnemy() {
        return pickedResonancesEnemy;
    }

    public byte[] getMyPickOrders() {
        return myPickOrders;
    }

    public int getCurrentPickIndex() {
        return currentPickIndex;
    }

    public int getAndIncrementCurrentPickIndex() {
        return currentPickIndex++;
    }
}
