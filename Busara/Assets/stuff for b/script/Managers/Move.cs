using UnityEngine;

public abstract class Move : MonoBehaviour
{
    [SerializeField]
    protected GameState gameState;
    
    public abstract bool Validate();
    public abstract void Execute();
    public abstract void Undo();
    
    protected virtual void Start()
    {
        gameState = GameState.Instance;
    }
}

