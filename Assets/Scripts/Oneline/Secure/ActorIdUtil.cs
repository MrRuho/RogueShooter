using Mirror;
using UnityEngine;

/// <summary>
/// Yksinkertainen, yhteinen paikka hakea "actorin" netId.
/// Käyttö: this.GetActorId() MISTÄ tahansa MonoBehaviourista.
/// </summary>
public static class ActorIdUtil
{
    /// <summary>
    /// Hakee lähimmän yläjuuren NetworkIdentityn ja palauttaa sen netId:n.
    /// Käy kaikissa: Actionit, Animatorit, ym. joissa 'this' on Component.
    /// </summary>
    public static uint GetActorId(this Component self)
    {
        if (!self) return 0;
        var ni = self.GetComponentInParent<NetworkIdentity>();
        return ni ? ni.netId : 0;
    }

    /// <summary>
    /// Jos haluat hakea suoraan Unitista (jos se on saatavilla).
    /// </summary>
    public static uint GetActorId(this Unit unit)
    {
        if (!unit) return 0;
        var ni = unit.GetComponent<NetworkIdentity>();
        return ni ? ni.netId : 0;
    }

    /// <summary>
    /// Variaatiot, jos joskus tarvitset GameObjectista tai Transformista.
    /// </summary>
    public static uint GetActorId(this GameObject go) => go ? go.transform.GetActorId() : 0;
    public static uint GetActorId(this Transform t)
    {
        if (!t) return 0;
        var ni = t.GetComponentInParent<NetworkIdentity>();
        return ni ? ni.netId : 0;
    }
}
