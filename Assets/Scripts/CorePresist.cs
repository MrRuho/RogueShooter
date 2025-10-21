using UnityEngine;

public class CorePersist : MonoBehaviour
{
    static CorePersist inst;
    void Awake()
    {
        if (inst != null) { Destroy(gameObject); return; } // estä duplikaatit
        inst = this;
        DontDestroyOnLoad(gameObject); // tämä on JUURESSA
    }
}
