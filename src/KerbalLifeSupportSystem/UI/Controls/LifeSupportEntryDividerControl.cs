using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.UI;

public class LifeSupportEntryDividerControl : VisualElement
{
    private const string ClassName = "ls-entry-divider";

    public LifeSupportEntryDividerControl()
    {
        AddToClassList(ClassName);
    }
}