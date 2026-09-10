using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class VirtueUI : MonoBehaviour
{
    public Virtue virtueType;
    public Player currentPlayer;
    public Image virtueImage;
    public Transform ResourcePanel;
    public Image ResourceOneImage;
    public Image ResourceTwoImage;
    public TMP_Text VirtueName;
    public TMP_Text NumberOfvirtues;
    public GameObject IncDecButtons;
    public Button DisasterDiscardButton { get; private set; }

    public void SetDisasterDiscard(bool visible)
    {
        if (visible && DisasterDiscardButton == null)
        {
            var buttonObject = new GameObject("Discard Virtue", typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(transform, false);
            buttonObject.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(30f, 30f);
            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(0.65f, 0.12f, 0.12f);
            DisasterDiscardButton = buttonObject.GetComponent<Button>();
            DisasterDiscardButton.targetGraphic = background;

            var labelObject = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = NumberOfvirtues.font;
            label.text = "X";
            label.fontSize = 22;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            DisasterDiscardButton.onClick.AddListener(DiscardForDisaster);
        }
        if (DisasterDiscardButton != null)
        {
            DisasterDiscardButton.interactable = visible;
            DisasterDiscardButton.gameObject.SetActive(visible);
        }
    }

    public void DiscardForDisaster()
    {
        if (HardWinterDisaster.Active == null)
        {
            Debug.LogWarning("There is no virtue-loss disaster awaiting a discard.");
            return;
        }
        HardWinterDisaster.Active.TryDiscard(currentPlayer, virtueType);
    }

    public void SetResourceCombo()
    {
        if(ResourcePanel != null && ResourceOneImage != null && ResourceTwoImage != null)
        {
            ResourcePanel.gameObject.SetActive(true);
            ResourceOneImage.sprite = virtueType.ResourceOne.resourceIcon;
            ResourceTwoImage.sprite = virtueType.ResourceTwo.resourceIcon;
        }

    }

    public void AddVirtue()
    {
        Player currentPlayer = TurnManager.Instance.ActivePlayer;
        if (TurnManager.Instance.ActivePlayer == null) return;

        Dictionary<VirtueType, int> TempVirtue = PowerUIManager.Instance.virtueCounts;
        if (TempVirtue.ContainsKey(virtueType.type) && TempVirtue[virtueType.type] > 0)
        {
            int selectedCount = TurnManager.Instance.ActivePlayer.selectedVirtue.Count(v => v.type == virtueType.type);
            if (selectedCount < TempVirtue[virtueType.type])
            {
                int virtueCount = int.Parse(NumberOfvirtues.text);
                virtueCount--;
                NumberOfvirtues.text = virtueCount.ToString();

                TurnManager.Instance.ActivePlayer.selectedVirtue.Add(virtueType);
                Debug.Log($"Virtue {virtueType.name} added to selected virtues.");

                Debug.Log($"selected {virtueType.name}. Total virtue selected {TurnManager.Instance.ActivePlayer.selectedVirtue.Count}.");

                PowerUIManager.Instance.PowerMassage($"Virtue {virtueType.name} added to selected virtues. \n selected {virtueType.name}. Total virtue selected {TurnManager.Instance.ActivePlayer.selectedVirtue.Count}.");

            }
            else
            {
                Debug.Log($"Cannot add more {virtueType.name}, maximum allowed based on available virtues reached.");
                PowerUIManager.Instance.PowerMassage($"Cannot add more {virtueType.name}, maximum allowed based on available virtues reached.");

            }
        }
        else
        {
            Debug.Log($"Player does not have {virtueType.name} to add.");
            PowerUIManager.Instance.PowerMassage($"Player does not have {virtueType.name} to add.");
        }
    }
    public void RemoveVirtue()
    {
        Player currentPlayer = TurnManager.Instance.ActivePlayer;
        if (TurnManager.Instance.ActivePlayer == null) return;


        if (TurnManager.Instance.ActivePlayer.selectedVirtue.Contains(virtueType))
        {
            TurnManager.Instance.ActivePlayer.selectedVirtue.Remove(virtueType);
            Debug.Log($"Virtue {virtueType.name} removed from selected virtues.");
            int virtueCount = int.Parse(NumberOfvirtues.text);
            virtueCount++;
            NumberOfvirtues.text = virtueCount.ToString();
            PowerUIManager.Instance.PowerMassage($"Virtue {virtueType.name} removed from selected virtues.");
        }
        else
        {
            Debug.Log($"Cannot remove {virtueType.name} as it is not in selected virtues.");
            PowerUIManager.Instance.PowerMassage($"Cannot remove {virtueType.name} as it is not in selected virtues.");
        }
    }

    public void TurnOnINCDEC()
    {
        IncDecButtons.SetActive(true);
    }
    public void TurnOffINCDEC()
    {
        IncDecButtons.SetActive(false);
    }

}
