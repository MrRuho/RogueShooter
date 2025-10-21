using System.Collections;
using UnityEngine;

public sealed class GlobalCoroutineHost : MonoBehaviour
{
    private static GlobalCoroutineHost _instance;

    public static Coroutine StartRoutine(IEnumerator routine)
    {
        if (_instance == null)
        {
            var go = new GameObject("[GlobalCoroutineHost]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GlobalCoroutineHost>();
        }
        return _instance.StartCoroutine(routine);
    }
}
