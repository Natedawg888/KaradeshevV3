using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class UnitGroupActionManager : MonoBehaviour
{
    public Coroutine StartManagedRoutine(IEnumerator routine)
    {
        if (routine == null || !isActiveAndEnabled)
            return null;

        return StartCoroutine(routine);
    }
}