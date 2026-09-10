using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class HardWinterDisasterSceneTests
{
    private const string BackupKey = "Busara.HardWinterDisasterSceneTests.SceneBackup";
    private string phase;

    [UnityTest]
    public IEnumerator ActualPlayerInfoButtonsResolveDisasterInPlayerOrder()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False, "Do not discard scene edits for this test.");
        SessionState.SetString(BackupKey, JsonUtility.ToJson(new SceneBackup
        {
            Scenes = EditorSceneManager.GetSceneManagerSetup().Select(scene => new SceneRecord
            {
                Path = scene.path, Loaded = scene.isLoaded, Active = scene.isActive
            }).ToArray()
        }));
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        yield return new EnterPlayMode();
        // Allocate captured scenario locals after the domain reload, not in this entry iterator.
        yield return PlayDisasterScenario();
        yield return new ExitPlayMode();
    }

    private IEnumerator PlayDisasterScenario()
    {
        Application.runInBackground = true;
        EditorWindow gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        gameView.Show();
        gameView.Focus();
        for (int frame = 0; frame < 120 && Object.FindFirstObjectByType<PlayerSetupUI>() == null; frame++)
            yield return null;

        PlayerSetupUI setup = Object.FindFirstObjectByType<PlayerSetupUI>();
        Assert.That(setup, Is.Not.Null, "GameScene must initialize player setup before gameplay.");
        TMP_InputField[] names = setup.GetComponentsInChildren<TMP_InputField>();
        TMP_Dropdown[] kingdoms = setup.GetComponentsInChildren<TMP_Dropdown>();
        for (int i = 0; i < 2; i++)
        {
            names[i].text = "Winter player " + (i + 1);
            kingdoms[i].value = i + 1;
        }
        PlayerManager manager = PlayerManager.Instance;
        Assert.That(manager, Is.Not.Null);
        foreach (Player player in manager.Players.Take(2))
        {
            Slot slot = player.Board.Slots[0];
            Resource resource = BoardManager.Instance.SpawnByResourceType(ResourceType.Earth, slot.gameObject)
                .GetComponent<Resource>();
            Board.PlaceResource(resource, slot);
            player.hasFinishedSettingUp = true;
        }
        setup.GetComponentsInChildren<Button>().Single(button => button.name == "Start Game").onClick.Invoke();
        Assert.That(manager.IsSetupComplete, Is.True);
        yield return null;
        Assert.That(Application.isPlaying, Is.True, "The scene walkthrough must remain in Play Mode.");
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("GameScene"));
        Assert.That(manager != null && manager.gameObject.activeInHierarchy, Is.True, "The player manager must remain active.");
        Assert.That(manager.Players, Is.Not.Null, "Configured player list must remain available.");
        Assert.That(manager.Players.Count, Is.EqualTo(2));
        phase = "Reading configured players";
        Player first = manager.Players[0];
        Player second = manager.Players[1];
        phase = "Preparing player inventories";
        foreach (Player player in manager.Players)
        {
            Assert.That(player, Is.Not.Null);
            Assert.That(player.Virtues, Is.Not.Null, "Each player needs a virtue inventory.");
            player.hasFinishedSettingUp = true;
            player.Virtues.Clear();
        }
        Assert.That(first.Kingdom, Is.Not.Null);
        Assert.That(second.Kingdom, Is.Not.Null);
        phase = "Reading kingdom virtue assets";
        Virtue nature = first.Kingdom.virtuesForWin.First(goal => goal.virtues.type == VirtueType.Nature).virtues;
        Virtue art = second.Kingdom.virtuesForWin.First(goal => goal.virtues.type == VirtueType.Art).virtues;
        first.Virtues.AddRange(new[] { nature, nature });
        second.Virtues.Add(art);
        phase = "Resetting actions";
        Assert.That(ActionManager.Instance, Is.Not.Null, "Gameplay action manager must be active.");
        ActionManager.Instance.ResetActionState();
        phase = "Finding the disaster";
        HardWinterDisaster disaster = Object.FindFirstObjectByType<HardWinterDisaster>(FindObjectsInactive.Include);
        Assert.That(disaster, Is.Not.Null, "The authored deck must contain the hard winter effect.");
        Assert.That(DisasterManager.Instance, Is.Not.Null, "Gameplay disaster manager must be active.");
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(first));
        phase = "Triggering the disaster";
        DisasterManager.Instance.TriggerDisaster(disaster);
        yield return null;
        Canvas.ForceUpdateCanvases();

        phase = "Finding visible player cards";
        PlayerInfoCard[] cards = Object.FindObjectsByType<PlayerInfoCard>(FindObjectsSortMode.None);
        Assert.That(cards.Length, Is.EqualTo(2), "Only participating players should be visible.");
        Button close = DisplayManager.Instance.PlayerInfoPanel.GetComponentsInChildren<Button>()
            .Single(button => Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                .Any(index => button.onClick.GetPersistentMethodName(index) == "ClosePlayerInfo"));
        VirtueUI firstRow = cards.Single(card => card.player == first).VirtueUIList
            .Single(row => row.virtueType.type == VirtueType.Nature);
        Assert.That(firstRow.NumberOfvirtues.text, Is.EqualTo("2"));
        Assert.That(cards.SelectMany(card => card.VirtueUIList)
            .Count(row => row.DisasterDiscardButton != null && row.DisasterDiscardButton.gameObject.activeInHierarchy),
            Is.EqualTo(1), "Zero-count and waiting-player rows must not offer a discard.");
        phase = "Clicking the first discard";
        yield return ClickThroughEventSystem(firstRow.DisasterDiscardButton);
        yield return null;
        Assert.That(first.Virtues, Has.Count.EqualTo(1));
        Assert.That(firstRow.NumberOfvirtues.text, Is.EqualTo("1"));
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(second));
        Assert.That(firstRow.DisasterDiscardButton.gameObject.activeSelf, Is.False);
        VirtueUI secondRow = cards.Single(card => card.player == second).VirtueUIList
            .Single(row => row.virtueType.type == VirtueType.Art);
        phase = "Clicking the second discard";
        yield return ClickThroughEventSystem(secondRow.DisasterDiscardButton);
        yield return null;
        Assert.That(second.Virtues, Is.Empty);
        Assert.That(secondRow.NumberOfvirtues.text, Is.EqualTo("0"));
        Assert.That(HardWinterDisaster.Active, Is.Null);
        Assert.That(TurnManager.Instance.isSpecialCardDrawn, Is.False);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(second),
            "Normal play resumes after the original drawer, not after the last discard owner.");
        Assert.That(cards.SelectMany(card => card.VirtueUIList).All(row =>
            row.DisasterDiscardButton == null || !row.DisasterDiscardButton.gameObject.activeSelf), Is.True);
        Assert.That(GameManager.Instance.DrawScreen.activeSelf, Is.False);
        close.onClick.Invoke();
        Assert.That(DisplayManager.Instance.PlayerInfoPanel.activeSelf, Is.False);
    }

    internal static IEnumerator ClickThroughEventSystem(Button button)
    {
        Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True);
        Canvas.ForceUpdateCanvases();
        for (int frame = 0; frame < 120 && button.targetGraphic.depth < 0; frame++)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            yield return null;
        }
        Assert.That(button.targetGraphic.depth, Is.GreaterThanOrEqualTo(0), "Render the new UI before pointer raycasting.");
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits, Is.Not.Empty, "The button must be reachable by a real UI pointer.");
        Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button),
            "Another graphic must not intercept the button: " +
            string.Join("/", hits[0].gameObject.GetComponentsInParent<Transform>().Reverse().Select(item => item.name)));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            Debug.Log("Hard winter scene failure (" + phase + "): " + TestContext.CurrentContext.Result.StackTrace);
        if (Application.isPlaying)
            yield return new ExitPlayMode();
        string json = SessionState.GetString(BackupKey, "");
        SessionState.EraseString(BackupKey);
        if (string.IsNullOrEmpty(json))
            yield break;
        SceneSetup[] scenes = JsonUtility.FromJson<SceneBackup>(json).Scenes.Select(scene => new SceneSetup
        {
            path = scene.Path, isLoaded = scene.Loaded, isActive = scene.Active
        }).ToArray();
        if (scenes.Length > 0 && scenes.All(scene => !string.IsNullOrEmpty(scene.path)))
            EditorSceneManager.RestoreSceneManagerSetup(scenes);
        else
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [System.Serializable]
    private class SceneBackup { public SceneRecord[] Scenes; }

    [System.Serializable]
    private class SceneRecord
    {
        public string Path;
        public bool Loaded;
        public bool Active;
    }
}
