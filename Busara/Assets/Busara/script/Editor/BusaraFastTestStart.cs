using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[Serializable]
internal sealed class BusaraFastStartRequest
{
    public string id;
    public string[] names;
    public string[] kingdomGuids;
    public bool[] botControlled;
    public long deadlineTicks;
    public string phase;

    internal static BusaraFastStartRequest Create(IReadOnlyList<PlayerSetupEntry> entries,
        KingdomCatalog catalog, int capacity, DateTime now)
    {
        if (!PlayerSetupRules.Validate(entries, catalog, capacity, out string error))
            throw new InvalidOperationException(error);
        var request = new BusaraFastStartRequest
        {
            id = Guid.NewGuid().ToString("N"),
            names = entries.Select(entry => entry.Name.Trim()).ToArray(),
            botControlled = entries.Select(entry => entry.IsBotControlled).ToArray(),
            kingdomGuids = entries.Select(entry =>
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.Kingdom))).ToArray(),
            deadlineTicks = now.AddSeconds(90).Ticks,
            phase = "Waiting"
        };
        if (request.kingdomGuids.Any(string.IsNullOrEmpty) || entries.Where((entry, index) =>
            AssetDatabase.LoadAssetAtPath<Kingdom>(AssetDatabase.GUIDToAssetPath(request.kingdomGuids[index])) !=
            entry.Kingdom).Any())
            throw new InvalidOperationException("Fast Test Start requires saved kingdom assets.");
        return request;
    }

    internal List<PlayerSetupEntry> Resolve(KingdomCatalog catalog, int capacity)
    {
        if (string.IsNullOrEmpty(id) || names == null || kingdomGuids == null || botControlled == null ||
            botControlled.Length != names.Length ||
            names.Length != kingdomGuids.Length || kingdomGuids.Any(string.IsNullOrEmpty))
            throw new InvalidOperationException("The saved Fast Test Start request is incomplete.");
        var entries = names.Select((name, index) => new PlayerSetupEntry(name,
            AssetDatabase.LoadAssetAtPath<Kingdom>(AssetDatabase.GUIDToAssetPath(kingdomGuids[index])), botControlled[index])).ToList();
        if (!PlayerSetupRules.Validate(entries, catalog, capacity, out string error))
            throw new InvalidOperationException(error);
        return entries;
    }

    internal bool HasExpired(DateTime now) => now.Ticks >= deadlineTicks;
    internal void Save(string key) => SessionState.SetString(key, JsonUtility.ToJson(this));
    internal static BusaraFastStartRequest Load(string key)
    {
        string json = SessionState.GetString(key, "");
        return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<BusaraFastStartRequest>(json);
    }
}

[InitializeOnLoad]
internal static class BusaraFastTestStart
{
    internal const string ScenePath = "Assets/Scenes/GameScene.unity";
    private const string RequestKey = "Busara.FastTestStart.Request";
    private const string StatusKey = "Busara.FastTestStart.Status";
    private const string ErrorKey = "Busara.FastTestStart.Error";
    private const string CompletedKey = "Busara.FastTestStart.Completed";

    internal static bool IsPending => !string.IsNullOrEmpty(SessionState.GetString(RequestKey, ""));
    internal static string Status => SessionState.GetString(StatusKey, "");
    internal static bool Failed => SessionState.GetBool(ErrorKey, false);
    internal static string CompletedRequestId => SessionState.GetString(CompletedKey, "");

