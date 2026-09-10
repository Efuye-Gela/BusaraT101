using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Player))]
public sealed class BotPlayerController : MonoBehaviour
{
    private Player player;
    private float nextStep;
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
        nextStep = Time.unscaledTime + (powers != null && powers.IsBusy ? .25f : 1f);
        try
        {
            LastDecision = HeuristicBot.Analyze(actor);
            // Normal handlers end the action/turn; a draw needs a subsequent placement step.
            LastOutcome = HeuristicBot.Execute(LastDecision);
            ExecutedSteps++;
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

    private void Fail(string message)
    {
        Paused = true;
        Failed = true;
        Status = "Bot stopped: " + message;
        Debug.LogWarning(Status, this);
    }
}
