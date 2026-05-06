using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class FixedCameraAspectRatio : MonoBehaviour
{
    [Header("Target Web Build Dimensions")]
    public float targetWidth = 1080f;
    public float targetHeight = 2220f;

    private Camera cam;

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        ApplyAspectRatio();
    }

    private void Update()
    {
        ApplyAspectRatio();
    }

    private void ApplyAspectRatio()
    {
        if (cam == null)
            return;

        float targetAspect = targetWidth / targetHeight;
        float windowAspect = (float)Screen.width / Screen.height;

        float scaleHeight = windowAspect / targetAspect;

        if (scaleHeight < 1f)
        {
            Rect rect = cam.rect;
            rect.width = 1f;
            rect.height = scaleHeight;
            rect.x = 0f;
            rect.y = (1f - scaleHeight) / 2f;
            cam.rect = rect;
        }
        else
        {
            float scaleWidth = 1f / scaleHeight;

            Rect rect = cam.rect;
            rect.width = scaleWidth;
            rect.height = 1f;
            rect.x = (1f - scaleWidth) / 2f;
            rect.y = 0f;
            cam.rect = rect;
        }
    }
}