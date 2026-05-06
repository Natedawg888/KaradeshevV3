using System;                // NEW
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class EnvironmentResourceNode : MonoBehaviour
{
    // -------- BARREN API --------

    public static event Action<EnvironmentResourceNode> OnNodeBecameBarren;

    public void StartBarren(int turns = -1)
    {
        // If already barren, don't stack it
        if (isBarren) return;

        // Every time we go barren, increase recovery time
        if (barrenRecoveryIncreasePerUse > 0)
        {
            barrenRecoveryTurns += barrenRecoveryIncreasePerUse;
        }

        // If recovery has grown too high, auto-clear the tile instead of going barren
        if (allowImmediateClearOnOveruse &&
            barrenRecoveryClearThreshold > 0 &&
            barrenRecoveryTurns >= barrenRecoveryClearThreshold)
        {
            Debug.Log(
                $"[{name}] Barren recovery ({barrenRecoveryTurns}) exceeded threshold " +
                $"({barrenRecoveryClearThreshold}). Performing immediate clear (no clearing task timer).");

            PerformImmediateClear();
            return; // do NOT enter barren state
        }

        // Normal barren behaviour
        int duration = (turns > 0) ? turns : barrenRecoveryTurns;
        duration = Mathf.Max(1, duration);

        isBarren        = true;
        barrenTurnsLeft = duration;

        ApplyBarrenVisuals();

        Debug.Log($"[{name}] Node set to BARREN. Recovery in {barrenTurnsLeft} turns " +
                  $"(current barrenRecoveryTurns={barrenRecoveryTurns}).");

        // ensure world canvas is visible so the barren icon/timer can be seen
        if (environmentControl != null && environmentControl.canvas != null)
            environmentControl.canvas.SetActive(true);

        // notify listeners (e.g. production buildings) that this node is now barren
        OnNodeBecameBarren?.Invoke(this);
    }

    private void ExitBarrenState()
    {
        if (!isBarren) return;

        isBarren        = false;
        barrenTurnsLeft = 0;

        // When recovering from barren, fully restore environment health
        currentEnvironmentHealth = maxEnvironmentHealth;

        ApplyBarrenVisuals();

        Debug.Log($"[{name}] Node recovered from barren state. Environment health reset to {currentEnvironmentHealth}.");
    }

    private void ApplyBarrenVisuals()
    {
        UpdateBarrenIconAlphaFromHealthAndState();

        if (barrenTimerUI != null)
        {
            bool showTimer = isBarren;
            barrenTimerUI.gameObject.SetActive(showTimer);
            if (showTimer)
            {
                int total = Mathf.Max(1, (barrenTurnsLeft > 0 ? barrenTurnsLeft : barrenRecoveryTurns));
                barrenTimerUI.Initialize(total);
                barrenTimerUI.UpdateTimer(barrenTurnsLeft > 0 ? barrenTurnsLeft : total);
            }
        }
    }

    private void UpdateBarrenIconAlphaFromHealthAndState()
    {
        if (barrenIcon == null)
            return;

        float alpha = 0f;

        if (isBarren)
        {
            // Fully barren -> fully visible
            alpha = 1f;
        }
        else if (maxEnvironmentHealth > 0)
        {
            float health01 = Mathf.Clamp01((float)currentEnvironmentHealth / maxEnvironmentHealth);

            if (health01 <= 0.5f)
            {
                // health01: 0.5 -> 0.0  maps to  alpha: 0 -> 1
                float t = (0.5f - health01) / 0.5f;
                alpha = Mathf.Clamp01(t);
            }
            else
            {
                alpha = 0f;
            }
        }

        // Try UI Image first (world-space canvas), then SpriteRenderer as fallback
        var img = barrenIcon.GetComponent<Image>();
        if (img != null)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
        else
        {
            var sr = barrenIcon.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }

        // Only keep it active when there’s something to see
        barrenIcon.SetActive(alpha > 0.01f);
    }

    public void ApplyEnvironmentDamage(int amount)
    {
        if (amount <= 0) return;

        currentEnvironmentHealth = Mathf.Max(0, currentEnvironmentHealth - amount);

        // If we just hit 0 and aren't barren yet, enter barren state
        if (!isBarren && currentEnvironmentHealth <= 0)
        {
            Debug.Log($"[{name}] Environment health reached 0. Entering barren state.");
            StartBarren();
            return;
        }

        // Not barren (yet) -> update the fade based on the new health
        ApplyBarrenVisuals();
    }

    private void PerformImmediateClear()
    {
        if (environmentControl == null)
        {
            Debug.LogWarning($"[{name}] PerformImmediateClear called but EnvironmentControl is null.");
            return;
        }

        if (gridManager == null)
            gridManager = FindObjectOfType<GridManager>();

        if (gridManager == null)
        {
            Debug.LogWarning($"[{name}] PerformImmediateClear aborted: GridManager not found.");
            return;
        }

        if (clearingTaskPrefab == null || clearingTaskPrefab.clearedTilePrefab == null)
        {
            Debug.LogWarning(
                $"[{name}] clearingTaskPrefab or its clearedTilePrefab is not assigned; " +
                "cannot auto-clear tile.");
            return;
        }

        GameObject clearedPrefab = clearingTaskPrefab.clearedTilePrefab;

        // Use the big tile this node belongs to
        var tile = environmentControl.GetComponentInParent<TileControl>();
        if (tile == null)
        {
            Debug.LogWarning($"[{name}] PerformImmediateClear could not find TileControl parent.");
            return;
        }

        var tileBox = tile.GetComponent<BoxCollider>();
        if (tileBox == null)
        {
            Debug.LogWarning($"[{name}] PerformImmediateClear: TileControl has no BoxCollider.");
            return;
        }

        // We fill the whole tile footprint using grid cells
        Bounds bounds = tileBox.bounds;

        Vector2Int minGridPos = gridManager.GetGridPosition(bounds.min);
        Vector2Int maxGridPos = gridManager.GetGridPosition(bounds.max);
        float halfCell = gridManager.cellSize * 0.5f;

        Debug.Log($"[{name}] PerformImmediateClear filling tile area " +
                $"min={minGridPos} max={maxGridPos} bounds={bounds}");

        // 1) Destroy any existing TileControl tiles in this area (including this tile)
        {
            Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity);
            foreach (var hit in hits)
            {
                var hitTile = hit.GetComponent<TileControl>();
                if (hitTile == null) continue;

                Debug.Log($"[{name}] Immediate clear: destroying tile '{hitTile.name}' in area.");
                Destroy(hitTile.gameObject);
            }
        }

        // 2) Fill every grid cell in the bounds with a cleared tile
        for (int x = minGridPos.x; x <= maxGridPos.x; x++)
        {
            for (int y = minGridPos.y; y <= maxGridPos.y; y++)
            {
                Vector3 cellCorner = gridManager.GetWorldPosition(x, y);
                Vector3 cellCenter = cellCorner + new Vector3(halfCell, 0f, halfCell);
                Vector2Int gridPos = new Vector2Int(x, y);

                // Only consider cells whose CENTER is inside the bounds
                if (!bounds.Contains(new Vector3(cellCenter.x, bounds.center.y, cellCenter.z)))
                    continue;

                GameObject cleared = Instantiate(clearedPrefab, cellCenter, Quaternion.identity);
                cleared.name = $"ImmediateCleared_{gridPos.x}_{gridPos.y}";

                gridManager.MarkCellOccupied(gridPos.x, gridPos.y);
            }
        }

        Debug.Log(
            $"[{name}] PerformImmediateClear completed; tile area at {environmentControl.gridPosition} " +
            "filled with cleared tiles.");

        // The node lives on the old tile, which we've destroyed above,
        // so this component will go away with it. No extra Destroy(gameObject) needed.
    }
}
