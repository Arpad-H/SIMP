using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class MainMenu : MonoBehaviour
{
    public GameObject SettingsPanel;
    public GameObject CreditsPanel;
    public GameObject readyButton;
    public GameObject cancelButton;
    public GameObject ControllPanel;
    public GameObject qrCodeConnection;
    
    public TextMeshProUGUI playButtonText;

    [Header("Companion app")]
    [Tooltip("Invoked when the phone companion app connects (the /hello OSC signal relayed by OSCReceiver).")]
    public UnityEvent onAppConnected;

    private bool vrPlayerReady = false;

    private void OnEnable()
    {
        OSCReceiver.OnAppConnected += AppConnected;
    }

    private void OnDisable()
    {
        OSCReceiver.OnAppConnected -= AppConnected;
    }

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
    public void ShowControlls()
    {
        ControllPanel.SetActive(true);
    }
    public void HideControlls()
    {
         ControllPanel.SetActive(false);
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

    /// <summary>
    /// Called when the phone companion app connects, via the /hello signal relayed by
    /// <see cref="OSCReceiver.OnAppConnected"/>. Wire menu reactions onto <see cref="onAppConnected"/>
    /// in the Inspector (e.g. show a "controller connected" message or enable the ready button).
    /// </summary>
    public void AppConnected()
    {
        qrCodeConnection.SetActive(false);
        Debug.Log("Companion app connected");
        onAppConnected?.Invoke();
    }
}
