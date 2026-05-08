using System.Collections;

public sealed class NotificationsSaveSection : SaveSectionBase
{
    public override string Key => SaveSectionKeys.Notifications;

    public override IEnumerator CaptureInto(SaveSnapshot snapshot, SaveCaptureContext context, int objectsPerFrame)
    {
        snapshot.notifications = NotificationManager.Instance != null
            ? NotificationManager.Instance.SaveState()
            : null;

        ClearDirty();
        yield break;
    }
}
