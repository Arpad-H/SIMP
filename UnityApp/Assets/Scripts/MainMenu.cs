using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public GameObject SettingsPanel;
    public GameObject CreditsPanel;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public void StartGame() 
    {
        // Load the next scene in the build index
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1);
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
}
