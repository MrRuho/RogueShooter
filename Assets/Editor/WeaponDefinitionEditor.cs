/*
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WeaponDefinition))]
public class WeaponDefinitionEditor : Editor
{
    private SerializedProperty overwatch;
    
    private SerializedProperty maxShootRange;
    private SerializedProperty noCoverDamageBonus;
    private SerializedProperty baseDamage;
    private SerializedProperty critBonusDamage;
    private SerializedProperty grazeFactor;
    private SerializedProperty missChipFactor;
    private SerializedProperty burstMin;
    private SerializedProperty burstMax;
    private SerializedProperty burstShotDelay;
    private SerializedProperty closeMax;
    private SerializedProperty mediumMax;
    private SerializedProperty longMax;
    private SerializedProperty useAdvancedAccuracy;
    private SerializedProperty melee;
    private SerializedProperty close;
    private SerializedProperty medium;
    private SerializedProperty longRange;
    private SerializedProperty extreme;
    private SerializedProperty meleeAcc;
    private SerializedProperty closeAcc;
    private SerializedProperty mediumAcc;
    private SerializedProperty longAcc;
    private SerializedProperty extremeAcc;
    private SerializedProperty critStartMelee;
    private SerializedProperty critStartClose;
    private SerializedProperty critStartMedium;
    private SerializedProperty critStartLong;
    private SerializedProperty critStartExtreme;
    

    private UnitArchetype selectedArchetype;
    private int skillLevel = 0;

    private void OnEnable()
    {
        overwatch = serializedObject.FindProperty("overwatch");
        maxShootRange = serializedObject.FindProperty("maxShootRange");
        noCoverDamageBonus = serializedObject.FindProperty("NoCoverDamageBonus");
        baseDamage = serializedObject.FindProperty("baseDamage");
        critBonusDamage = serializedObject.FindProperty("critBonusDamage");
        grazeFactor = serializedObject.FindProperty("grazeFactor");
        missChipFactor = serializedObject.FindProperty("missChipFactor");
        burstMin = serializedObject.FindProperty("burstMin");
        burstMax = serializedObject.FindProperty("burstMax");
        burstShotDelay = serializedObject.FindProperty("burstShotDelay");
        closeMax = serializedObject.FindProperty("closeMax");
        mediumMax = serializedObject.FindProperty("mediumMax");
        longMax = serializedObject.FindProperty("longMax");
        useAdvancedAccuracy = serializedObject.FindProperty("useAdvancedAccuracy");
        melee = serializedObject.FindProperty("melee");
        close = serializedObject.FindProperty("close");
        medium = serializedObject.FindProperty("medium");
        longRange = serializedObject.FindProperty("long");
        extreme = serializedObject.FindProperty("extreme");
        meleeAcc = serializedObject.FindProperty("meleeAcc");
        closeAcc = serializedObject.FindProperty("closeAcc");
        mediumAcc = serializedObject.FindProperty("mediumAcc");
        longAcc = serializedObject.FindProperty("longAcc");
        extremeAcc = serializedObject.FindProperty("extremeAcc");
        critStartMelee = serializedObject.FindProperty("critStartMelee");
        critStartClose = serializedObject.FindProperty("critStartClose");
        critStartMedium = serializedObject.FindProperty("critStartMedium");
        critStartLong = serializedObject.FindProperty("critStartLong");
        critStartExtreme = serializedObject.FindProperty("critStartExtreme");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        WeaponDefinition weapon = (WeaponDefinition)target;

        // POISTA kaikki manuaaliset LabelField-otsikot.
        // Jätä vain PropertyField-kutsut – Header-attribuutit tulevat datasta.

        // Range
        EditorGUILayout.PropertyField(maxShootRange);
        EditorGUILayout.Space(5);

        // No-cover bonus
        EditorGUILayout.PropertyField(noCoverDamageBonus);
        EditorGUILayout.Space(5);

        // Base damage
        EditorGUILayout.PropertyField(baseDamage);
        EditorGUILayout.PropertyField(critBonusDamage);
        EditorGUILayout.PropertyField(grazeFactor);
        EditorGUILayout.PropertyField(missChipFactor);
        EditorGUILayout.Space(5);

        // Burst
        EditorGUILayout.PropertyField(burstMin, new GUIContent("Burst Min", "Minimum shots per burst"));
        EditorGUILayout.PropertyField(burstMax, new GUIContent("Burst Max", "Maximum shots per burst"));
        EditorGUILayout.PropertyField(burstShotDelay, new GUIContent("Burst Shot Delay", "Time between shots in a burst (seconds)"));
        EditorGUILayout.Space(5);

        // Legacy per-weapon ranges
        EditorGUILayout.PropertyField(closeMax);
        EditorGUILayout.PropertyField(mediumMax);
        EditorGUILayout.PropertyField(longMax);
        EditorGUILayout.Space(5);

        // Overwatch MODE  ←  UUSI BLOKKI
        EditorGUILayout.PropertyField(overwatch, new GUIContent("Overwatch Mode"), true);
        EditorGUILayout.Space(8);

        // Advanced accuracy
        EditorGUILayout.PropertyField(useAdvancedAccuracy);
        EditorGUILayout.Space(10);

        // Tilastopreview (pidä tämä kuten nyt)
        if (weapon.burstMax > 1)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Statistics Preview Settings", EditorStyles.boldLabel);
            selectedArchetype = (UnitArchetype)EditorGUILayout.ObjectField("Unit Archetype", selectedArchetype, typeof(UnitArchetype), false);
            if (selectedArchetype != null)
                skillLevel = EditorGUILayout.IntSlider("Skill Level", skillLevel, 0, 10);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        // Range bandit + tilastot
        DrawRangeBandWithStatistics(weapon, melee, "Melee", RangeBand.Melee);
        DrawRangeBandWithStatistics(weapon, close, "Close", RangeBand.Close);
        DrawRangeBandWithStatistics(weapon, medium, "Medium", RangeBand.Medium);
        DrawRangeBandWithStatistics(weapon, longRange, "Long", RangeBand.Long);
        DrawRangeBandWithStatistics(weapon, extreme, "Extreme", RangeBand.Extreme);

        EditorGUILayout.Space(10);

        // Legacy baselines
        EditorGUILayout.PropertyField(meleeAcc);
        EditorGUILayout.PropertyField(closeAcc);
        EditorGUILayout.PropertyField(mediumAcc);
        EditorGUILayout.PropertyField(longAcc);
        EditorGUILayout.PropertyField(extremeAcc);
        EditorGUILayout.Space(5);

        // Legacy crit starts
        EditorGUILayout.PropertyField(critStartMelee);
        EditorGUILayout.PropertyField(critStartClose);
        EditorGUILayout.PropertyField(critStartMedium);
        EditorGUILayout.PropertyField(critStartLong);
        EditorGUILayout.PropertyField(critStartExtreme);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawRangeBandWithStatistics(WeaponDefinition weapon, SerializedProperty rangeBandProperty, string label, RangeBand band)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.PropertyField(rangeBandProperty, new GUIContent(label), true);

        if (weapon.burstMax > 1 && rangeBandProperty.isExpanded)
        {
            EditorGUILayout.Space(5);
            DrawBurstProbabilities(weapon, band);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void DrawBurstProbabilities(WeaponDefinition weapon, RangeBand band)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.LabelField($"━━━ Burst Statistics for {band} ━━━", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(3);

        RangeBandTuning tuning = weapon.GetTuning(band);
        int baseHitChance = tuning.baseHitChance;

        int skillBonus = 0;
        if (selectedArchetype != null)
        {
            skillBonus = skillLevel * selectedArchetype.accuracyBonusPerSkillLevel;
        }

        if (selectedArchetype != null)
        {
            DrawCoverScenario(weapon, band, tuning, baseHitChance, skillBonus, 0, "No Cover", new Color(0.3f, 0.7f, 0.3f));
            DrawCoverScenario(weapon, band, tuning, baseHitChance, skillBonus, selectedArchetype.LowCoverEnemyHitPenalty, "Low Cover", new Color(0.7f, 0.7f, 0.3f));
            DrawCoverScenario(weapon, band, tuning, baseHitChance, skillBonus, selectedArchetype.highCoverEnemyHitPenalty, "High Cover", new Color(0.7f, 0.3f, 0.3f));
        }
        else
        {
            DrawCoverScenario(weapon, band, tuning, baseHitChance, 0, 0, "Base", new Color(0.3f, 0.6f, 0.6f));
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawCoverScenario(WeaponDefinition weapon, RangeBand band, RangeBandTuning tuning, int baseHitChance, int skillBonus, int coverPenalty, string scenarioLabel, Color headerColor)
    {
        int finalHitChance = Mathf.Clamp(baseHitChance + skillBonus - coverPenalty, 0, 100);
        float hitProbability = finalHitChance / 100f;

        Color originalBgColor = GUI.backgroundColor;
        GUI.backgroundColor = headerColor;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = originalBgColor;

        string modifierText = "";
        if (skillBonus > 0) modifierText += $"+{skillBonus}% skill";
        if (coverPenalty > 0)
        {
            if (modifierText.Length > 0) modifierText += ", ";
            modifierText += $"-{coverPenalty}% cover";
        }

        EditorGUILayout.LabelField($"┌─ {scenarioLabel} ─┐", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Hit: {baseHitChance}% {(modifierText.Length > 0 ? $"({modifierText})" : "")} = {finalHitChance}%", EditorStyles.miniLabel);
        EditorGUILayout.Space(2);

        if (weapon.burstMin == weapon.burstMax)
        {
            int burstSize = weapon.burstMin;
            DrawProbabilitiesForBurstSize(burstSize, hitProbability, tuning, false);
        }
        else
        {
            EditorGUILayout.LabelField($"Burst Range: {weapon.burstMin}-{weapon.burstMax} shots", EditorStyles.miniLabel);
            
            for (int burstSize = weapon.burstMin; burstSize <= weapon.burstMax; burstSize++)
            {
                EditorGUILayout.LabelField($"If {burstSize} shots:", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                DrawProbabilitiesForBurstSize(burstSize, hitProbability, tuning, true);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void DrawProbabilitiesForBurstSize(int burstSize, float hitProbability, RangeBandTuning tuning, bool compact)
    {
        float atLeastOneHit = 1f - Mathf.Pow(1f - hitProbability, burstSize);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("≥1 Hit:", EditorStyles.boldLabel, GUILayout.Width(80));
        DrawProbabilityBar(atLeastOneHit, true);
        EditorGUILayout.LabelField($"{atLeastOneHit * 100:F1}%", EditorStyles.boldLabel, GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();

        if (!compact)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Hit Type Distribution (when hit):", EditorStyles.miniLabel);

            int totalWeight = tuning.onHit_Close + tuning.onHit_Graze + tuning.onHit_Hit + tuning.onHit_Crit;
            if (totalWeight > 0)
            {
                DrawHitTypeDistribution("Close", tuning.onHit_Close, totalWeight, new Color(1f, 0.6f, 0.2f));
                DrawHitTypeDistribution("Graze", tuning.onHit_Graze, totalWeight, new Color(1f, 1f, 0.3f));
                DrawHitTypeDistribution("Hit", tuning.onHit_Hit, totalWeight, new Color(0.3f, 0.7f, 1f));
                DrawHitTypeDistribution("Crit", tuning.onHit_Crit, totalWeight, new Color(0.3f, 1f, 0.3f));
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Exact Hit Count:", EditorStyles.miniLabel);
        }

        for (int hits = 0; hits <= burstSize; hits++)
        {
            float probability = CalculateBinomialProbability(burstSize, hits, hitProbability);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{hits} hit{(hits != 1 ? "s" : "")}:", GUILayout.Width(80));
            DrawProbabilityBar(probability, false);
            EditorGUILayout.LabelField($"{probability * 100:F1}%", GUILayout.Width(55));
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawHitTypeDistribution(string label, int weight, int totalWeight, Color barColor)
    {
        if (totalWeight == 0) return;
        
        float probability = (float)weight / totalWeight;
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"  {label}:", GUILayout.Width(80));
        
        Rect rect = GUILayoutUtility.GetRect(18, 16, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
        Rect fillRect = new Rect(rect.x, rect.y, rect.width * probability, rect.height);
        EditorGUI.DrawRect(fillRect, barColor);
        DrawRectBorder(rect);
        
        EditorGUILayout.LabelField($"{probability * 100:F1}%", GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawProbabilityBar(float probability, bool bold)
    {
        Rect rect = GUILayoutUtility.GetRect(18, bold ? 20 : 16, GUILayout.ExpandWidth(true));
        
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
        
        Rect fillRect = new Rect(rect.x, rect.y, rect.width * probability, rect.height);
        
        Color barColor = Color.Lerp(new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.8f, 0.2f), probability);
        EditorGUI.DrawRect(fillRect, barColor);
        
        DrawRectBorder(rect);
    }

    private void DrawRectBorder(Rect rect)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), Color.black);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), Color.black);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), Color.black);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), Color.black);
    }

    private float CalculateBinomialProbability(int n, int k, float p)
    {
        float binomialCoeff = BinomialCoefficient(n, k);
        float probability = binomialCoeff * Mathf.Pow(p, k) * Mathf.Pow(1f - p, n - k);
        return probability;
    }

    private float BinomialCoefficient(int n, int k)
    {
        if (k > n) return 0;
        if (k == 0 || k == n) return 1;
        
        float result = 1;
        for (int i = 0; i < k; i++)
        {
            result *= (n - i);
            result /= (i + 1);
        }
        return result;
    }
}
*/
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WeaponDefinition))]
public class WeaponDefinitionEditor : Editor
{
    private SerializedProperty normalShooting;   // UUSI
    private SerializedProperty overwatch;        // UUSI