    static BusaraFastTestStart()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += PlayModeChanged;
    }

    internal static void Begin(IReadOnlyList<PlayerSetupEntry> entries)
    {
        if (IsPending)
            throw new InvalidOperationException("Fast Test Start is already pending.");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode))
            throw new InvalidOperationException("Wait for the Editor to finish its current transition.");
        RequireGameScene();
        if (EditorApplication.isPaused)
            throw new InvalidOperationException("Resume Play Mode before using Fast Test Start.");
        if (!EditorApplication.isPlaying && EditorSceneManager.playModeStartScene != null &&
            AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) != ScenePath)
            throw new InvalidOperationException("The Play Mode start scene override must be GameScene or unset.");
        PlayerManager manager = Object.FindFirstObjectByType<PlayerManager>();
        if (manager == null || !InGameScene(manager) || !manager.requirePlayerSetup)
            throw new InvalidOperationException("GameScene needs its player manager and player setup enabled.");
        if (EditorApplication.isPlaying)
            RequireAwaitingSetup(manager);
        BusaraFastStartRequest request = BusaraFastStartRequest.Create(entries,
            manager.kingdomCatalog, manager.SupportedPlayerCount, DateTime.UtcNow);
        SessionState.EraseString(CompletedKey);
        request.Save(RequestKey);
        SetStatus("Fast Test Start: waiting for GameScene to initialize (90-second limit).");
        if (!EditorApplication.isPlaying)
        {
            try
            {
                EditorApplication.EnterPlaymode();
            }
            catch (InvalidOperationException exception)
            {
                Finish("Fast Test Start failed to enter Play Mode: " + exception.Message, true);
                throw;
            }
            catch (UnityException exception)
            {
                Finish("Fast Test Start failed to enter Play Mode: " + exception.Message, true);
                throw;
            }
        }
    }

    internal static void Cancel()
    {
        if (IsPending)
            Finish("Fast Test Start cancelled. Any already-applied runtime setup is left intact.", true);
    }

    private static void PlayModeChanged(PlayModeStateChange state)
    {
        if (IsPending && (state == PlayModeStateChange.ExitingPlayMode ||
            state == PlayModeStateChange.EnteredEditMode))
            Finish("Fast Test Start cancelled because Play Mode stopped or failed to start.", true);
    }

    private static void Update()
    {
        if (!IsPending)
            return;
        try
        {
            BusaraFastStartRequest request = BusaraFastStartRequest.Load(RequestKey);
            if (request == null || request.HasExpired(DateTime.UtcNow))
                throw new InvalidOperationException("Initialization timed out. Check the Console and GameScene setup.");
            if (!EditorApplication.isPlaying || !Application.isPlaying || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPaused)
                return;
            RequireGameScene();
            PlayerManager manager = PlayerManager.Instance;
            if (request.phase == "Starting" || request.phase == "Finishing")
                throw new InvalidOperationException("Startup was interrupted during a mutation; inspect the game before retrying.");
            if (!ManagersReady(manager))
                return;
            if (request.phase == "Waiting")
            {
                RequireAwaitingSetup(manager);
                PlayerSetupUI[] setups = Object.FindObjectsByType<PlayerSetupUI>(FindObjectsSortMode.None);
                if (setups.Length == 0)
                    return;
                if (setups.Length != 1 || !InGameScene(setups[0]) || setups[0].gameObject != manager.gameObject)
                    throw new InvalidOperationException("Expected exactly one player setup on GameScene's player manager.");
                if (setups[0].Entries.Count < PlayerSetupRules.MinimumPlayers)
                    return;
                List<PlayerSetupEntry> entries = request.Resolve(manager.kingdomCatalog, manager.SupportedPlayerCount);
                request.phase = "Starting";
                request.Save(RequestKey);
                SetStatus("Fast Test Start: starting configured players through the game setup.");
                BusaraPlaytestTools.StartGame(entries);
                request.phase = "Resources";
                request.Save(RequestKey);
                return;
            }
            if (request.phase != "Resources")
                throw new InvalidOperationException("The saved Fast Test Start phase is invalid.");
            if (!manager.IsSetupComplete || !TurnManager.Instance.TurnsStarted ||
                TurnManager.Instance.ActivePlayer == null)
                throw new InvalidOperationException("Player setup did not initialize the first turn.");
            if (manager.Players == null || manager.Players.Count != request.names.Length ||
                manager.Players.Any(player => player == null))
                throw new InvalidOperationException("Participating players changed during startup.");
            List<PlayerSetupEntry> configured = request.Resolve(manager.kingdomCatalog, manager.Players.Count);
            for (int i = 0; i < configured.Count; i++)
                if (manager.Players[i].Name != configured[i].Name || manager.Players[i].Kingdom != configured[i].Kingdom ||
                    manager.Players[i].IsBotControlled != configured[i].IsBotControlled)
                    throw new InvalidOperationException("The configured game no longer matches the startup request.");
            request.phase = "Finishing";
            request.Save(RequestKey);
            SetStatus("Fast Test Start: placing actual setup-card resources in legal non-adjacent spaces.");
            BusaraPlaytestTools.FinishResourceSetup();
            if (manager.Players.Any(player => !player.hasFinishedSettingUp || player.setUpCard != null) ||
                BusaraPlaytestTools.ActionStateName != "None")
                throw new InvalidOperationException("Resource setup did not reach a completed gameplay state.");
            SessionState.SetString(CompletedKey, request.id);
            Finish("Fast Test Start complete. Players and legal resource setup are ready in Gameplay.", false);
        }
        catch (InvalidOperationException exception)
        {
            Finish("Fast Test Start failed: " + exception.Message, true);
        }
        catch (ArgumentException exception)
        {
            Finish("Fast Test Start failed: " + exception.Message, true);
        }
        catch (UnityException exception)
        {
            Finish("Fast Test Start failed: " + exception.Message, true);
        }
    }

    private static void RequireGameScene()
    {
        if (SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != ScenePath ||
            !SceneManager.GetActiveScene().isLoaded)
            throw new InvalidOperationException("Open GameScene alone first. Fast Test Start never replaces scenes or discards unsaved work.");
    }

    private static bool InGameScene(Component component) =>
        component != null && component.gameObject.scene == SceneManager.GetActiveScene() &&
        component.gameObject.scene.path == ScenePath;

    private static bool ManagersReady(PlayerManager manager) =>
        InGameScene(manager) && InGameScene(TurnManager.Instance) && InGameScene(ActionManager.Instance) &&
        InGameScene(BoardManager.Instance) && InGameScene(SelectionManager.Instance) &&
        InGameScene(DeckManager.Instance) && InGameScene(PowerManager.Instance);

    private static void RequireAwaitingSetup(PlayerManager manager)
    {
        if (!manager.IsAwaitingSetup || (TurnManager.Instance != null &&
            (TurnManager.Instance.TurnsStarted || TurnManager.Instance.ActivePlayer != null)))
            throw new InvalidOperationException("A game has already started. Fast Test Start will not restart or replace it.");
    }

    private static void SetStatus(string status)
    {
        SessionState.SetString(StatusKey, status);
        SessionState.SetBool(ErrorKey, false);
    }

    private static void Finish(string status, bool failed)
    {
        SessionState.EraseString(RequestKey);
        SetStatus(status);
        SessionState.SetBool(ErrorKey, failed);
        if (failed)
            Debug.LogWarning(status);
    }
}
