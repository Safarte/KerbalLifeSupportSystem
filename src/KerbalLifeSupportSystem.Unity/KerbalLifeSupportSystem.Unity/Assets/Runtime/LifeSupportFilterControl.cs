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
        private readonly Label _selectorVessels;
        private readonly Label _selectorKerbals;
        private readonly Label _selectorBoth;

        public string VesselsSelectText
        {
            set => _selectorVessels.text = value;
        }

        public string KerbalSelectText
        {
            set => _selectorKerbals.text = value;
        }

        public string BothSelectText
        {
            set => _selectorBoth.text = value;
        }

        // Selected filter type
        public FilterType SelectedType = FilterType.Both;

        public LifeSupportFilterControl()
        {
            AddToClassList(ClassName);

            _selectorVessels = new Label { name = "ls-select-vessel", text = "Vessels" };
            _selectorVessels.AddManipulator(new Clickable(() => UpdateFilterType(FilterType.Vessel)));
            _selectorVessels.AddToClassList(SelectorClassName);
            hierarchy.Add(_selectorVessels);

            _selectorKerbals = new Label { name = "ls-select-vessel", text = "Kerbals" };
            _selectorKerbals.AddManipulator(new Clickable(() => UpdateFilterType(FilterType.Kerbal)));
            _selectorKerbals.AddToClassList(SelectorClassName);
            hierarchy.Add(_selectorKerbals);

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
            _selectorVessels.EnableInClassList(SelectedClassName, SelectedType == FilterType.Vessel);
            _selectorKerbals.EnableInClassList(SelectedClassName, SelectedType == FilterType.Kerbal);
            _selectorBoth.EnableInClassList(SelectedClassName, SelectedType == FilterType.Both);
        }

        public new class UxmlFactory : UxmlFactory<LifeSupportFilterControl>
        {
        }
    }
}