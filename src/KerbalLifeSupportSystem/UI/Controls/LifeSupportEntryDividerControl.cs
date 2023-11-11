using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.UI;

/// <summary>
///     Life-Support UI entry horizontal divider
/// </summary>
public class LifeSupportEntryDividerControl : VisualElement
{
    private const string ClassName = "ls-entry-divider";

    public LifeSupportEntryDividerControl()
    {
        AddToClassList(ClassName);
    }
}