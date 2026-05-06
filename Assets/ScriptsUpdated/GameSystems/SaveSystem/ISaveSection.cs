using System.Collections;

public interface ISaveSection
{
    string Key { get; }
    bool IsDirty { get; }

    IEnumerator CaptureInto(SaveSnapshot snapshot, SaveCaptureContext context, int objectsPerFrame);

    void MarkDirty();
    void ClearDirty();
}