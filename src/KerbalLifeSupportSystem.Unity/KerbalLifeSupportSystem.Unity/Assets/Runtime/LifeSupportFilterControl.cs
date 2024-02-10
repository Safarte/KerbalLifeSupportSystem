// ReSharper disable CheckNamespace

using System;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.Unity.Runtime
{
    public class LifeSupportFilterControl : VisualElement
    {
        public enum FilterType
        {
            Vessel,
            Kerbal,
            Both
        }

        // USS Class names
        private const string ClassName = "lsFilter";
        private const string SelectorClassName = ClassName + "__select";
        private const string SelectedClassName = SelectorClassName + "Selected";

        // Internal elements
        private readonly Label _selectorBoth;
        private readonly Label _selectorKerbal;
        private readonly Label _selectorVessel;

        // Selected filter type
        public FilterType SelectedType = FilterType.Both;

        public LifeSupportFilterControl()
        {
            AddToClassList(ClassName);

            _selectorVessel = new Label { name = "ls-select-vessel", text = "Vessels" };
            _selectorVessel.AddManipulator(new Clickable(() => UpdateFilterType(FilterType.Vessel)));
            _selectorVessel.AddToClassList(SelectorClassName);
            hierarchy.Add(_selectorVessel);

            _selectorKerbal = new Label { name = "ls-select-vessel", text = "Kerbals" };
            _selectorKerbal.AddManipulator(new Clickable(() => UpdateFilterType(FilterType.Kerbal)));
            _selectorKerbal.AddToClassList(SelectorClassName);
            hierarchy.Add(_selectorKerbal);

            _selectorBoth = new Label { name = "ls-select-vessel", text = "Both" };
            _selectorBoth.AddManipulator(new Clickable(() => UpdateFilterType(FilterType.Both)));
            _selectorBoth.AddToClassList(SelectorClassName);
            hierarchy.Add(_selectorBoth);

            UpdateSelectionColor();
        }

        private void UpdateFilterType(FilterType type)
        {
            SelectedType = type;
            UpdateSelectionColor();
        }

        private void UpdateSelectionColor()
        {
            _selectorVessel.EnableInClassList(SelectedClassName, SelectedType == FilterType.Vessel);
            _selectorKerbal.EnableInClassList(SelectedClassName, SelectedType == FilterType.Kerbal);
            _selectorBoth.EnableInClassList(SelectedClassName, SelectedType == FilterType.Both);
        }

        public new class UxmlFactory : UxmlFactory<LifeSupportFilterControl>
        {
        }
    }
}