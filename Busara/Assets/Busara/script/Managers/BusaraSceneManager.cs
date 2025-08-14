using UnityEngine;
using UnityEngine.SceneManagement;

public class BusaraSceneManager : MonoBehaviour
{
    public void LoadScene(int index)
    {
        SceneManager.LoadScene(index);
    }
    public void MakeFullScreen()
    {
        Screen.fullScreen = true;
        //Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, true);
    }
    public void UnMakeFullScreen()
    {
        Screen.fullScreen = false;
    }
    public void Quit()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.ExternalEval("window.close();");
#else
        Application.Quit();
#endif
    }
}
