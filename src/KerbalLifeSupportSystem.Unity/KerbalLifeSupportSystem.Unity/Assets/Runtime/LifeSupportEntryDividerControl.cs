// ReSharper disable CheckNamespace

using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.Unity.Runtime
{
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

        public new class UxmlFactory : UxmlFactory<LifeSupportEntryDividerControl>
        {
        }
    }
}