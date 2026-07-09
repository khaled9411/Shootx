using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using static AxisAlignmentRuleBase;
using System.IO;

public class PolyboxToolsWindow : EditorWindow
{
    const string VersionNumber = "1.08";

    #region Window
    [SerializeField] Texture2D PolyboxToolboxTitle;
    Transform CopyTransformsFrom;
    const float WindowSizeX = 380;
    const float WindowSizeY = 375;
    private int selectedTab = 0;
    private string[] tabNames = { "Axis align", "Material wrangler" };
    [SerializeField] Texture2D UpArrowIcon;
    #endregion

    #region GUI Styles
    GUIStyle ImageStyle, AxisAlignStyle, SubAlignStyle, MaterialWrangleStyle, SubMaterialWrangleStyle;
    #endregion

    #region Axis aliignement
    public AxisAlignmentRuleBase axisAlignmentRuleBase;
    SerializedObject serializedListData;
    SerializedProperty axisAlignmentRuleBaseList;
    Vector2 scrollPosition;

    AxisToRotate DefaultMatchForward = AxisToRotate.Right;
    AxisToRotate DefaultMatchUp = AxisToRotate.Up;

    public Transform TransformToMatch;
    public Transform TransformToWrangle;
    public List<Transform> TransformsToWrangleList = new List<Transform>();
    public List<Transform> TransformsToMatchList = new List<Transform>();
    #endregion

    #region Material clone
    class MatSource
    {
        public Material SourceMaterial;
        public Material Replacement;
    }

    [SerializeField] Texture2D DownArrowIcon;
    List<Material> GrabMaterials = new List<Material>();
    List<Material> CloneGrabMaterials = new List<Material>();
    Transform ParentFromRenderers;
    List<MeshRenderer> FromRenderers = new List<MeshRenderer>();
    Transform ParentToRenderers;
    List<MeshRenderer> ToRenderers = new List<MeshRenderer>();
    List<MatSource> NewMatsSources = new List<MatSource>();
    #endregion

    [MenuItem("Polybox/PolyboxToolsWindow")]
    public static void ShowWindow()
    {
        var win = GetWindow<PolyboxToolsWindow>("Polybox Tools Window");
        win.minSize = new Vector2(WindowSizeX, WindowSizeY);
        win.maxSize = new Vector2(win.minSize.x, 8000);
    }

    private void Awake()
    {

        serializedListData = new SerializedObject(axisAlignmentRuleBase);
    }

    void OnGUI()
    {
        GenerateStyles();

        GUILayout.BeginHorizontal();
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label(PolyboxToolboxTitle, ImageStyle, GUILayout.Width(WindowSizeX), GUILayout.Height(100));
            GUILayout.FlexibleSpace();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Polybox toolbox version {VersionNumber}");
            GUILayout.FlexibleSpace();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        {
            for (int i = 0; i < tabNames.Length; i++)
            {
                GUI.backgroundColor = i == selectedTab ? Color.gray : Color.white;

                if (GUILayout.Button(tabNames[i]))
                {
                    selectedTab = i;
                }

                GUI.backgroundColor = Color.white;
            }
        }
        GUILayout.EndHorizontal();

        switch (selectedTab)
        {
            case 0:
                AxisAlignGUI();
                break;
            case 1:
                MaterialWrangleGUI();
                break;
        }
    }

