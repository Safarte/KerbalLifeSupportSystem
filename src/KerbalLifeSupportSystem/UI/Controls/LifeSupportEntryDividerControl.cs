using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.UI
{
    public class LifeSupportEntryDividerControl : VisualElement
    {
        public static string UssClassName = "ls-entry-divider";

        public LifeSupportEntryDividerControl()
        {
            AddToClassList(UssClassName);
        }
    }
}
