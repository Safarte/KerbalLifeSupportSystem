
using KSP.Modules;
using KSP.OAB;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Module_Engine))]
public class EngineDataEditor : Editor
{
    // [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
    // public static void DrawGizmoForEngineData(Module_Engine engine, GizmoType gizmoType)
    // {
    //     CenterOfThrustQuery query = new CenterOfThrustQuery();
    //     engine.OnCenterOfThrustQuery(query);
    //     Gizmos.color = Color.magenta;
    //     Debug.Log(query.pos);
    //     Debug.Log(query.dir);
    //     Gizmos.DrawIcon(query.pos, "cot_icon.png", false);
    //     Gizmos.DrawRay(query.pos,query.dir);
    // }
}
