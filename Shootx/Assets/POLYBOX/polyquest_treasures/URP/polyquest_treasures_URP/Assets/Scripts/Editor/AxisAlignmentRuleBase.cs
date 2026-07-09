using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "AxisAlignmentRuleData", menuName = "AxisAlignmentRuleData", order = 1)]
public class AxisAlignmentRuleBase : ScriptableObject
{
    [System.Serializable]
    public class AxisAlignmentRule
    {
        public string RuleText;
        public AxisToRotate MatchForward = AxisToRotate.Forward;
        public AxisToRotate MatchUp = AxisToRotate.Up;
        public bool WrangleScales = false;
        public scaleAxises ScaleAxisX = scaleAxises.X;
        public scaleAxises ScaleAxisY = scaleAxises.Y;
        public scaleAxises ScaleAxisZ = scaleAxises.Z;
    }

    public List<AxisAlignmentRule> AxisAlignmentRuleList = new List<AxisAlignmentRule>();

    public Vector3[] vectorAxes = new Vector3[]
    {
        Vector3.back,
        Vector3.down,
        Vector3.forward,
        Vector3.left,
        Vector3.right,
        Vector3.up,
        Vector3.zero
    };
    public enum AxisToRotate { Back, Down, Forward, Left, Right, Up, Zero };

    public Vector3[] scaleAxis = new Vector3[]
    {
        Vector3.right,
        Vector3.up,
        Vector3.forward,
    };
    public enum scaleAxises { X, Y, Z };
}


