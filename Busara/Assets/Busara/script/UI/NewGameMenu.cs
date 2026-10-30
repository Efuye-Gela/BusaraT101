using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The main menu's New Game screen: how many bots to play against, how strong they are and whether their kingdoms
/// are hidden, then Start Game opens the kingdom selection (player setup) with those bots seated. Every control is authored; each option button has an
/// inactive "Selected" fill that this shows for the chosen option.
/// </summary>
public class NewGameMenu : MonoBehaviour
{
    public GameObject mainColumn;
    public GameObject newGameColumn;
    [Tooltip("1, 2 and 3 bots, in order.")]
    public List<UnityEngine.UI.Button> opponentButtons = new List<UnityEngine.UI.Button>();
    [Tooltip("Easy, Normal and Hard, in order.")]
    public List<UnityEngine.UI.Button> strengthButtons = new List<UnityEngine.UI.Button>();
    [Tooltip("Choose and Hidden, in order.")]
    public List<UnityEngine.UI.Button> kingdomButtons = new List<UnityEngine.UI.Button>();
    [Tooltip("Hidden in the Web build, where a page cannot quit.")]
    public GameObject quitButton;
    public UnityEngine.UI.Button startButton;
    public UnityEngine.UI.Button backButton;
    public string gameScene = "GameScene";
    public Color labelColour = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
    public Color selectedLabelColour = new Color32(0x2A, 0x16, 0x06, 0xFF);

    private int bots = 1;
    private BotStrength strength = BotStrength.Normal;
    private bool hiddenKingdoms = true;

    private void Awake()
    {
        for (int i = 0; i < opponentButtons.Count; i++)
        {
            int count = i + 1;
            opponentButtons[i].onClick.AddListener(() => { bots = count; Refresh(); });
        }
        for (int i = 0; i < strengthButtons.Count; i++)
        {
            var chosen = (BotStrength)i;
            strengthButtons[i].onClick.AddListener(() => { strength = chosen; Refresh(); });
        }
        for (int i = 0; i < kingdomButtons.Count; i++)
        {
            bool hidden = i == 1;
            kingdomButtons[i].onClick.AddListener(() => { hiddenKingdoms = hidden; Refresh(); });
        }
        if (quitButton != null && Application.platform == RuntimePlatform.WebGLPlayer)
            quitButton.SetActive(false);
        startButton.onClick.AddListener(StartGame);
        backButton.onClick.AddListener(Close);
        Refresh();
    }

    public void Open()
    {
        mainColumn.SetActive(false);
        newGameColumn.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        newGameColumn.SetActive(false);
        mainColumn.SetActive(true);
    }

    public void StartGame()
    {
        GameLaunch.Configure(bots, strength, hiddenKingdoms);
        SceneManager.LoadScene(gameScene);
    }

    private void Refresh()
    {
        for (int i = 0; i < opponentButtons.Count; i++)
            Show(opponentButtons[i], i + 1 == bots);
        for (int i = 0; i < strengthButtons.Count; i++)
            Show(strengthButtons[i], (BotStrength)i == strength);
        for (int i = 0; i < kingdomButtons.Count; i++)
            Show(kingdomButtons[i], (i == 1) == hiddenKingdoms);
    }

    private void Show(UnityEngine.UI.Button button, bool selected)
    {
        Transform fill = button.transform.Find("Selected");
        if (fill != null)
            fill.gameObject.SetActive(selected);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = selected ? selectedLabelColour : labelColour;
    }
}
