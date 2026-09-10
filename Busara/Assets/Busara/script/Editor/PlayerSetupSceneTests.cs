using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class PlayerSetupSceneTests
{
    private const string SceneBackupKey = "Busara.PlayerSetupSceneTests.SceneBackup";

    [UnityTest]
    public IEnumerator MenuPlayRequiresSetupBeforeResourceTurns()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False, "Save scene changes before running the scene test.");
        SessionState.SetString(SceneBackupKey, JsonUtility.ToJson(new SceneBackup
        {
            Scenes = EditorSceneManager.GetSceneManagerSetup().Select(scene => new SceneRecord
            {
                Path = scene.path, Loaded = scene.isLoaded, Active = scene.isActive
            }).ToArray()
        }));
        EditorSceneManager.OpenScene("Assets/Scenes/mainMenu.unity");
        yield return new EnterPlayMode();
        Button play = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(button => button.gameObject.name == "Play");
        play.onClick.Invoke();
        yield return null;
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("GameScene"));
        PlayerManager manager = PlayerManager.Instance;
        TurnManager turns = TurnManager.Instance;
        PlayerSetupUI setup = Object.FindFirstObjectByType<PlayerSetupUI>();
        Assert.That(setup, Is.Not.Null);
        Assert.That(manager.SupportedPlayerCount, Is.EqualTo(4));
        Assert.That(turns.ActivePlayer, Is.Null);
        Assert.That(turns.TurnsStarted, Is.False);
        Assert.That(manager.IsAwaitingSetup, Is.True);
        Assert.That(setup.Entries, Has.Count.EqualTo(2));
        Button start = setup.GetComponentsInChildren<Button>().Single(button => button.name == "Start Game");
        Assert.That(start.interactable, Is.False);
        Button add = setup.GetComponentsInChildren<Button>().Single(button => button.name == "Add Player");
        add.onClick.Invoke();
        add.onClick.Invoke();
        yield return null;
        Assert.That(setup.Entries, Has.Count.EqualTo(4));
        Assert.That(add.interactable, Is.False);
        setup.GetComponentsInChildren<Button>().First(button => button.name == "Remove Player").onClick.Invoke();
        yield return null;
        Assert.That(setup.Entries, Has.Count.EqualTo(3));
        TMP_InputField[] names = setup.GetComponentsInChildren<TMP_InputField>();
        TMP_Dropdown[] kingdoms = setup.GetComponentsInChildren<TMP_Dropdown>();
        for (int i = 0; i < 3; i++)
        {
            names[i].text = $"Configured {i + 1}";
            Assert.That(kingdoms[i].options, Has.Count.EqualTo(16));
            kingdoms[i].value = 15 - i;
        }
        kingdoms[0].Show();
        yield return null;
        Assert.That(setup.GetComponentsInChildren<Toggle>().Length, Is.GreaterThanOrEqualTo(15));
        kingdoms[0].Hide();
        yield return null;
        Assert.That(start.interactable, Is.True);
        var chosenKingdoms = setup.Entries.Select(entry => entry.Kingdom).ToArray();
        var slotsBefore = manager.Players.Take(3).SelectMany(player => player.Board.Slots)
            .Select(slot => slot.Index).ToArray();
        Canvas.ForceUpdateCanvases();
        var boardPositions = manager.Players.Take(3).Select(player => player.Board.transform.localPosition).ToArray();
        start.onClick.Invoke();
        yield return null;
        Assert.That(manager.IsAwaitingSetup, Is.False);
        Assert.That(manager.Players, Has.Count.EqualTo(3));
        Assert.That(turns.ActivePlayer, Is.SameAs(manager.Players[0]));
        Assert.That(manager.Players[0].Name, Is.EqualTo("Configured 1"));
        CollectionAssert.AreEqual(chosenKingdoms, manager.Players.Select(player => player.Kingdom));
        CollectionAssert.AreEquivalent(slotsBefore, BoardManager.slots.Select(slot => slot.Index));
        Canvas.ForceUpdateCanvases();
        CollectionAssert.AreEqual(boardPositions, manager.Players.Select(player => player.Board.transform.localPosition));
        Assert.That(manager.Players.All(player => !player.hasFinishedSettingUp && player.setUpCard != null), Is.True);
        Assert.That(new SerializedObject(ActionManager.Instance).FindProperty("_currentActionState").enumValueIndex,
            Is.EqualTo((int)ActionManager.ActionState.ResourceSetup));
        Assert.That(setup.GetComponentsInChildren<Canvas>(), Is.Empty);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying)
            yield return new ExitPlayMode();
        string backup = SessionState.GetString(SceneBackupKey, "");
        SessionState.EraseString(SceneBackupKey);
        if (string.IsNullOrEmpty(backup))
            yield break;
        SceneSetup[] scenes = JsonUtility.FromJson<SceneBackup>(backup).Scenes.Select(scene => new SceneSetup
        {
            path = scene.Path, isLoaded = scene.Loaded, isActive = scene.Active
        }).ToArray();
        if (scenes.Length > 0 && scenes.All(scene => !string.IsNullOrEmpty(scene.path)))
            EditorSceneManager.RestoreSceneManagerSetup(scenes);
        else
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [System.Serializable]
    private class SceneBackup
    {
        public SceneRecord[] Scenes;
    }

    [System.Serializable]
    private class SceneRecord
    {
        public string Path;
        public bool Loaded;
        public bool Active;
    }
}
