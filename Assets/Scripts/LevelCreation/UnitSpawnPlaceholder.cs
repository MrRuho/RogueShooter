using UnityEngine;

#if UNITY_EDITOR
[ExecuteAlways]
#endif
[DisallowMultipleComponent]
public class UnitSpawnPlaceholder : MonoBehaviour
{
    public enum Side { Host, Client, Enemy }

    [Header("Who spawns here?")]
    public Side side = Side.Host;

    [Header("Options")]
    [Tooltip("Snäppää paikan ruudun keskelle, jos LevelGrid on käytössä.")]
    public bool snapToGridCenter = true;

    [Tooltip("Piilota renderöijät play-tilassa (Editorissa näkyy).")]
    public bool hideRendererInPlayMode = true;

    [Tooltip("Tuhotaan palvelimella spawnin jälkeen (muussa tapauksessa disabloidaan).")]
    public bool destroyOnServerAfterUse = true;

    [Tooltip("Vapaa järjestysnumero deterministiseen spawn-järjestykseen (pienin ensin).")]
    public int order = 0;

    public Vector3 GetSpawnWorldPosition()
    {
        var pos = transform.position;

        if (snapToGridCenter && LevelGrid.Instance != null)
        {
            var gp = LevelGrid.Instance.GetGridPosition(pos);
            pos = LevelGrid.Instance.GetWorldPosition(gp); // keskittää ruutuun
        }

        return pos;
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;
#endif
        if (hideRendererInPlayMode) ToggleRenderers(false);
    }

    public void Consume()
    {
        if (destroyOnServerAfterUse)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void ToggleRenderers(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // yksinkertainen “keila”: väri puolen mukaan
        Color c = side == Side.Host ? new Color(0f, 0.9f, 1f, 0.9f)
                 : side == Side.Client ? new Color(1f, 0f, 1f, 0.9f)
                 : new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.color = c;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.05f, new Vector3(0.6f, 0.1f, 0.6f));
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.8f);
    }
#endif
}
