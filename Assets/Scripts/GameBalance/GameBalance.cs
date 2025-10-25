using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class GameBalance : MonoBehaviour {
    public static GameBalance I { get; private set; }
    [SerializeField] CombatRanges ranges;
    public static CombatRanges R => I ? I.ranges : null;

    void Awake() {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        if (ranges == null)
            Debug.LogWarning("GameBalance.ranges puuttuu — käytetään Resources-fallbackia jos saatavilla.");
    }
}