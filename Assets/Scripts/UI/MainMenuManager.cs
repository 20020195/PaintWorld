using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Main Menu scene controller.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public Button startButton;
    public Button quitButton;
    public Text titleText;
    public Text subtitleText;

    void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
    }

    void OnStartClicked()
    {
        SceneManager.LoadScene("PictureSelect");
    }

    void OnQuitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
