using KSP.Game;
using KSP.UI.Binding;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.UI;

internal class LifeSupportMonitorUIController : KerbalMonoBehaviour
{
    private static VisualElement s_container;
    private static bool s_initialized = false;

    private void Start()
    {
        SetupDocument();
    }

    private void Update()
    {
        if (!s_initialized)
        {
            InitElements();
        }
    }

    public void SetEnabled(bool newState)
    {
        s_container.style.display = newState ? DisplayStyle.Flex : DisplayStyle.None;
        GameObject.Find(KerbalLifeSupportSystemPlugin.ToolbarOABButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(newState);
    }

    private void SetupDocument()
    {
        var document = GetComponent<UIDocument>();

        // Set up localization
        if (document.TryGetComponent<DocumentLocalization>(out var localization))
        {
            localization.Localize();
        }
        else
        {
            document.EnableLocalization();
        }

        // root Visual Element
        s_container = document.rootVisualElement;

        // Move the GUI to its starting position
        s_container[0].transform.position = new Vector2(500, 50);
        s_container[0].CenterByDefault();

        // Hide the GUI by default
        s_container.style.display = DisplayStyle.None;
    }

    private void InitElements()
    {
        s_initialized = true;
    }
}
