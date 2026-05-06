using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BuildingUnderAttackIconView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root; // icon root (or leave null to use this.gameObject)

    private GameObject _go;

    private void Awake()
    {
        _go = root != null ? root : gameObject;
        _go.SetActive(false);
    }

    public void SetUnderAttack(bool underAttack)
    {
        if (_go == null) _go = root != null ? root : gameObject;

        if (_go.activeSelf != underAttack)
            _go.SetActive(underAttack);
    }

    // Optional compatibility if anything still calls Ping()
    public void Ping(int attackerGroupId = -1)
    {
        SetUnderAttack(true);
    }
}