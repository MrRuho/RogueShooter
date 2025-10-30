// LocalVisibility.cs
using UnityEngine;

public class LocalVisibility : MonoBehaviour
{
    Renderer[] _renderers;
    Canvas[] _canvases;
    public bool IsVisible { get; private set; } = true;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _canvases  = GetComponentsInChildren<Canvas>(true);
    }

    public void Apply(bool visible)
    {
        if (IsVisible == visible) return; // ei turhaa työtä
        IsVisible = visible;

        // Toggle vain kun oikeasti muuttuu
        for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = visible;
        for (int i = 0; i < _canvases.Length;  i++) _canvases[i].enabled  = visible;
    }
}
