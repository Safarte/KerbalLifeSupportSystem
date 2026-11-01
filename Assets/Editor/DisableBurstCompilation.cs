using Unity.Burst;
using UnityEditor;

/// <summary>Burst compilation crashes the editor, force it off on load</summary>
[InitializeOnLoad]
public static class DisableBurstCompilation
{
    static DisableBurstCompilation()
    {
        BurstCompiler.Options.EnableBurstCompilation = false;
    }
}
