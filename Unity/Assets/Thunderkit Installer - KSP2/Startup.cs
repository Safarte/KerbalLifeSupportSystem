using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

[InitializeOnLoad]
public class Startup
{
    public static List<string> Packages { get; set; } = new List<string>() {
#if UNITY_2020_3_33
        "com.unity.ui", 
        "com.unity.ui.builder", 
#endif
        "com.unity.burst",
        "com.unity.inputsystem" 
    };

    static Startup()
    {
        EditorApplication.update += ExecuteCoroutine;
        // StartCoroutine(InstallPackages());
    }

    [MenuItem("Tools/Reinstall Thunderkit")]
    static void ReinstallThunderKit()
    {
        StartCoroutine(InstallPackages());
    }

    private static IEnumerator StartCoroutine(IEnumerator newCorou)
    {
        CoroutineInProgress.Add(newCorou);
        return newCorou;
    }

    private static readonly List<IEnumerator> CoroutineInProgress = new();

    static int currentExecute = 0;
    private static void ExecuteCoroutine()
    {
        if(CoroutineInProgress.Count <= 0)
            return;

        currentExecute = (currentExecute + 1) % CoroutineInProgress.Count;

        bool finish = !CoroutineInProgress[currentExecute].MoveNext();

        if(finish)
            CoroutineInProgress.RemoveAt(currentExecute);
    }

    private static IEnumerator InstallPackages()
    {
        // if(!EditorUtility.DisplayDialog($"Install Thunderkit?", 
        //     "Do you want to run this installer?", 
        //     "Install", "Skip"))
        //     {
        //
        //     if (EditorUtility.DisplayDialog($"Remove Installer?", 
        //         "Do you want to remove this installer package?", "Delete", "Skip") && 
        //         AssetDatabase.DeleteAsset("Assets/Thunderkit Installer"))
        //         Debug.Log($"Installer Removed.");
        //
        //     yield break;
        // }

        var listRequest = Client.List(true, false);
        while(!listRequest.IsCompleted)
        {
            yield return null;
        }

        if(listRequest.Status == StatusCode.Success)
        {
            var collection = listRequest.Result;

            if(!collection.Any(x => x.packageId.StartsWith("com.passivepicasso.thunderkit")))
            {
                var result = Client.Add("https://github.com/PassivePicasso/ThunderKit.git");
                while(!result.IsCompleted)
                {
                    yield return null;
                }

                if(result.Status != StatusCode.Success)
                {
                    Debug.Log(result.Error);
                    yield break;
                }

                Debug.Log($"Installed {"https://github.com/PassivePicasso/ThunderKit.git"}.");
                yield break;
            }

            for(int i = 0; i < Packages.Count; i++)
            {
                string package = Packages[i];
                if(!collection.Any(x => x.packageId.StartsWith(package)))
                {
                    var result = Client.Add(package);
                    while(!result.IsCompleted)
                    {
                        yield return null;
                    }

                    if(result.Status != StatusCode.Success)
                    {
                        Debug.Log(result.Error);
                        yield break;
                    }

                    Debug.Log($"Installed {package}.");
                }
                else
                {
                    Debug.Log($"{package} found.");
                }
            }
            
            EditorUtility.DisplayDialog("Reinstallation Complete", "The reinstallation of thunderkit has succeeded",
                "ok");
            
            Debug.Log($"Installation Complete.");
            yield break;
        }

        Debug.Log($"{listRequest.Error.errorCode}: {listRequest.Error.message}");
    }
}