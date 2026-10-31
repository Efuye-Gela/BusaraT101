using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Player))]
public sealed class BotPlayerController : MonoBehaviour
{
    [Tooltip("Seconds between a bot's actions, so players can see each move.")]
    public float actionDelay = 2.5f;
    [Tooltip("Seconds between setup placements and power choices.")]
    public float stepDelay = 0.9f;

    private Player player;
    private float nextStep;
    private readonly List<Slot> shown = new List<Slot>();
    private readonly List<string> history = new List<string>();

    public bool Paused { get; private set; }
    public bool Failed { get; private set; }
    public string Status { get; private set; } = "Waiting for gameplay.";
    public string LastOutcome { get; private set; } = "";
    public BotDecision LastDecision { get; private set; }
    public IReadOnlyList<string> History => history.AsReadOnly();
    public int ExecutedSteps { get; private set; }

    private void OnEnable()
    {
        player = GetComponent<Player>();
        nextStep = Time.unscaledTime + 1;
    }

    public void SetPaused(bool paused)
    {
        Paused = paused;
        if (!paused)
        {
            Failed = false;
            nextStep = Time.unscaledTime + 1;
        }
        Status = paused ? "Bot paused." : "Bot resumed; waiting for a legal action.";
    }

    private void Update()
    {
        if (!player.IsBotControlled)
        {
            Status = "Player controlled.";
            return;
        }
        if (Paused || Time.timeScale <= 0)
            return;
        if (SetupTurn())
            return;
        PowerManager powers = PowerManager.Instance;
        Player actor = player;
        if (powers != null && powers.IsBusy)
        {
            if (powers.DecisionContext == null || powers.DecisionContext.Kind == PowerDecisionKind.Unknown)
            {
                Status = "Unrecognized power choice: waiting for manual inspection.";
                return;
            }
            if (powers.DecisionContext.Owner != player)
            {
                Status = $"Waiting for {(powers.DecisionContext.Owner != null ? powers.DecisionContext.Owner.Name : "an assigned owner")}: " +
                    powers.DecisionContext.Kind + ".";
                return;
            }
        }
        else if (powers != null && powers.Controller != null && !TurnManager.Instance.isSpecialCardDrawn)
        {
            if (powers.Controller != player)
            {
                Status = "Waiting for the turn controller.";
                return;
            }
            actor = TurnManager.Instance.ActivePlayer;
        }
        Status = HeuristicBot.BlockReason(actor);
        if (Status.Length > 0 || Time.unscaledTime < nextStep)
            return;
        nextStep = Time.unscaledTime + (powers != null && powers.IsBusy ? stepDelay : actionDelay);
        Unmark();
        try
        {
            LastDecision = HeuristicBot.Analyze(actor);
            // Normal handlers end the action/turn; a draw needs a subsequent placement step.
            BotCandidate chosen = LastDecision.Chosen;
            LastOutcome = HeuristicBot.Execute(LastDecision);
            ExecutedSteps++;
            Announce(actor, chosen);
            history.Add($"{LastDecision.PlayerName}: {LastDecision.Chosen.Path} | value {LastDecision.Chosen.Score:0.##}");
            if (history.Count > 20)
                history.RemoveAt(0);
            Status = LastOutcome;
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

    // The resource setup turn: place the setup card's resources one at a time, then the turn passes on.
    private bool SetupTurn()
    {
        if (player.hasFinishedSettingUp || player.setUpCard == null || TurnManager.Instance == null ||
            TurnManager.Instance.ActivePlayer != player || (PlayerManager.Instance != null && PlayerManager.Instance.IsAwaitingSetup))
            return false;
        Status = "Placing setup resources.";
        if (Time.unscaledTime < nextStep)
            return true;
        nextStep = Time.unscaledTime + stepDelay;
        ResourceCollectionDisplay setup = FindFirstObjectByType<ResourceCollectionDisplay>();
        if (setup == null || !setup.PlaceForBot())
            Fail("Could not place a setup resource.");
        return true;
    }

    // Shows what the bot just did: a message, and the spaces it used stay marked until its next step.
    private void Announce(Player actor, BotCandidate chosen)
    {
        if (chosen == null)
            return;
        string[] parts = chosen.Path.Split(new[] { " -> " }, StringSplitOptions.None);
        // Paths read "Move Water #3 -> #4 -> why" and "Forge #1 + #2 -> gain Art -> why".
        string move = parts.Length < 2 ? parts[0]
            : chosen.Kind == BotActionKind.Move ? parts[0] + " to " + parts[1]
            : chosen.Kind == BotActionKind.Forge ? parts[0] + ": " + parts[1]
            : parts[0];
        if (DisplayManager.Instance != null)
            DisplayManager.Instance.DeliverInstructions(actor.Name + ": " + move);
        foreach (int index in new[] { chosen.From, chosen.To })
        {
            Slot slot = index < 0 ? null : BoardManager.slots.Find(item => item != null && item.Index == index);
            if (slot == null)
                continue;
            slot.Highlight();
            shown.Add(slot);
        }
    }

    private void Unmark()
    {
        foreach (Slot slot in shown)
            if (slot != null)
                slot.UnHighlight();
        shown.Clear();
    }

    private void OnDisable() => Unmark();

    private void Fail(string message)
    {
        Paused = true;
        Failed = true;
        Status = "Bot stopped: " + message;
        Debug.LogWarning(Status, this);
    }
}
