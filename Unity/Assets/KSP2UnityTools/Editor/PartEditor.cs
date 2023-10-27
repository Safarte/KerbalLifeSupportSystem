
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KSP;
using UnityEditor;
using Cheese.Extensions;
using KSP.IO;
using KSP.Modules;
using KSP.Sim.Definitions;
using KSP.Sim.impl;
using Newtonsoft.Json;
using Newtonsoft.Json.UnityConverters;
using Newtonsoft.Json.UnityConverters.Configuration;
using UnityEditor.VersionControl;
using UnityEngine;

[CustomEditor(typeof(CorePartData))]
public class PartEditor : Editor
{

    private static bool _initialized = false;
    private static readonly Color ComColor = new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0.5f);

    private static string _jsonPath = "%NAME%.json";

    private static bool _centerOfMassGizmos = true;
    private static bool _centerOfLiftGizmos = true;
    private static bool _attachNodeGizmos = true;

    public static bool DragCubeGizmos = true;
    
    // Just initialize all the conversion stuff
    private static void Initialize()
    {
        typeof(IOProvider).GetMethod("Init", BindingFlags.Static | BindingFlags.NonPublic)
            ?.Invoke(null, new object[] { });
        _initialized = true;
        Module_Engine mod;
    }

    private void OnSceneGUI()
    {
    }
    private GameObject TargetObject => TargetData.gameObject;
    private CorePartData TargetData => target as CorePartData;
    private PartCore TargetCore => TargetData.Core;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        GUILayout.Label("Gizmo Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _centerOfMassGizmos = EditorGUILayout.Toggle("CoM gizmos", _centerOfMassGizmos);
        _centerOfLiftGizmos = EditorGUILayout.Toggle("CoL gizmos", _centerOfLiftGizmos);
        _attachNodeGizmos = EditorGUILayout.Toggle("Attach Node Gizmos", _attachNodeGizmos);
        DragCubeGizmos = EditorGUILayout.Toggle("Drag Cube Gizmos", DragCubeGizmos);
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(target);
        }
        GUILayout.Label("Part Saving", EditorStyles.boldLabel);
        _jsonPath = EditorGUILayout.TextField("JSON Path",_jsonPath);
        if (!GUILayout.Button("Save Part JSON")) return;
        if (!_initialized) Initialize();
        if (TargetCore == null) return;
        // Clear out the serialized part modules and reserialize them
        TargetCore.data.serializedPartModules.Clear();
        foreach (var child in TargetObject.GetComponents<Component>())
        {
            if (!(child is PartBehaviourModule partBehaviourModule)) continue;
            var addMethod = child.GetType().GetMethod("AddDataModules", BindingFlags.Instance | BindingFlags.NonPublic) ??
                            child.GetType().GetMethod("AddDataModules", BindingFlags.Instance | BindingFlags.Public);
            addMethod?.Invoke(child, new object[] { });
            foreach (var data in partBehaviourModule.DataModules.Values)
            {
                var rebuildMethod = data.GetType().GetMethod("RebuildDataContext", BindingFlags.Instance | BindingFlags.NonPublic) ?? data.GetType().GetMethod("RebuildDataContext", BindingFlags.Instance | BindingFlags.Public);
                rebuildMethod?.Invoke(data, new object[] { });
            }
            TargetCore.data.serializedPartModules.Add(new SerializedPartModule(partBehaviourModule,false));
        }
        var json = IOProvider.ToJson(TargetCore);
        var path = $"{Application.dataPath}/{_jsonPath}";
        path = path.Replace("%NAME%", TargetCore.data.partName);
        File.WriteAllText($"{path}", json);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Part Exported", $"Json is at: {path}", "ok");
    }

    [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
    public static void DrawGizmoForPartCoreData(CorePartData data, GizmoType gizmoType)
    {
        var localToWorldMatrix = data.transform.localToWorldMatrix;
        if (_centerOfMassGizmos)
        {
            var centerOfMassPosition = data.Data.coMassOffset;
            centerOfMassPosition = localToWorldMatrix.MultiplyPoint(centerOfMassPosition);
            Gizmos.DrawIcon(centerOfMassPosition, "com_icon.png", false);
        }
        if (_centerOfLiftGizmos)
        {
            var centerOfLiftPosition = data.Data.coLiftOffset;
            centerOfLiftPosition = localToWorldMatrix.MultiplyPoint(centerOfLiftPosition);
            Gizmos.DrawIcon(centerOfLiftPosition, "col_icon.png", false);
        }
        if (!_attachNodeGizmos) return;
        Gizmos.color = new Color(Color.green.r, Color.green.g, Color.green.b, 0.5f);
        foreach (var attachNode in data.Data.attachNodes)
        {
            var pos = attachNode.position;
            pos = localToWorldMatrix.MultiplyPoint(pos);
            var dir = attachNode.orientation;
            dir = localToWorldMatrix.MultiplyVector(dir);
            Gizmos.DrawRay(pos, dir * 0.25f);
            Gizmos.DrawSphere(pos,0.05f);
        }
    }
}
