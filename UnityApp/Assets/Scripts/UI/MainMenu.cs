using TMPro;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public GameObject SettingsPanel;
    public GameObject CreditsPanel;
    public GameObject readyButton;
    public GameObject cancelButton;
    public TextMeshProUGUI playButtonText;
    private bool vrPlayerReady = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playButtonText.text = "[VR PLAYER NOT READY]";
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public void StartGame() 
    {
        // Load the next scene in the build index
        if (vrPlayerReady) UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void QuitGame()
    {
       //exit the application
        Application.Quit();
    }

    public void ShowSettings()
    {
        
    }

    public void ShowCredits()
    {
        
    }
    public void VRPlayerReady()
    {
        vrPlayerReady = true;
        readyButton.SetActive(false);
        cancelButton.SetActive(true);
        playButtonText.text = "[PLAY GAME]";
    }
    public void VRPlayerNotReady()
    {
        vrPlayerReady  = false;
        readyButton.SetActive(true);
        cancelButton.SetActive(false);
        playButtonText.text = "[VR PLAYER NOT READY]";
    }
}
