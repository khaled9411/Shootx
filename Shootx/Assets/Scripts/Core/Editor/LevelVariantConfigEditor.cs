using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

[CustomEditor(typeof(LevelVariantConfig))]
public class LevelVariantConfigEditor : Editor
{

    private int _totalBaseLevels = 50;
    private int _totalDisplayLevels = 500;
    private int _maxChildIndex = 5;
    private bool _showGeneratorPanel = true;
    private bool _showPreviewTable = false;
    private int _previewCount = 20;

    private bool _allowGlobalIndex = true;
    private bool _allowRandom = true;
    private bool _allowWeightedRandom = false;
    private bool _allowNoChange = false;


    private GUIStyle _headerStyle;
    private GUIStyle _subHeaderStyle;
    private GUIStyle _boxStyle;
    private bool _stylesInit;

    private void InitStyles()
    {
        if (_stylesInit) return;
        _stylesInit = true;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter
        };

        _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11
        };

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 8, 8)
        };
    }

    public override void OnInspectorGUI()
    {
        InitStyles();

        serializedObject.Update();

        // ===== Generator Panel =====
        _showGeneratorPanel = EditorGUILayout.Foldout(_showGeneratorPanel, "Automatic Sequence Generator", true, EditorStyles.foldoutHeader);

        if (_showGeneratorPanel)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(4);

            GUILayout.Label("Generation settings", _subHeaderStyle);
            GUILayout.Space(4);

            _totalBaseLevels = EditorGUILayout.IntField(
                new GUIContent("Number of original levels (Base Levels)",
                               "Number of prefabs available — Level1 to LevelN"),
                _totalBaseLevels);
            _totalBaseLevels = Mathf.Max(1, _totalBaseLevels);

            _totalDisplayLevels = EditorGUILayout.IntField(
                new GUIContent("Total levels for the player",
                               "The 500 levels that the player will see"),
                _totalDisplayLevels);
            _totalDisplayLevels = Mathf.Max(_totalBaseLevels, _totalDisplayLevels);

            _maxChildIndex = EditorGUILayout.IntField(
                new GUIContent("Maximum Child Index Available",
                               "Number of available shapes - 1 (e.g., 5 means shapes 0,1,2,3,4,5)"),
                _maxChildIndex);
            _maxChildIndex = Mathf.Max(0, _maxChildIndex);

            GUILayout.Space(8);

            GUILayout.Label("Mode types in the random section", _subHeaderStyle);
            GUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            _allowGlobalIndex = GUILayout.Toggle(_allowGlobalIndex, " GlobalIndex", GUILayout.Width(120));
            _allowRandom = GUILayout.Toggle(_allowRandom, " Random", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _allowWeightedRandom = GUILayout.Toggle(_allowWeightedRandom, " WeightedRandom", GUILayout.Width(120));
            _allowNoChange = GUILayout.Toggle(_allowNoChange, " NoChange", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            if (!_allowGlobalIndex && !_allowRandom && !_allowWeightedRandom && !_allowNoChange)
            {
                _allowGlobalIndex = true;
                EditorGUILayout.HelpBox("At least one type must be chosen.", MessageType.Warning);
            }

            GUILayout.Space(8);

            int randomCount = _totalDisplayLevels - _totalBaseLevels;
            EditorGUILayout.HelpBox(
                $"Result:\n" +
                $"- Levels 1 to {_totalBaseLevels}: Base 1 to {_totalBaseLevels}  |  Mode = NoChange\n" +
                $"- Levels {_totalBaseLevels + 1} to {_totalDisplayLevels}: Base Random  |  Mode Random ({randomCount} levels)",
                MessageType.Info);

            GUILayout.Space(6);

            // --- Generate Button ---
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Generate Sequence", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog(
                    "Confirm Generation",
                    $"The current sequence will be replaced with {_totalDisplayLevels} elements.\nAre you sure?",
                    "Yes, Generate",
                    "Cancel"))
                {
                    GenerateSequence();
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(8);

        // ===== Preview Table =====
        _showPreviewTable = EditorGUILayout.Foldout(_showPreviewTable, "Preview First Elements of the Sequence", true, EditorStyles.foldoutHeader);

        if (_showPreviewTable)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(4);

            _previewCount = EditorGUILayout.IntSlider("Number of elements to preview", _previewCount, 5, 100);

            var listProp = serializedObject.FindProperty("levelSequence");
            int showCount = Mathf.Min(_previewCount, listProp.arraySize);

            if (showCount == 0)
            {
                EditorGUILayout.HelpBox("The sequence is empty — use the generator above.", MessageType.Warning);
            }
            else
            {
                // header
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Player Level", EditorStyles.miniLabel, GUILayout.Width(90));
                GUILayout.Label("Base Level", EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label("Mode", EditorStyles.miniLabel, GUILayout.Width(120));
                GUILayout.Label("Child Index", EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();

                DrawSeparator();

                for (int i = 0; i < showCount; i++)
                {
                    var elem = listProp.GetArrayElementAtIndex(i);
                    var baseLevel = elem.FindPropertyRelative("baseLevelNumber").intValue;
                    var mode = (EnemySelectionMode)elem.FindPropertyRelative("selectionMode").enumValueIndex;
                    var childIdx = elem.FindPropertyRelative("globalChildIndex").intValue;

                    if (i < _totalBaseLevels)
                        GUI.backgroundColor = new Color(0.85f, 0.95f, 0.85f);
                    else
                        GUI.backgroundColor = new Color(0.85f, 0.90f, 1.0f);

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    GUI.backgroundColor = Color.white;

                    GUILayout.Label($"#{i + 1}", GUILayout.Width(90));
                    GUILayout.Label($"Level{baseLevel}", GUILayout.Width(80));
                    GUILayout.Label(mode.ToString(), GUILayout.Width(120));
                    GUILayout.Label(
                        mode == EnemySelectionMode.GlobalIndex ? childIdx.ToString() : "—",
                        GUILayout.Width(80));

                    EditorGUILayout.EndHorizontal();
                }

                if (listProp.arraySize > _previewCount)
                {
                    EditorGUILayout.HelpBox(
                        $"... and {listProp.arraySize - _previewCount} more elements (increase preview to see)",
                        MessageType.None);
                }
            }

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(8);

        // ===== Default Inspector =====
        GUILayout.Label("The Sequence (for manual editing)", _subHeaderStyle);
        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }


    private void GenerateSequence()
    {
        var config = (LevelVariantConfig)target;
        var listProp = serializedObject.FindProperty("levelSequence");

        listProp.ClearArray();

        for (int i = 0; i < _totalBaseLevels; i++)
        {
            listProp.InsertArrayElementAtIndex(i);
            var elem = listProp.GetArrayElementAtIndex(i);

            elem.FindPropertyRelative("baseLevelNumber").intValue = i + 1;
            elem.FindPropertyRelative("selectionMode").enumValueIndex = (int)EnemySelectionMode.NoChange;
            elem.FindPropertyRelative("globalChildIndex").intValue = 0;
        }

        var allowedModes = BuildAllowedModes();

        int groupCount = _totalBaseLevels / 5;
        int randomCount = _totalDisplayLevels - _totalBaseLevels;
        int insertIndex = _totalBaseLevels;
        int fullGroups = randomCount / 5;
        int remainder = randomCount % 5;
        int lastGroupStart = -1;

        for (int g = 0; g < fullGroups; g++)
        {
            int groupStart = PickGroupStart(groupCount, lastGroupStart);
            lastGroupStart = groupStart;

            EnemySelectionMode groupMode = allowedModes[Random.Range(0, allowedModes.Count)];
            int groupChild = Random.Range(0, _maxChildIndex + 1);

            for (int j = 0; j < 5; j++)
            {
                listProp.InsertArrayElementAtIndex(insertIndex);
                var elem = listProp.GetArrayElementAtIndex(insertIndex);

                elem.FindPropertyRelative("baseLevelNumber").intValue = groupStart + j + 1;
                elem.FindPropertyRelative("selectionMode").enumValueIndex = (int)groupMode;
                elem.FindPropertyRelative("globalChildIndex").intValue =
                    groupMode == EnemySelectionMode.GlobalIndex ? groupChild : 0;

                if (groupMode == EnemySelectionMode.WeightedRandom)
                    FillWeightedRandom(elem);

                insertIndex++;
            }
        }

        if (remainder > 0)
        {
            int groupStart = PickGroupStart(groupCount, lastGroupStart);
            EnemySelectionMode groupMode = allowedModes[Random.Range(0, allowedModes.Count)];
            int groupChild = Random.Range(0, _maxChildIndex + 1);

            for (int j = 0; j < remainder; j++)
            {
                listProp.InsertArrayElementAtIndex(insertIndex);
                var elem = listProp.GetArrayElementAtIndex(insertIndex);

                elem.FindPropertyRelative("baseLevelNumber").intValue = groupStart + j + 1;
                elem.FindPropertyRelative("selectionMode").enumValueIndex = (int)groupMode;
                elem.FindPropertyRelative("globalChildIndex").intValue =
                    groupMode == EnemySelectionMode.GlobalIndex ? groupChild : 0;

                if (groupMode == EnemySelectionMode.WeightedRandom)
                    FillWeightedRandom(elem);

                insertIndex++;
            }
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(config);

        Debug.Log($"[LevelVariantConfigEditor] Generated {_totalDisplayLevels} levels: " +
                  $"{_totalBaseLevels} fixed + {_totalDisplayLevels - _totalBaseLevels} random.");
    }


    private int PickGroupStart(int groupCount, int lastGroupStart)
    {
        if (groupCount <= 1) return 0;

        int picked;
        int attempts = 0;
        do
        {
            picked = Random.Range(0, groupCount) * 5;
            attempts++;
        }
        while (picked == lastGroupStart && attempts < 20);

        return picked;
    }

    private void FillWeightedRandom(UnityEditor.SerializedProperty elem)
    {
        var weightsArr = elem.FindPropertyRelative("weightedRandomWeights");
        int childCount = _maxChildIndex + 1;
        weightsArr.arraySize = childCount;

        float remaining = 100f;
        for (int w = 0; w < childCount - 1; w++)
        {
            float val = Mathf.Round(Random.Range(10f, remaining - 10f * (childCount - 1 - w)));
            weightsArr.GetArrayElementAtIndex(w).floatValue = val;
            remaining -= val;
        }
        weightsArr.GetArrayElementAtIndex(childCount - 1).floatValue = remaining;
    }

    private List<EnemySelectionMode> BuildAllowedModes()
    {
        var list = new List<EnemySelectionMode>();
        if (_allowGlobalIndex) list.Add(EnemySelectionMode.GlobalIndex);
        if (_allowRandom) list.Add(EnemySelectionMode.Random);
        if (_allowWeightedRandom) list.Add(EnemySelectionMode.WeightedRandom);
        if (_allowNoChange) list.Add(EnemySelectionMode.NoChange);
        return list;
    }

    private void DrawSeparator()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        GUILayout.Space(2);
    }
}