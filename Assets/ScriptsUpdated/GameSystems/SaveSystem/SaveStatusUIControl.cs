using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SaveStatusUIControl : MonoBehaviour
{
    [Header("Saving Visual")]
    [SerializeField] private GameObject savingRoot;
    [SerializeField] private RectTransform spinningCircle;
    [SerializeField] private Image circleFillImage;

    [Header("Complete Visual")]
    [SerializeField] private GameObject completeRoot;

    [Header("Timing")]
    [SerializeField, Min(10f)] private float spinSpeed = 180f;
    [SerializeField, Min(0.1f)] private float circlePulseSeconds = 1.1f;
    [SerializeField, Min(0.1f)] private float completeVisibleSeconds = 0.75f;

    private SaveSystem _saveSystem;
    private Coroutine _visualRoutine;
    private bool _savingVisualActive;

    private void Awake()
    {
        HideImmediate();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void InstallSaveSystem(SaveSystem saveSystem)
    {
        if (_saveSystem == saveSystem)
            return;

        Unsubscribe();

        _saveSystem = saveSystem;

        Subscribe();

        if (_saveSystem != null && _saveSystem.IsSaving)
            ShowSaving();
        else
            HideImmediate();
    }

    private void Subscribe()
    {
        if (_saveSystem == null)
            return;

        _saveSystem.OnSaveQueued += HandleSaveQueued;
        _saveSystem.OnSaveStarted += HandleSaveStarted;
        _saveSystem.OnSaveCompleted += HandleSaveCompleted;
        _saveSystem.OnSaveFailed += HandleSaveFailed;
    }

    private void Unsubscribe()
    {
        if (_saveSystem == null)
            return;

        _saveSystem.OnSaveQueued -= HandleSaveQueued;
        _saveSystem.OnSaveStarted -= HandleSaveStarted;
        _saveSystem.OnSaveCompleted -= HandleSaveCompleted;
        _saveSystem.OnSaveFailed -= HandleSaveFailed;
    }

    private void HandleSaveQueued()
    {
        ShowSaving();
    }

    private void HandleSaveStarted()
    {
        ShowSaving();
    }

    private void HandleSaveCompleted()
    {
        ShowCompleteThenHide();
    }

    private void HandleSaveFailed(string error)
    {
        //Debug.LogWarning("[SaveStatusUIControl] Save failed, hiding save UI.\n" + error);
        HideImmediate();
    }

    private void ShowSaving()
    {
        _savingVisualActive = true;

        if (_visualRoutine != null)
            StopCoroutine(_visualRoutine);

        _visualRoutine = StartCoroutine(SavingLoop());
    }

    private void ShowCompleteThenHide()
    {
        _savingVisualActive = false;

        if (_visualRoutine != null)
            StopCoroutine(_visualRoutine);

        _visualRoutine = StartCoroutine(CompleteThenHideRoutine());
    }

    private IEnumerator SavingLoop()
    {
        if (savingRoot != null)
            savingRoot.SetActive(true);

        if (completeRoot != null)
            completeRoot.SetActive(false);

        while (_savingVisualActive)
        {
            if (spinningCircle != null)
                spinningCircle.Rotate(0f, 0f, -spinSpeed * Time.unscaledDeltaTime);

            if (circleFillImage != null)
            {
                float pulse = Mathf.PingPong(Time.unscaledTime / circlePulseSeconds, 1f);
                circleFillImage.fillAmount = Mathf.Lerp(0.25f, 1f, pulse);
            }

            yield return null;
        }
    }

    private IEnumerator CompleteThenHideRoutine()
    {
        if (savingRoot != null)
            savingRoot.SetActive(false);

        if (completeRoot != null)
            completeRoot.SetActive(true);

        if (circleFillImage != null)
            circleFillImage.fillAmount = 1f;

        yield return new WaitForSecondsRealtime(completeVisibleSeconds);

        HideImmediate();
    }

    private void HideImmediate()
    {
        _savingVisualActive = false;

        if (savingRoot != null)
            savingRoot.SetActive(false);

        if (completeRoot != null)
            completeRoot.SetActive(false);

        if (circleFillImage != null)
            circleFillImage.fillAmount = 0f;
    }
}
