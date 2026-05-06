using UnityEngine;
using UnityEngine.UI;

public class ProfilePicSelector : MonoBehaviour
{
    [Header("Image References")]
    public Image mainProfileImage; // e.g., large display
    public Image profileImage; // e.g., small icon / button image

    public void SetProfilePicture(Sprite sprite)
    {
        if (sprite == null) return;

        if (mainProfileImage != null) mainProfileImage.sprite = sprite;
        if (profileImage != null) profileImage.sprite = sprite;
    }
}