    private SerializedProperty maxShootRange;
    private SerializedProperty noCoverDamageBonus;
    private SerializedProperty baseDamage;
    private SerializedProperty critBonusDamage;
    private SerializedProperty grazeFactor;
    private SerializedProperty missChipFactor;

    private SerializedProperty burstMin;
    private SerializedProperty burstMax;
    private SerializedProperty burstShotDelay;

    private SerializedProperty closeMax;
    private SerializedProperty mediumMax;
    private SerializedProperty longMax;

    private SerializedProperty useAdvancedAccuracy;
    private SerializedProperty melee;
    private SerializedProperty close;
    private SerializedProperty medium;
    private SerializedProperty longRange;
    private SerializedProperty extreme;

    private SerializedProperty meleeAcc;
    private SerializedProperty closeAcc;
    private SerializedProperty mediumAcc;
    private SerializedProperty longAcc;
    private SerializedProperty extremeAcc;

    private SerializedProperty critStartMelee;
    private SerializedProperty critStartClose;
    private SerializedProperty critStartMedium;
    private SerializedProperty critStartLong;
    private SerializedProperty critStartExtreme;

    private UnitArchetype selectedArchetype;
    private int skillLevel = 0;

    private void OnEnable()
    {
        normalShooting      = serializedObject.FindProperty("normalShooting");   // UUSI
        overwatch           = serializedObject.FindProperty("overwatch");        // UUSI

        maxShootRange       = serializedObject.FindProperty("maxShootRange");
        noCoverDamageBonus  = serializedObject.FindProperty("NoCoverDamageBonus");
        baseDamage          = serializedObject.FindProperty("baseDamage");
        critBonusDamage     = serializedObject.FindProperty("critBonusDamage");
        grazeFactor         = serializedObject.FindProperty("grazeFactor");
        missChipFactor      = serializedObject.FindProperty("missChipFactor");

        burstMin            = serializedObject.FindProperty("burstMin");
        burstMax            = serializedObject.FindProperty("burstMax");
        burstShotDelay      = serializedObject.FindProperty("burstShotDelay");

        closeMax            = serializedObject.FindProperty("closeMax");
        mediumMax           = serializedObject.FindProperty("mediumMax");
        longMax             = serializedObject.FindProperty("longMax");

        useAdvancedAccuracy = serializedObject.FindProperty("useAdvancedAccuracy");
        melee               = serializedObject.FindProperty("melee");
        close               = serializedObject.FindProperty("close");
        medium              = serializedObject.FindProperty("medium");
        longRange           = serializedObject.FindProperty("long");
        extreme             = serializedObject.FindProperty("extreme");

        meleeAcc            = serializedObject.FindProperty("meleeAcc");
        closeAcc            = serializedObject.FindProperty("closeAcc");
        mediumAcc           = serializedObject.FindProperty("mediumAcc");
        longAcc             = serializedObject.FindProperty("longAcc");
        extremeAcc          = serializedObject.FindProperty("extremeAcc");

        critStartMelee      = serializedObject.FindProperty("critStartMelee");
        critStartClose      = serializedObject.FindProperty("critStartClose");
        critStartMedium     = serializedObject.FindProperty("critStartMedium");
        critStartLong       = serializedObject.FindProperty("critStartLong");
        critStartExtreme    = serializedObject.FindProperty("critStartExtreme");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var weapon = (WeaponDefinition)target;

        // --- UUSI: Normal + Overwatch lohkot yhdessä, includeChildren=true näyttää kentät siististi ---
        EditorGUILayout.PropertyField(normalShooting, new GUIContent("Normal Shooting"), true);
        EditorGUILayout.Space(6);
        EditorGUILayout.PropertyField(overwatch,      new GUIContent("Overwatch Mode"),  true);
        EditorGUILayout.Space(10);

        // Range & damage
        EditorGUILayout.PropertyField(maxShootRange);
        EditorGUILayout.PropertyField(noCoverDamageBonus);
        EditorGUILayout.Space(5);

        EditorGUILayout.PropertyField(baseDamage);
        EditorGUILayout.PropertyField(critBonusDamage);
        EditorGUILayout.PropertyField(grazeFactor);
        EditorGUILayout.PropertyField(missChipFactor);
        EditorGUILayout.Space(8);

        // Burst
        EditorGUILayout.LabelField("Burst Fire Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(burstMin,       new GUIContent("Burst Min", "Minimum shots per burst"));
        EditorGUILayout.PropertyField(burstMax,       new GUIContent("Burst Max", "Maximum shots per burst"));
        EditorGUILayout.PropertyField(burstShotDelay, new GUIContent("Burst Shot Delay", "Time between shots in a burst (seconds)"));
        EditorGUILayout.Space(8);

        // Legacy per-weapon ranges
        EditorGUILayout.LabelField("Legacy per-weapon ranges (used if no global CombatRanges found)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(closeMax);
        EditorGUILayout.PropertyField(mediumMax);
        EditorGUILayout.PropertyField(longMax);
        EditorGUILayout.Space(8);

        // Tilastopreview (vain jos burst > 1)
        if (weapon.burstMax > 1)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Statistics Preview Settings", EditorStyles.boldLabel);
            selectedArchetype = (UnitArchetype)EditorGUILayout.ObjectField("Unit Archetype", selectedArchetype, typeof(UnitArchetype), false);
            if (selectedArchetype != null)
                skillLevel = EditorGUILayout.IntSlider("Skill Level", skillLevel, 0, 10);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        // Per-band tunables + previewt
        DrawRangeBandWithStatistics(weapon, melee,   "Melee",  RangeBand.Melee);
        DrawRangeBandWithStatistics(weapon, close,   "Close",  RangeBand.Close);
        DrawRangeBandWithStatistics(weapon, medium,  "Medium", RangeBand.Medium);
        DrawRangeBandWithStatistics(weapon, longRange,"Long",  RangeBand.Long);
        DrawRangeBandWithStatistics(weapon, extreme, "Extreme",RangeBand.Extreme);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Legacy baselines (ignored if useAdvancedAccuracy==true)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(meleeAcc);
        EditorGUILayout.PropertyField(closeAcc);
        EditorGUILayout.PropertyField(mediumAcc);
        EditorGUILayout.PropertyField(longAcc);
        EditorGUILayout.PropertyField(extremeAcc);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Legacy crit starts (ignored if useAdvancedAccuracy==true)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(critStartMelee);
        EditorGUILayout.PropertyField(critStartClose);
        EditorGUILayout.PropertyField(critStartMedium);
        EditorGUILayout.PropertyField(critStartLong);
        EditorGUILayout.PropertyField(critStartExtreme);

        serializedObject.ApplyModifiedProperties();
    }

    // --- alla on sinun aiemmat helperit, sellaisenaan ---
    private void DrawRangeBandWithStatistics(WeaponDefinition weapon, SerializedProperty rangeBandProperty, string label, RangeBand band)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.PropertyField(rangeBandProperty, new GUIContent(label), true);

        if (weapon.burstMax > 1 && rangeBandProperty.isExpanded)
        {
            EditorGUILayout.Space(5);
            DrawBurstProbabilities(weapon, band);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void DrawBurstProbabilities(WeaponDefinition weapon, RangeBand band)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField($"━━━ Burst Statistics for {band} ━━━", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(3);

        RangeBandTuning tuning = weapon.GetTuning(band);
        int baseHitChance = tuning.baseHitChance;

        int skillBonus = 0;
        if (selectedArchetype != null)
            skillBonus = skillLevel * selectedArchetype.accuracyBonusPerSkillLevel;

        if (selectedArchetype != null)
        {
            DrawCoverScenario(weapon, band, tuning, baseHitChance, skillBonus, selectedArchetype.LowCoverEnemyHitPenalty, "Low Cover",  new Color(0.7f, 0.7f, 0.3f));
            DrawCoverScenario(weapon, band, tuning, baseHitChance, skillBonus, selectedArchetype.highCoverEnemyHitPenalty,"High Cover", new Color(0.7f, 0.3f, 0.3f));
            DrawCoverScenario(weapon, band, tuning, baseHitChance, skillBonus, 0,                                           "No Cover", new Color(0.3f, 0.7f, 0.3f));
        }
        else
        {
            DrawCoverScenario(weapon, band, tuning, baseHitChance, 0, 0, "Base", new Color(0.3f, 0.6f, 0.6f));
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawCoverScenario(WeaponDefinition weapon, RangeBand band, RangeBandTuning tuning, int baseHitChance, int skillBonus, int coverPenalty, string scenarioLabel, Color headerColor)
    {
        int finalHitChance = Mathf.Clamp(baseHitChance + skillBonus - coverPenalty, 0, 100);
        float hitProbability = finalHitChance / 100f;

        Color originalBgColor = GUI.backgroundColor;
        GUI.backgroundColor = headerColor;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = originalBgColor;

        string modifierText = "";
        if (skillBonus > 0) modifierText += $"+{skillBonus}% skill";
        if (coverPenalty > 0)
        {
            if (modifierText.Length > 0) modifierText += ", ";
            modifierText += $"-{coverPenalty}% cover";
        }

        EditorGUILayout.LabelField($"┌─ {scenarioLabel} ─┐", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Hit: {baseHitChance}% {(modifierText.Length > 0 ? $"({modifierText})" : "")} = {finalHitChance}%", EditorStyles.miniLabel);
        EditorGUILayout.Space(2);

        if (weapon.burstMin == weapon.burstMax)
        {
            int burstSize = weapon.burstMin;
            DrawProbabilitiesForBurstSize(burstSize, hitProbability, tuning, false);
        }
        else
        {
            EditorGUILayout.LabelField($"Burst Range: {weapon.burstMin}-{weapon.burstMax} shots", EditorStyles.miniLabel);

            for (int burstSize = weapon.burstMin; burstSize <= weapon.burstMax; burstSize++)
            {
                EditorGUILayout.LabelField($"If {burstSize} shots:", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                DrawProbabilitiesForBurstSize(burstSize, hitProbability, tuning, true);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void DrawProbabilitiesForBurstSize(int burstSize, float hitProbability, RangeBandTuning tuning, bool compact)
    {
        float atLeastOneHit = 1f - Mathf.Pow(1f - hitProbability, burstSize);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("≥1 Hit:", EditorStyles.boldLabel, GUILayout.Width(80));
        DrawProbabilityBar(atLeastOneHit, true);
        EditorGUILayout.LabelField($"{atLeastOneHit * 100:F1}%", EditorStyles.boldLabel, GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();

        if (!compact)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Hit Type Distribution (when hit):", EditorStyles.miniLabel);

            int totalWeight = tuning.onHit_Close + tuning.onHit_Graze + tuning.onHit_Hit + tuning.onHit_Crit;
            if (totalWeight > 0)
            {
                DrawHitTypeDistribution("Close", tuning.onHit_Close, totalWeight, new Color(1f, 0.6f, 0.2f));
                DrawHitTypeDistribution("Graze", tuning.onHit_Graze, totalWeight, new Color(1f, 1f, 0.3f));
                DrawHitTypeDistribution("Hit",   tuning.onHit_Hit,   totalWeight, new Color(0.3f, 0.7f, 1f));
                DrawHitTypeDistribution("Crit",  tuning.onHit_Crit,  totalWeight, new Color(0.3f, 1f, 0.3f));
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Exact Hit Count:", EditorStyles.miniLabel);
        }

        for (int hits = 0; hits <= burstSize; hits++)
        {
            float probability = CalculateBinomialProbability(burstSize, hits, hitProbability);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{hits} hit{(hits != 1 ? "s" : "")}:", GUILayout.Width(80));
            DrawProbabilityBar(probability, false);
            EditorGUILayout.LabelField($"{probability * 100:F1}%", GUILayout.Width(55));
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawHitTypeDistribution(string label, int weight, int totalWeight, Color barColor)
    {
        if (totalWeight == 0) return;

        float probability = (float)weight / totalWeight;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"  {label}:", GUILayout.Width(80));

        Rect rect = GUILayoutUtility.GetRect(18, 16, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
        Rect fillRect = new Rect(rect.x, rect.y, rect.width * probability, rect.height);
        EditorGUI.DrawRect(fillRect, barColor);
        DrawRectBorder(rect);

        EditorGUILayout.LabelField($"{probability * 100:F1}%", GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawProbabilityBar(float probability, bool bold)
    {
        Rect rect = GUILayoutUtility.GetRect(18, bold ? 20 : 16, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

        Rect fillRect = new Rect(rect.x, rect.y, rect.width * probability, rect.height);
        Color barColor = Color.Lerp(new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.8f, 0.2f), probability);
        EditorGUI.DrawRect(fillRect, barColor);

        DrawRectBorder(rect);
    }

    private void DrawRectBorder(Rect rect)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), Color.black);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), Color.black);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), Color.black);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), Color.black);
    }

    private float CalculateBinomialProbability(int n, int k, float p)
    {
        float binomialCoeff = BinomialCoefficient(n, k);
        return binomialCoeff * Mathf.Pow(p, k) * Mathf.Pow(1f - p, n - k);
    }

    private float BinomialCoefficient(int n, int k)
    {
        if (k > n) return 0;
        if (k == 0 || k == n) return 1;

        float result = 1;
        for (int i = 0; i < k; i++)
        {
            result *= (n - i);
            result /= (i + 1);
        }
        return result;
    }
}