    void AxisAlignGUI()
    {
        GUILayout.BeginVertical(AxisAlignStyle);
        {
            GUILayout.Label("Axis alignment tool", EditorStyles.boldLabel);
            GUILayout.Space(4);

            GUILayout.BeginVertical(SubAlignStyle);
            {
                GUILayout.Label("Default axis alignment tool", EditorStyles.boldLabel);
                GUILayout.Space(2);
                DefaultMatchForward = (AxisToRotate)EditorGUILayout.EnumPopup("Default Match forward", DefaultMatchForward);
                DefaultMatchUp = (AxisToRotate)EditorGUILayout.EnumPopup("Default Match Up", DefaultMatchUp);
            }
            GUILayout.EndVertical();

            GUILayout.Space(4);

            GUILayout.BeginVertical(SubAlignStyle);
            {
                GUILayout.Label("Custom axis alignment tool", EditorStyles.boldLabel);
                axisAlignmentRuleBaseList = serializedListData.FindProperty("AxisAlignmentRuleList");
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                EditorGUILayout.PropertyField(axisAlignmentRuleBaseList, true);
                EditorGUILayout.EndScrollView();
                serializedListData.ApplyModifiedProperties();
            }
            GUILayout.EndVertical();



            GUILayout.Space(6);

            GUILayout.BeginVertical(SubAlignStyle);
            {
                TransformToMatch = (Transform)EditorGUILayout.ObjectField("Transform to match", TransformToMatch, typeof(Transform), true);

                GUILayout.Label(UpArrowIcon, ImageStyle, GUILayout.Width(WindowSizeX), GUILayout.Height(50));

                TransformToWrangle = (Transform)EditorGUILayout.ObjectField("Transform to wrangle", TransformToWrangle, typeof(Transform), true);
                GUILayout.Space(4);

                if (GUILayout.Button(" Match transforms"))
                {
                    if (TransformToWrangle && TransformToMatch)
                        MatchTransformAll();
                    else
                        EditorUtility.DisplayDialog("Reference objects missing", "Reference object and Align object need to be set", "On it!");
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(4);

            GUILayout.BeginVertical(SubAlignStyle);
            {
                GUILayout.Label("Quick rotate selected local 90", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal(SubAlignStyle);
                {

                    if (GUILayout.Button("X"))
                    {
                        var tempTrasform = Selection.activeTransform;
                        var tempRot = tempTrasform.localEulerAngles;
                        tempRot.x += 90;
                        Undo.RecordObject(tempTrasform, "Rotate object by 90 on X Axis");
                        tempTrasform.localEulerAngles = tempRot;
                    }
                    if (GUILayout.Button("Y"))
                    {
                        var tempTrasform = Selection.activeTransform;
                        var tempRot = tempTrasform.localEulerAngles;
                        tempRot.y += 90;
                        Undo.RecordObject(tempTrasform, "Rotate object by 90 on Y Axis");
                        tempTrasform.localEulerAngles = tempRot;
                    }
                    if (GUILayout.Button("Z"))
                    {
                        var tempTrasform = Selection.activeTransform;
                        var tempRot = tempTrasform.localEulerAngles;
                        tempRot.z += 90;
                        Undo.RecordObject(tempTrasform, "Rotate object by 90 on Z Axis");
                        tempTrasform.localEulerAngles = tempRot;
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
        GUILayout.EndVertical();
    }

    void MaterialWrangleGUI()
    {
        GUILayout.BeginVertical(MaterialWrangleStyle);
        {
            ParentFromRenderers = (Transform)EditorGUILayout.ObjectField("Grab materials from", ParentFromRenderers, typeof(Transform), true);
            EditorGUILayout.LabelField("Child renderers (including inactive) " + FromRenderers.Count);

            GUILayout.Label(DownArrowIcon, ImageStyle, GUILayout.Width(WindowSizeX), GUILayout.Height(100));

            ParentToRenderers = (Transform)EditorGUILayout.ObjectField("Set materials to", ParentToRenderers, typeof(Transform), true);
            EditorGUILayout.LabelField("Child renderers (including inactive) " + ToRenderers.Count);

            if (GUILayout.Button($"Wrangle materials"))
            {
                FromRenderers.Clear();
                FromRenderers = ParentFromRenderers.GetComponentsInChildren<MeshRenderer>(true).ToList();
                ToRenderers.Clear();
                ToRenderers = ParentToRenderers.GetComponentsInChildren<MeshRenderer>(true).ToList();
                WrangleMaterials();
            }
            if (GUILayout.Button("Delete materials just wrangled"))
            {
                if (CloneGrabMaterials.Count <= 0)
                {
                    Debug.LogWarning("No materials has been wrangled");

                }
                else
                {
                    foreach (var mat in CloneGrabMaterials)
                    {
                        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(mat));
                    }
                    CloneGrabMaterials.Clear();
                    AssetDatabase.Refresh();
                }
            }

        }
        GUILayout.EndVertical();
    }

    void WrangleMaterials()
    {
        NewMatsSources.Clear();
        CloneGrabMaterials.Clear();
        var firstRenderDone = false;
        foreach (var ren in FromRenderers)
        {
            if (ren.sharedMaterial == null)
            {
                Debug.LogError("No material set on: " + ren.name, ren.gameObject);
                continue;
            }
            var replacementMats = DuplicateMaterial(ren.sharedMaterials);
            foreach (var mat in replacementMats)
            {
                CloneGrabMaterials.Add(mat);
            }
            if (firstRenderDone == false)
            {
                firstRenderDone = true;
                ToRenderers[0].sharedMaterials = replacementMats;
            }

            foreach (var toRen in ToRenderers)
            {
                if (toRen.name == ren.name)
                {
                    toRen.sharedMaterials = replacementMats;
                    break;
                }
            }
        }
        AssetDatabase.Refresh();
    }

    Material[] DuplicateMaterial(Material[] mats)
    {
        Material newMat;
        var newMats = new List<Material>();
        foreach (var mat in mats)
        {
            bool replacementShaderAlreadyCreated = false;
            foreach (MatSource matSource in NewMatsSources)
            {
                if (mat == matSource.SourceMaterial)
                {
                    newMat = matSource.Replacement;
                    newMats.Add(newMat);
                    replacementShaderAlreadyCreated = true;
                    break;
                }
            }
            if (replacementShaderAlreadyCreated)
                continue;

            var matSourceTemp = new MatSource();
            matSourceTemp.SourceMaterial = mat;
            var orginalMatPath = AssetDatabase.GetAssetPath(mat);
            var orginalMatName = Path.GetFileNameWithoutExtension(orginalMatPath);
            var newMatPath = AssetDatabase.GenerateUniqueAssetPath(orginalMatPath);

            //Has this file already been made?
            if (AssetDatabase.GetMainAssetTypeAtPath(newMatPath) == null)
            {
                if (!AssetDatabase.CopyAsset(orginalMatPath, newMatPath))
                {
                    Debug.LogError($"Failed to copy {newMatPath}");
                    return null;
                }
                else
                {
                    newMat = (Material)AssetDatabase.LoadAssetAtPath(newMatPath, typeof(Material));
                    Debug.Log($"New material created at {newMatPath}", newMat);
                }
            }
            else
            {
                newMat = (Material)AssetDatabase.LoadAssetAtPath(newMatPath, typeof(Material));
                Debug.LogWarning($"New material has already been created at {newMatPath}", newMat);
            }

            matSourceTemp.Replacement = newMat;
            NewMatsSources.Add(matSourceTemp);
            newMats.Add(newMat);
        }
        return newMats.ToArray();
    }

    void MatchTransformAll()
    {
        TransformsToWrangleList.Clear();
        TransformsToMatchList.Clear();

        TransformsToWrangleList = TransformToWrangle.GetComponentsInChildren<Transform>(true).ToList<Transform>();
        TransformsToMatchList = TransformToMatch.GetComponentsInChildren<Transform>(true).ToList<Transform>();

        TransformToWrangle.position = TransformToMatch.position;
        TransformToWrangle.localRotation = TransformToMatch.localRotation;
        TransformToWrangle.localScale = TransformToMatch.localScale;

        foreach (var transformsToWrangle in TransformsToWrangleList)
        {
            foreach (var transformsToMatch in TransformsToMatchList)
            {
                if (transformsToWrangle.name == transformsToMatch.name)
                {
                    bool useDefault = true;

                    foreach (var rule in axisAlignmentRuleBase.AxisAlignmentRuleList)
                    {
                        if (transformsToWrangle.name.Contains(rule.RuleText))
                        {
                            MatchTransformDefault(transformsToWrangle, transformsToMatch, rule.MatchForward, rule.MatchUp, false);
                            if (rule.WrangleScales)
                            {
                                var axisX = GetScaleAxis(rule.ScaleAxisX);
                                var scaleX = new Vector3(transformsToMatch.transform.localScale.x * axisX.x,
                                                        transformsToMatch.transform.localScale.y * axisX.y,
                                                        transformsToMatch.transform.localScale.z * axisX.z).magnitude;

                                var axisY = GetScaleAxis(rule.ScaleAxisY);
                                var scaleY = new Vector3(transformsToMatch.transform.localScale.x * axisY.x,
                                                        transformsToMatch.transform.localScale.y * axisY.y,
                                                        transformsToMatch.transform.localScale.z * axisY.z).magnitude;

                                var axisZ = GetScaleAxis(rule.ScaleAxisZ);
                                var scaleZ = new Vector3(transformsToMatch.transform.localScale.x * axisZ.x,
                                                        transformsToMatch.transform.localScale.y * axisZ.y,
                                                        transformsToMatch.transform.localScale.z * axisZ.z).magnitude;

                                var wrangledLocalScale = new Vector3(scaleX, scaleY, scaleZ);
                                transformsToWrangle.localScale = wrangledLocalScale;
                            }
                            useDefault = false;
                            break;
                        }
                    }

                    if (useDefault)
                        MatchTransformDefault(transformsToWrangle, transformsToMatch, DefaultMatchForward, DefaultMatchUp);
                }
            }
        }
    }

    void MatchTransformDefault(Transform transFrom, Transform transTo, AxisToRotate forwardIn, AxisToRotate upIn, bool withScale = true)
    {
        var toAxis = Vector3.zero;
        var toUpAxis = Vector3.zero;
        toAxis = transTo.TransformDirection(GetAxis(forwardIn));
        toUpAxis = transTo.TransformDirection(GetAxis(upIn));
        Vector3 rightDirection = Vector3.Cross(toUpAxis, toAxis);

        Undo.RecordObject(transFrom, "Change Axis alignment");

        Matrix4x4 rotationMatrix = new Matrix4x4(rightDirection, toUpAxis, toAxis, Vector4.zero);
        rotationMatrix[3, 3] = 1.0f;

        //transFrom.rotation = Quaternion.LookRotation(toAxis, toUpAxis);
        transFrom.rotation = rotationMatrix.rotation;
        transFrom.position = transTo.transform.position;
        if (withScale)
            transFrom.localScale = transTo.transform.localScale;
        transFrom.gameObject.SetActive(transTo.gameObject.activeSelf);
        Debug.Log(transFrom.name);
    }

    public Vector3 GetScaleAxis(scaleAxises axis)
    {
        return axisAlignmentRuleBase.scaleAxis[(int)axis];
    }

    public Vector3 GetAxis(AxisToRotate axis)
    {
        return axisAlignmentRuleBase.vectorAxes[(int)axis];
    }

    private void GenerateStyles()
    {
        ImageStyle = new GUIStyle(EditorStyles.label);
        ImageStyle.alignment = TextAnchor.UpperCenter;

        AxisAlignStyle = new GUIStyle(EditorStyles.label);
        AxisAlignStyle.normal.background = MakeTex(new Color(0.1f, 0.1f, 0.2f));

        SubAlignStyle = new GUIStyle(EditorStyles.label);
        SubAlignStyle.normal.background = MakeTex(new Color(0.05f, 0.05f, 0.1f));

        MaterialWrangleStyle = new GUIStyle(EditorStyles.label);
        MaterialWrangleStyle.normal.background = MakeTex(new Color(0.2f, 0.1f, 0.1f));

        SubMaterialWrangleStyle = new GUIStyle(EditorStyles.label);
        SubMaterialWrangleStyle.normal.background = MakeTex(new Color(0.1f, 0.05f, 0.05f));
    }

    private Texture2D MakeTex(Color textureColor)
    {
        int width = 2;
        Color[] pix = new Color[width * width];
        for (int i = 0; i < pix.Length; i++)
        {
            pix[i] = textureColor;
        }
        Texture2D result = new Texture2D(width, width);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}