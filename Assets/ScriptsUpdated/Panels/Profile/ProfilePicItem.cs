using System;
using UnityEngine;
using UnityEngine.UI;

public class ProfilePicItem : MonoBehaviour
{
    [SerializeField] private Image avatarImage;
    [SerializeField] private Button selectButton;

    private Sprite currentSprite;
    private Action<Sprite> onSelected;

    public void Setup(Sprite sprite, Action<Sprite> selectionCallback)
    {
        currentSprite = sprite;
        avatarImage.sprite = sprite;
        onSelected = selectionCallback;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelected?.Invoke(currentSprite));
    }
}
