using UnityEngine;

[CreateAssetMenu(fileName = "LoSConfig", menuName = "Config/LoS Config")]
public class LoSConfig : ScriptableObject
{
    // Esteet joiden yli ei voida nähdä kun ne ylittävät tietyn korkeuden.
    public LayerMask losBlockersMask;      // vain korkeat seinät

    // Miltä korkeudelta testataan.
    public float     eyeHeight      = 1.6f;
    [Range(1,5)]
    public int       samplesPerCell = 1;   // 1=nopea, 5=kulmiin jämäkämpi

    // Kuinka paljon samplet siirretään ruudun reunoista sisään, jotta kulmabugit ja reunatapaukset vähenevät.
    [Range(0f, 0.5f)]
    public float     insetWU        = 0.30f;

    private static LoSConfig _instance;
    public static LoSConfig Instance {
        get {
            if (_instance == null)
                _instance = Resources.Load<LoSConfig>("LoSConfig");
#if UNITY_EDITOR
            if (_instance == null)
                Debug.LogError("LoSConfig asset puuttuu: luo Resources/LoSConfig.asset (Create > Config > LoS Config).");
#endif
            return _instance;
        }
    }
}
