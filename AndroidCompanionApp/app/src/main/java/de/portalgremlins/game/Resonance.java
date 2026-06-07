package de.portalgremlins.game;

public enum Resonance {
    PLAGUE("1", "Plague", "#3d264a"),
    HOLY("2", "Holy", "#b0aa30"),
    DEATH("3", "Death", "#400118"),
    PSYCHIC("4", "Psychic", "#090fbd"),
    LIFE("5", "Life", "#00961e"),
    DARKNESS("6", "Darkness", "#141414"),
    ;
    private final String id;
    private final String name;
    private final String dominantColorCode;

    Resonance(String id, String name, String dominantColorCode) {
        this.id = id;
        this.name = name;
        this.dominantColorCode = dominantColorCode;
    }

    public String getDominantColorCode() {
        return dominantColorCode;
    }

    public String getId() {
        return id;
    }

    public String getName() {
        return name;
    }
}
