using UnityEngine;
using System.Linq;

public class CivilizationStateManager : MonoBehaviour
{
    public static CivilizationStateManager Instance { get; private set; }

    [Header("State (0..1)")]
    [Range(0f, 1f)] public float happiness01 = 0.5f;
    [Range(0f,1f)] public float health01   = 0.5f;
    [Range(0f,1f)] public float diversity01   = 0.5f;   // wire later
    [Range(0f, 1f)] public float integration01 = 0.5f;   // wire later
    [Range(0f, 1f)] public float order01 = 0.5f;
    [Range(0f, 1f)] public float discovery01 = 0.6f;
    [Range(0f, 1f)] public float knowledge01   = 0.01f;
    [Range(0f, 1f)] public float faith01       = 0.5f;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SetHappiness01(float v) => happiness01 = Mathf.Clamp01(v);

    public void SetOrder01(float v) => order01 = Mathf.Clamp01(v);

    public void SetDiscovery01(float v) => discovery01 = Mathf.Clamp01(v);
    
    public void SetKnowledge01(float v)   => knowledge01   = Mathf.Clamp01(v);
    public void SetFaith01(float v)       => faith01       = Mathf.Clamp01(v);

    private void MarkCoreSystemsDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.CoreSystems);
    }

    public void AdjustHappiness(float delta)
    {
        happiness01 = Mathf.Clamp01(happiness01 + delta);
        MarkCoreSystemsDirty();
    }

    public void AdjustHealth(float delta)
    {
        health01 = Mathf.Clamp01(health01 + delta);
        MarkCoreSystemsDirty();
    }

    public void AdjustDiversity(float delta)
    {
        diversity01 = Mathf.Clamp01(diversity01 + delta);
        MarkCoreSystemsDirty();
    }

    public void AdjustIntegration(float delta)
    {
        integration01 = Mathf.Clamp01(integration01 + delta);
        MarkCoreSystemsDirty();
    }

    public void AdjustOrder(float delta)
    {
        order01 = Mathf.Clamp01(order01 + delta);
        MarkCoreSystemsDirty();
    }

    public void AdjustDiscovery(float delta)
    {
        discovery01 = Mathf.Clamp01(discovery01 + delta);
        MarkCoreSystemsDirty();
    }

    public void AdjustKnowledge(float d)
    {
        knowledge01 = Mathf.Clamp01(knowledge01 + d);
        MarkCoreSystemsDirty();
    }

    public void AdjustFaith(float delta)
    {
        faith01 = Mathf.Clamp01(faith01 + delta);
        MarkCoreSystemsDirty();
    }

    public CivilizationStateSaveData SaveState()
    {
        return new CivilizationStateSaveData
        {
            happiness01 = happiness01,
            health01 = health01,
            diversity01 = diversity01,
            integration01 = integration01,
            order01 = order01,
            discovery01 = discovery01,
            knowledge01 = knowledge01,
            faith01 = faith01
        };
    }

    public void LoadState(CivilizationStateSaveData data)
    {
        if (data == null)
            return;

        happiness01 = Mathf.Clamp01(data.happiness01);
        health01 = Mathf.Clamp01(data.health01);
        diversity01 = Mathf.Clamp01(data.diversity01);
        integration01 = Mathf.Clamp01(data.integration01);
        order01 = Mathf.Clamp01(data.order01);
        discovery01 = Mathf.Clamp01(data.discovery01);
        knowledge01 = Mathf.Clamp01(data.knowledge01);
        faith01     = Mathf.Clamp01(data.faith01);
    }
}