using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class BusaraBotWindow : EditorWindow
{
    [SerializeField] private Player player;
    private Vector2 scroll;
    private BotDecision decision;
    private BotDecision observedAutomaticDecision;
    private string feedback = "Analyze a turn to inspect the paths before executing.";
    private MessageType feedbackType = MessageType.Info;
    private readonly List<string> history = new List<string>();

    [MenuItem("Tools/Busara/Bot Decisions")]
    public static void Open() => GetWindow<BusaraBotWindow>("Bot Decisions").Show();

    public void ShowAnalysis(BotDecision value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        player = value.Player;
        BotPlayerController controller = player != null ? player.GetComponent<BotPlayerController>() : null;
        if (controller != null && player.IsBotControlled)
            controller.SetPaused(true);
        observedAutomaticDecision = controller != null ? controller.LastDecision : null;
        decision = value;
        feedback = "Analysis only - game unchanged.";
        feedbackType = MessageType.Info;
        Repaint();
    }

    private void OnEnable()
    {
        minSize = new Vector2(550, 450);
    }

    private void OnInspectorUpdate() => Repaint();

    internal void BindLivePlayer(Player current)
    {
        if (ReferenceEquals(player, current))
            return;
        player = current;
        decision = null;
        observedAutomaticDecision = null;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Heuristic bot - initial version", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("One-step scoring, not exhaustive search. Own-board pair forging/moves, unknown draws, " +
            "revealed resource placement, all kingdom powers/reactions, controlled turns and Hard Winter virtue discards. " +
            "Trade, weapon activation and other disaster discard phases remain manual. " +
            "Scores are utility points, NOT win probabilities.", MessageType.Info);
        PlayerManager manager = PlayerManager.Instance;
        PowerManager powers = PowerManager.Instance;
        if (powers != null && powers.IsBusy)
        {
            PowerDecisionContext context = powers.DecisionContext;
            EditorGUILayout.LabelField("Pending power decision",
                context != null && context.Owner != null ? context.Owner.Name + " / " + context.Kind : "Unknown owner/phase");
        }
        Player[] players = manager == null || manager.Players == null ? Array.Empty<Player>() :
            manager.Players.Where(item => item != null).ToArray();
        if (players.Length == 0)
        {
            EditorGUILayout.HelpBox("Start a game from Playtest Runner > Game Setup.", MessageType.Info);
            return;
        }
        int index = player == null ? -1 : Array.IndexOf(players, player);
        if (index < 0)
            index = 0;
        int selected = EditorGUILayout.Popup("Bot player", index, players.Select(item => item.Name).ToArray());
        // A scene reload can replace the managed wrapper while retaining its Unity instance ID.
        BindLivePlayer(players[selected]);
        BotPlayerController controller = player.GetComponent<BotPlayerController>();
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            bool botControlled = EditorGUILayout.ToggleLeft("Bot controlled (automatic turns; runs with this window closed)", player.IsBotControlled);
            if (botControlled != player.IsBotControlled)
            {
                player.IsBotControlled = botControlled;
                if (botControlled)
                {
                    if (controller == null)
                        controller = player.gameObject.AddComponent<BotPlayerController>();
                    controller.SetPaused(false);
                }
            }
            if (controller != null && player.IsBotControlled)
            {
                bool paused = EditorGUILayout.ToggleLeft("Pause bot for inspection/manual steps", controller.Paused);
                if (paused != controller.Paused)
                    controller.SetPaused(paused);
            }
        }
        if (controller != null)
        {
            EditorGUILayout.LabelField("Automation: " + controller.Status, EditorStyles.wordWrappedLabel);
            if (controller.LastDecision != null && controller.LastDecision != observedAutomaticDecision)
            {
                observedAutomaticDecision = controller.LastDecision;
                decision = controller.LastDecision;
                feedback = controller.Failed ? controller.Status : controller.LastOutcome;
                feedbackType = controller.Failed ? MessageType.Error : MessageType.Info;
            }
        }
        string blocked = HeuristicBot.BlockReason(player);
        if (blocked.Length > 0)
            EditorGUILayout.HelpBox(blocked, MessageType.Warning);
        using (new EditorGUI.DisabledScope(blocked.Length > 0 ||
            (player.IsBotControlled && controller != null && !controller.Paused)))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Analyze only"))
                    Run(() => ShowAnalysis(HeuristicBot.Analyze(player)));
                using (new EditorGUI.DisabledScope(decision == null))
                    if (GUILayout.Button("Execute chosen path"))
                        Run(ExecuteDecision);
                if (GUILayout.Button("Analyze + step"))
                    Run(() => { decision = HeuristicBot.Analyze(player); ExecuteDecision(); });
            }
        }
        EditorGUILayout.HelpBox(feedback, feedbackType);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (decision != null && decision.Chosen != null)
        {
            EditorGUILayout.LabelField($"Last analysis: {decision.PlayerName} / {decision.Candidates.Count} paths", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Chosen: " + decision.Chosen.Path, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField($"Output value: {decision.Chosen.Score:0.##}");
            float minimum = decision.Candidates.Min(candidate => candidate.Score);
            float maximum = decision.Chosen.Score;
            foreach (BotCandidate candidate in decision.Candidates)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField((candidate == decision.Chosen ? "CHOSEN - " : "") + candidate.Path, EditorStyles.wordWrappedLabel);
                Rect bar = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.ProgressBar(bar, maximum == minimum ? 1 : (candidate.Score - minimum) / (maximum - minimum),
                    $"Score: {candidate.Score:0.##}");
                foreach (BotScoreTerm term in candidate.Terms)
                    EditorGUILayout.LabelField($"{term.Value:+0.##;-0.##;0}   {term.Reason}", EditorStyles.wordWrappedMiniLabel);
            }
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Automatic path history (last 20 steps)", EditorStyles.boldLabel);
        if (controller != null)
            foreach (string item in controller.History.Reverse())
                EditorGUILayout.LabelField(item, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Manual path history (last 20 steps)", EditorStyles.boldLabel);
        foreach (string item in history.AsEnumerable().Reverse())
            EditorGUILayout.LabelField(item, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void ExecuteDecision()
    {
        feedback = HeuristicBot.Execute(decision);
        history.Add($"{decision.PlayerName}: {decision.Chosen.Path} | value {decision.Chosen.Score:0.##}");
        if (history.Count > 20)
            history.RemoveAt(0);
    }

    private void Run(Action action)
    {
        try
        {
            action();
            feedbackType = MessageType.Info;
        }
        catch (InvalidOperationException error)
        {
            Fail(error.Message);
        }
        catch (ArgumentException error)
        {
            Fail(error.Message);
        }
    }

    private void Fail(string error)
    {
        if (player != null && player.IsBotControlled)
            player.GetComponent<BotPlayerController>()?.SetPaused(true);
        feedback = error;
        feedbackType = MessageType.Error;
        Debug.LogWarning("Busara bot: " + error);
    }
}
