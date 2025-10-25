using UnityEngine;

public static class BalanceDB {
    const string RES_PATH = "CombatRanges"; // => Assets/Resources/CombatRanges.asset
    static CombatRanges _cached;
    static bool _warned;

    public static CombatRanges Ranges {
        get {
            if (GameBalance.R != null) return GameBalance.R;              // ensisijainen
            if (_cached != null) return _cached;                           // cache
            _cached = UnityEngine.Resources.Load<CombatRanges>(RES_PATH);  // fallback
            if (_cached == null && !_warned) {
                _warned = true;
                Debug.LogWarning("[BalanceDB] CombatRanges puuttuu.\n"+
                                 "- Suositus: aseta se Core→GameBalance.ranges -kenttään.\n"+
                                 "- Fallback: laita asset polkuun Assets/Resources/"+RES_PATH+".asset");
            }
            return _cached;
        }
    }
}
