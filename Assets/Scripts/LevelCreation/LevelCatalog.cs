// RogueShooter/LevelCatalog.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelCatalog", menuName = "RogueShooter/Level Catalog")]
public class LevelCatalog : ScriptableObject {
    public List<LevelEntry> levels = new();

    public int Count => levels?.Count ?? 0;
    public LevelEntry Get(int i) => (i >= 0 && i < Count) ? levels[i] : null;
    public int IndexOfScene(string sceneName) => levels.FindIndex(l => l != null && l.sceneName == sceneName);
}

[Serializable]
public class LevelEntry {
    [Tooltip("Scene name täsmälleen Build Settingsissä")]
    public string sceneName;
    public string displayName;
    public Sprite thumbnail;

    // Valinnainen metadata editorityöhön
    public Vector3Int gridSize = new(30, 1, 30);
    public int floors = 1;
}
