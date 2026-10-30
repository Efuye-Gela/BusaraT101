using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens another scene from an authored button (wired to <see cref="Open"/> in the inspector). Optionally hides
/// the button when the scene is not part of the build, as in builds without the online mode.
/// </summary>
public class SceneLink : MonoBehaviour
{
    public string sceneName;
    [Tooltip("Hide this object when the scene is not in the build.")]
    public bool hideWhenUnavailable = true;

    private void Awake()
    {
        if (hideWhenUnavailable && !Application.CanStreamedLevelBeLoaded(sceneName))
            gameObject.SetActive(false);
    }

    public void Open()
    {
        SceneManager.LoadScene(sceneName);
    }
}
