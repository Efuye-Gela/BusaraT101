using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// In the Web build the main menu loads first. An invitation link (#invite=...) or a resumed match (?match=...)
/// belongs to the online scene, so this sends those visits straight there.
/// </summary>
public class OnlineLinkRedirect : MonoBehaviour
{
    public string onlineScene = "OnlineMVP";

    private void Awake()
    {
        if (Application.platform != RuntimePlatform.WebGLPlayer)
            return;
        string url = Application.absoluteURL ?? "";
        if ((url.Contains("#invite=") || url.Contains("match=")) && Application.CanStreamedLevelBeLoaded(onlineScene))
            SceneManager.LoadScene(onlineScene);
    }
}
