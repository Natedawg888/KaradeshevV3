using System.Collections;

public abstract class SaveSectionBase : ISaveSection
{
    public abstract string Key { get; }

    public bool IsDirty { get; private set; } = true;

    public void MarkDirty() => IsDirty = true;
    public void ClearDirty() => IsDirty = false;

    public abstract IEnumerator CaptureInto(
        SaveSnapshot snapshot,
        SaveCaptureContext context,
        int objectsPerFrame);
}