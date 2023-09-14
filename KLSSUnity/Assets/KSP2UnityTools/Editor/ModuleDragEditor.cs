
using System.Reflection;
using KSP.Modules;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Module_Drag))]
public class ModuleDragEditor : Editor
{
    [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
    public static void DrawGizmosForDrag(Module_Drag moduleDrag, GizmoType gizmoType)
    {
        if (!PartEditor.DragCubeGizmos) return;
        var mat = moduleDrag.gameObject.transform.localToWorldMatrix;
        var dataDrag = moduleDrag.GetType().GetField("dataDrag", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(moduleDrag) as Data_Drag;

        Gizmos.color = new Color(Color.cyan.r, Color.cyan.g, Color.cyan.b, 0.5f);
        if (dataDrag == null) return;
        foreach (var cube in dataDrag.cubes)
        {
            Gizmos.DrawCube(mat.MultiplyPoint(cube.Center), mat.MultiplyVector(cube.Size));
        }
    }
}