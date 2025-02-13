using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerStatDisplay : MonoBehaviour
{
    public PlayerState PlayerStat;
    public Transform StatParent;
    public List<PlayerState> TheState;
    public void generatePlayer()
    {
        Debug.Log("your stat my lord ");
    }

    public void togelStatPanel(GameObject statPanal)
    {
        if (!statPanal.activeSelf)
        {
            statPanal.SetActive(true);
        }
        else
        {
            //foreach(PlayerState state in TheState)
            //{
            //    Destroy(state.gameObject);
            //}
            //TheState.Clear();
            statPanal.SetActive(false);

        }
    }
}
