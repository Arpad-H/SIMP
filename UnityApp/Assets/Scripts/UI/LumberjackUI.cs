using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Live scoreboard for the lumberjack (the VR player), the mirror of <see cref="squirrelUI"/>. Reads
/// everything from the <see cref="GameManager"/> and refreshes whenever a nut is eaten or collected
/// (via <see cref="GameManager.onScoreChanged"/>):
/// <list type="bullet">
///   <item>the score label shows the lumberjack's running score;</item>
///   <item>the nut label shows how many nuts have been taken out of play so far — the squirrel's
///   eaten nuts plus the lumberjack's collected nuts — out of the round's total, e.g. "7/12".</item>
/// </list>
/// Drop this on the wrist UI canvas and assign the two labels; no manual event wiring is needed.
/// Pair it with <see cref="WristUI"/> for the glance-to-reveal behaviour.
/// </summary>
public class LumberjackUI : MonoBehaviour
{
    [Tooltip("Label that shows nuts taken so far / total nuts this round (squirrel eaten + lumberjack collected).")]
    public TextMeshProUGUI nutCount;

    [Tooltip("Label that shows the lumberjack's running score.")]
    public TextMeshProUGUI scoreText;

    private GameManager gameManager;
    private bool subscribed;

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private IEnumerator Start()
    {
        // Nuts register themselves over the first frame (their spawners create them in Start), and the
        // GameManager may wake after us. Wait one frame, then make sure we're hooked up and showing the
        // real total rather than 0/0.
        yield return null;
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    // Find the manager (once it exists) and listen for score/nut changes — guarded so we subscribe
    // exactly once even though both OnEnable and the delayed Start may call in.
    private void Subscribe()
    {
        if (subscribed)
            return;

        if (gameManager == null)
            gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        gameManager.onScoreChanged.AddListener(Refresh);
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || gameManager == null)
            return;

        gameManager.onScoreChanged.RemoveListener(Refresh);
        subscribed = false;
    }

    /// <summary>Pull the latest score and nut tally from the manager onto the labels.</summary>
    public void Refresh()
    {
        if (gameManager == null)
            return;

        if (scoreText != null)
            scoreText.text = "Score: " + gameManager.LumberjackScore;

        if (nutCount != null)
            nutCount.text = gameManager.NutsTaken + "/" + gameManager.TotalNuts;
    }
}
