using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HexPopupManager : MonoBehaviour
{
    public static HexPopupManager Instance;

    public RectTransform popupPanel;
    public TMP_Text popupText;
    // public Canvas canvas;
    public Button closeButton;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        closeButton.onClick.AddListener(HidePopup);
        popupPanel.gameObject.SetActive(false);
    }

    public void ShowPopup(string tileType, Vector3 worldPosition)
    {
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
        popupPanel.position = screenPos;
        popupText.text = $"Tile: {tileType}";
        popupPanel.gameObject.SetActive(true);
    }

    public void HidePopup()
    {
        popupPanel.gameObject.SetActive(false);
    }
}
