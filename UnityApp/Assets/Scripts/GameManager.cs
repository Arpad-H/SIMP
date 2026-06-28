using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central scorekeeper and round controller for the squirrel-vs-lumberjack game.
///
/// The two sides compete over the nuts: the squirrel scores by EATING nuts
/// (<see cref="Peanut.Eat"/>), the lumberjack (the VR player) scores by TOUCHING nuts that have
/// fallen to the ground (<see cref="Peanut.Collect"/>). Each side's points-per-nut is configurable.
/// Every nut registers itself when it spawns and is taken out of play when it's eaten, collected or
/// otherwise destroyed; once the last nut is gone the round ends and the <see cref="GameOver"/>
/// screen is shown with both final scores.
///
/// Access it from anywhere via <see cref="Instance"/>. Put exactly one of these in the scene.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scoring")]
    [Tooltip("Points the squirrel earns for each nut it eats.")]
    [SerializeField] private int pointsPerNutEaten = 1;

    [Tooltip("Points the lumberjack earns for each fallen nut they pick up.")]
    [SerializeField] private int pointsPerNutCollected = 1;

    [Header("References")]
    [Tooltip("Game over screen shown when every nut is gone. Found automatically in the scene " +
             "(including inactive objects) if left empty.")]
    [SerializeField] private GameOver gameOverScreen;

    [Header("Events")]
    [Tooltip("Fired whenever either score changes — hook a live scoreboard / SFX here.")]
    public UnityEvent onScoreChanged;

    [Tooltip("Fired once when the round ends, right after the game over screen is shown.")]
    public UnityEvent onGameOver;

    /// <summary>The squirrel's running score.</summary>
    public int SquirrelScore => squirrelScore;
    /// <summary>The lumberjack's running score.</summary>
    public int LumberjackScore => lumberjackScore;
    /// <summary>How many nuts are still in play.</summary>
    public int RemainingNuts => liveNuts.Count;
    /// <summary>True once the round has ended.</summary>
    public bool GameEnded => gameEnded;

    /// <summary>How many nuts the squirrel has eaten so far this round.</summary>
    public int NutsEaten => nutsEaten;
    /// <summary>How many fallen nuts the lumberjack has collected so far this round.</summary>
    public int NutsCollected => nutsCollected;
    /// <summary>Nuts taken out of play so far by either side — eaten plus collected.</summary>
    public int NutsTaken => nutsEaten + nutsCollected;
    /// <summary>Every nut that has entered play this round — the denominator for <see cref="NutsTaken"/>.</summary>
    public int TotalNuts => totalNuts;

    private int squirrelScore;
    private int lumberjackScore;

    // Nut tallies, kept apart from the scores because points-per-nut isn't 1 — these are head counts.
    private int nutsEaten;
    private int nutsCollected;
    private int totalNuts;

    // Every nut currently in play. A set (not a plain counter) so a stray double register/unregister
    // can never corrupt the tally — the round ends precisely when this empties.
    private readonly HashSet<Peanut> liveNuts = new();

    // Guards: don't "end" the round before any nut ever spawned, and don't let the flood of
    // OnDestroy calls during scene teardown / app quit spuriously trigger the game over screen.
    private bool hasSpawnedNuts;
    private bool gameEnded;
    private bool applicationQuitting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[GameManager] A second {nameof(GameManager)} on '{name}' was destroyed; " +
                             "there should only be one in the scene.", this);
            Destroy(this);
            return;
        }
        Instance = this;

        if (gameOverScreen == null)
            gameOverScreen = FindAnyObjectByType<GameOver>(FindObjectsInactive.Include);
    }

    /// <summary>A nut has entered play. Called by <see cref="Peanut"/> when it spawns.</summary>
    public void RegisterNut(Peanut nut)
    {
        if (nut == null) return;
        if (liveNuts.Add(nut))   // Add is true only the first time, so the total counts each nut once.
            totalNuts++;
        hasSpawnedNuts = true;
    }

    /// <summary>The squirrel ate a nut: award its points and take the nut out of play.</summary>
    public void NotifyNutEaten(Peanut nut)
    {
        if (gameEnded) return;
        squirrelScore += pointsPerNutEaten;
        nutsEaten++;
        onScoreChanged?.Invoke();
        ResolveNut(nut);
    }

    /// <summary>The lumberjack picked up a fallen nut: award its points and take it out of play.</summary>
    public void NotifyNutCollected(Peanut nut)
    {
        if (gameEnded) return;
        lumberjackScore += pointsPerNutCollected;
        nutsCollected++;
        onScoreChanged?.Invoke();
        ResolveNut(nut);
    }

    /// <summary>
    /// Safety net for a nut destroyed without being eaten or collected (e.g. scene teardown). A nut
    /// already removed via eat/collect is ignored, so this never double-counts.
    /// </summary>
    public void UnregisterNut(Peanut nut)
    {
        if (applicationQuitting) return;
        ResolveNut(nut);
    }

    // Take a nut out of play, and end the round once the last one is gone.
    private void ResolveNut(Peanut nut)
    {
        if (gameEnded) return;
        if (!liveNuts.Remove(nut)) return; // never registered, or already resolved
        if (hasSpawnedNuts && liveNuts.Count == 0)
            EndGame();
    }

    private void EndGame()
    {
        if (gameEnded) return;
        gameEnded = true;

        Debug.Log($"[GameManager] Game over — squirrel {squirrelScore}, lumberjack {lumberjackScore}.", this);

        if (gameOverScreen != null)
        {
            gameOverScreen.gameObject.SetActive(true);
            gameOverScreen.ShowGameOver(squirrelScore, lumberjackScore);
        }
        else
        {
            Debug.LogWarning("[GameManager] No GameOver screen assigned or found; cannot show results.", this);
        }

        onGameOver?.Invoke();
    }

    private void OnApplicationQuit() => applicationQuitting = true;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
