using UnityEngine;

public class ActionManager : Manager<ActionManager>, TurnManager.TurnEndListener
{
    public enum ActionState
    {
        None,
        DrewCard,
        Forged,
        UsedWeapon,
        Traded,
        UsedPower,
        MoveResource,
        ResourceSetup
    }
    [SerializeField]private ActionState _currentActionState = ActionState.None;
    public ActionState CurrentState => _currentActionState;
    private void OnEnable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.AddTurnEndListeners(this);
    }
    private void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.RemoveTurnEndListener(this);
    }
    public bool CanPerformAction()
    {
        if (HardWinterDisaster.Active != null)
            return false;
        if (PowerManager.Instance != null && PowerManager.Instance.IsBusy)
            return false;
        if(_currentActionState == ActionState.None)
        {
            return true;
        }
        //DisplayManager.Instance.Deliver("You have already performed an action this turn.");
        Debug.Log("An action has already been performed this turn");
        return false;
    }

    public void SetAction(ActionState action)
    {
        if(_currentActionState == ActionState.None)
        {
            _currentActionState = action;
        }
    }

    public void ResetActionState()
    {
        _currentActionState = ActionState.None ;
    }

    public void OnTurnEnd()
    {
        // Reset the action state for the next player's turn.
        _currentActionState = ActionState.None;
        Debug.Log("ActionManager state has been reset.");
    }
}
