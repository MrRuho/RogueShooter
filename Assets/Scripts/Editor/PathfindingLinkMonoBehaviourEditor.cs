using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
[CustomEditor(typeof(PathfindingLinkMonoBehaviour))]
public class PathfindingLinkMonoBehaviourEditor : Editor
{
     
    private void OnSceneGUI()
    {
        var m = (PathfindingLinkMonoBehaviour)target;
        var t = m.transform;

        // Local -> World kahvoille
        Vector3 aW = t.TransformPoint(m.linkPositionA);
        Vector3 bW = t.TransformPoint(m.linkPositionB);

        EditorGUI.BeginChangeCheck();
        Vector3 naW = Handles.PositionHandle(aW, Quaternion.identity);
        Vector3 nbW = Handles.PositionHandle(bW, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(m, "Change Link Position");
            // World -> Local talteen
            m.linkPositionA = t.InverseTransformPoint(naW);
            m.linkPositionB = t.InverseTransformPoint(nbW);
        }
    }
}
