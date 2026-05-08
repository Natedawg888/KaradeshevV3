using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    // One Camera.main lookup shared across all instances — refreshes only when null
    private static Camera _sharedCam;
    private Transform _camTransform;

    private void OnEnable()
    {
        if (_sharedCam == null) _sharedCam = Camera.main;
        _camTransform = _sharedCam != null ? _sharedCam.transform : null;
    }

    private void Update()
    {
        if (_camTransform == null)
        {
            if (_sharedCam == null) _sharedCam = Camera.main;
            _camTransform = _sharedCam != null ? _sharedCam.transform : null;
            return;
        }
        transform.LookAt(transform.position + _camTransform.rotation * Vector3.forward,
                         _camTransform.rotation * Vector3.up);
    }
}