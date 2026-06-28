using TMPro;
using UnityEngine;
using UnityEngine.Events;
public class GameOver : MonoBehaviour
{
    public GameObject SquirrelWin;
    public GameObject SquirrelLose;
    public GameObject LumberjackWin;
    public GameObject LumberjackLose;
    public TextMeshProUGUI squirrelScore;
    public TextMeshProUGUI lumberjackScore;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ShowGameOver(int squirrelScore, int lumberjackScore)
    {
        if (squirrelScore > lumberjackScore)
        {
            SquirrelWin.SetActive(true);
            LumberjackLose.SetActive(true);
            SquirrelLose.SetActive(false);
            LumberjackWin.SetActive(false);
        }
        else if (squirrelScore < lumberjackScore)
        {
            SquirrelLose.SetActive(true);
            LumberjackWin.SetActive(true);
            SquirrelWin.SetActive(false);
            LumberjackLose.SetActive(false);
        }
        else
        {
            SquirrelWin.SetActive(true);
            LumberjackWin.SetActive(true);
            SquirrelLose.SetActive(false);
            LumberjackLose.SetActive(false);
        }
        this.squirrelScore.text = "SCORE: " + squirrelScore;
        this.lumberjackScore.text = "SCORE: " + lumberjackScore;
    }
}
