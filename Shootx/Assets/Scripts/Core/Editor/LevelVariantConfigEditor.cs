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
                new GUIContent("Base Levels",
                               "Number of prefabs available — Level1 to LevelN"),
                _totalBaseLevels);
            _totalBaseLevels = Mathf.Max(1, _totalBaseLevels);

            _totalDisplayLevels = EditorGUILayout.IntField(
                new GUIContent("Total Display Levels",
                               "The 500 levels that the player sees"),
                _totalDisplayLevels);
            _totalDisplayLevels = Mathf.Max(_totalBaseLevels, _totalDisplayLevels);

            _maxChildIndex = EditorGUILayout.IntField(
                new GUIContent("Max Child Index",
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

            // --- Summary ---
            int randomCount = _totalDisplayLevels - _totalBaseLevels;
            EditorGUILayout.HelpBox(
                $"Result:\n" +
                $"- Levels 1 to {_totalBaseLevels}: Base 1 to {_totalBaseLevels}  |  Mode = NoChange\n" +
                $"- Levels {_totalBaseLevels + 1} to {_totalDisplayLevels}: Base Random  |  Mode Random ({randomCount} levels)",
                MessageType.Info);

            GUILayout.Space(6);

            // --- Generate Button ---
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Generating the sequence", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog(
                    "Confirmation of delivery",
                    $"The current sequence will be replaced with {_totalDisplayLevels} elements.\nAre you sure?",
                    "Yes, generate",
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
        _showPreviewTable = EditorGUILayout.Foldout(_showPreviewTable, "Previewing the first elements of the sequence", true, EditorStyles.foldoutHeader);

        if (_showPreviewTable)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(4);

            _previewCount = EditorGUILayout.IntSlider("Number of items to preview", _previewCount, 5, 100);

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
                GUILayout.Label("Player level", EditorStyles.miniLabel, GUILayout.Width(90));
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
                        $"...and {listProp.arraySize - _previewCount} is an additional element (increase the preview for viewing)",
                        MessageType.None);
                }
            }

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(8);

        GUILayout.Label("Sequence (for manual editing)", _subHeaderStyle);
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

        for (int i = _totalBaseLevels; i < _totalDisplayLevels; i++)
        {
            listProp.InsertArrayElementAtIndex(i);
            var elem = listProp.GetArrayElementAtIndex(i);

            int randomBase = Random.Range(1, _totalBaseLevels + 1);
            EnemySelectionMode randomMode = allowedModes[Random.Range(0, allowedModes.Count)];
            int randomChild = Random.Range(0, _maxChildIndex + 1);

            elem.FindPropertyRelative("baseLevelNumber").intValue = randomBase;
            elem.FindPropertyRelative("selectionMode").enumValueIndex = (int)randomMode;
            elem.FindPropertyRelative("globalChildIndex").intValue =
                randomMode == EnemySelectionMode.GlobalIndex ? randomChild : 0;

            if (randomMode == EnemySelectionMode.WeightedRandom)
            {
                var weightsArr = elem.FindPropertyRelative("weightedRandomWeights");
                int childCount = _maxChildIndex + 1;
                weightsArr.arraySize = childCount;

                float remaining = 100f;
                for (int w = 0; w < childCount - 1; w++)
                {
                    float val = Random.Range(10f, remaining - (10f * (childCount - 1 - w)));
                    val = Mathf.Round(val);
                    weightsArr.GetArrayElementAtIndex(w).floatValue = val;
                    remaining -= val;
                }
                weightsArr.GetArrayElementAtIndex(childCount - 1).floatValue = remaining;
            }
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(config);

        Debug.Log($"[LevelVariantConfigEditor] {_totalDisplayLevels} level generated:" +
                  $"{_totalBaseLevels} fixed + {_totalDisplayLevels - _totalBaseLevels} random.");
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