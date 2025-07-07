using UnityEngine;

public class TradeConfirmDisplay : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void OnTapConfirmTrade()
    {
        TradeManager.Instance.TradeSelectedResource();
    }
}
