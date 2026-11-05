using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for SoloMainMenuAnimationController.
/// Provides testing controls and a clean visual layout for configuring
/// menu entrance and idle DOTween animations.
/// </summary>
[CustomEditor(typeof(SoloMainMenuAnimationController))]
public class SoloMainMenuAnimationControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SoloMainMenuAnimationController controller = (SoloMainMenuAnimationController)target;

        serializedObject.Update();

        // Status Banner
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Solo Main Menu Animation Controller", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            string state = controller.IsEntrancePlaying ? "Entrance Animation Running" :
                           controller.IsIdlePlaying ? "Idle Breathing Running" : "Stopped / Idle";
            EditorGUILayout.LabelField("Runtime Status:", state, EditorStyles.miniBoldLabel);
        }
        else
        {
            EditorGUILayout.LabelField("Mode:", "Edit Mode (Preview buttons enabled in Play Mode)", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // Runtime Testing Buttons
        if (Application.isPlaying)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Live Animation Testing", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Play Entrance", GUILayout.Height(28)))
            {
                controller.PlayEntranceAnimation();
            }

            if (GUILayout.Button("Start Idle", GUILayout.Height(28)))
            {
                controller.StartIdleAnimations();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Stop Animations", GUILayout.Height(24)))
            {
                controller.StopAllAnimations(restoreTransforms: true);
            }

            if (GUILayout.Button("Reset Transforms", GUILayout.Height(24)))
            {
                controller.ResetToOriginalInstant();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        // Quick Utility: Auto-Resolve Missing References
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (GUILayout.Button("Auto-Resolve Scene References", GUILayout.Height(24)))
        {
            Undo.RecordObject(controller, "Auto-Resolve References");
            controller.AutoResolveReferences();
            EditorUtility.SetDirty(controller);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // Draw Default Inspector Properties with Serialized Object tracking
        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }
}

