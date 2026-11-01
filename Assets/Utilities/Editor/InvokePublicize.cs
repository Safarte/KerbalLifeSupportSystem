using System;
using System.Diagnostics;
using ThunderKit.Core.Data;
using UnityEditor;

namespace Utilities.Editor
{
    public static class InvokePublicize
    {
        [MenuItem("Modding/Publicize Assembly-CSharp.dll")]
        public static void Publicize()
        {
            string gameManagedDir = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>().ManagedAssembliesPath;

            if (!gameManagedDir.Contains("KSP2"))
            {
                EditorUtility.DisplayDialog("Publicization", "ThunderKit settings does not contain a location for your redux install, this is necessary for publicization\nPlease rectify this before publicizing.", "Ok");
                return;
            }
            var info = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c publicize.bat \"{gameManagedDir}\"",
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };

            
            using var process = Process.Start(info);
            process!.WaitForExit();
            EditorUtility.DisplayDialog("Publicization", "Publicization should be complete!", "Ok");
        }
    }
}