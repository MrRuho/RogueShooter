using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>
///  Tämä pitää huolen siitä että kaikki objektit luodaan clientille oikeaan Level skeneen, kun client liittyy peliin.
///  Ilman tätä kaikki luotavat objektit latautusivat Coreen, mikä ei ole toivottavaa.
///  Käytä tätä kaikissa sellaisissa objekteissa jotka täytyy kentän latautuessa siirtyä oikeaan sceneen. 
///  Esim Unit, Tuhoutuvat objektit jne.
///  Huom! SpawnRouter hoitaa objekti siirron oikeaan sceneen varsinainen pelin aikana.
/// </summary>
[DisallowMultipleComponent]
public class NetworkSceneBinder : NetworkBehaviour
{
    [SyncVar] public string targetSceneName;  // server täyttää tämän ennen spawnia

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Hostilla server on jo siirtänyt oikein – ei tarvetta uudelleen siirtoon
        if (isServer) return;

        if (!string.IsNullOrEmpty(targetSceneName))
            StartCoroutine(MoveWhenLoaded(targetSceneName));
    }

    private IEnumerator MoveWhenLoaded(string sceneName)
    {
        // odota että kohdescene on varmasti ladattu clientillä
        while (true)
        {
            var sc = SceneManager.GetSceneByName(sceneName);
            if (sc.IsValid() && sc.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(gameObject, sc);
                yield break;
            }
            yield return null;
        }
    }
}
