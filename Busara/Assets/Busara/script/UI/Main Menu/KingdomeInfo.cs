using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KingdomeInfo : MonoBehaviour
{
    public TMP_Text KingdomeName;
    public TMP_Text KingdomeDescriptions;
    public TMP_Text powerName;
    public TMP_Text powerDescription;
    public List<KingdomeVirtue> kingdomeVirtues;
    public Kingdom chosenKingdom;
    public static Action<Kingdom> OnKingdomeSelect;
    public void OnKingdomeTap()
    {
        OnKingdomeSelect?.Invoke(chosenKingdom);
    }
}
[System.Serializable]
public class KingdomeVirtue
{
    public TMP_Text VirtueName;
    public TMP_Text VirtueCount;
    public Image VirtueIcon;
    public Image resourceOneIcon;
    public Image resourceTwoIcon;
}
