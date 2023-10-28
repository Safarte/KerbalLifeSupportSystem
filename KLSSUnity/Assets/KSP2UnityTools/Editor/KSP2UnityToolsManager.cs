using KSP2UT.KSP2UnityTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Windows;

namespace Editor.KSP2UnityTools.Editor
{

    public static class KSP2UnityToolsManager
    {
        public static readonly KSP2UnityToolsSettings Settings;

        static KSP2UnityToolsManager()
        {
            if (!File.Exists("Assets/KSP2UTSettings.asset"))
            {
                Settings = ScriptableObject.CreateInstance<KSP2UnityToolsSettings>();
                AssetDatabase.CreateAsset(Settings, "Assets/KSP2UTSettings.asset");
                AssetDatabase.SaveAssets();
            }
            else
            {
                Settings = AssetDatabase.LoadAssetAtPath<KSP2UnityToolsSettings>("Assets/KSP2UTSettings.asset");
            }
        }
    }
}