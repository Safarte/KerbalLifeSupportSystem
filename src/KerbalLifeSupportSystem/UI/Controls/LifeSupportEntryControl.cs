using KSP.Sim.impl;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.UI
{
    public class LifeSupportEntryControl : VisualElement
    {
        public static string UssClassName = "ls-vessel-entry";

        public static string UssVesselNameClassName = "ls-vessel-sub-entry__vessel-name";

        LifeSupportSubEntryControl Header;
        LifeSupportSubEntryControl CurrentCew;
        LifeSupportSubEntryControl MaximumCrew;
        LifeSupportEntryDividerControl Divider;

        private bool _expanded = false;
        public LSEntryData Data;

        public void SetValues(LSEntryData data, bool isActiveVessel)
        {
            name = "ls-entry__" + data.VesselName;

            Data = data;

            CurrentCew.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;
            MaximumCrew.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;

            string headerTitle = data.VesselName + " " + (_expanded ? "▼" : "▶");
            string curCrewTitle = $"Current Crew ({data.CurrentCrew}):";
            string maxCrewTitle = $"Maximum Crew ({data.MaximumCrew}):";

            Header.SetValues(headerTitle, data.CurFood, data.CurWater, data.CurOxygen, !_expanded);
            Header.TitleLabel.EnableInClassList("ls-vessel-sub-entry__title_active", isActiveVessel);
            CurrentCew.SetValues(curCrewTitle, data.CurFood, data.CurWater, data.CurOxygen, _expanded);
            MaximumCrew.SetValues(maxCrewTitle, data.MaxFood, data.MaxWater, data.MaxOxygen, _expanded);
        }

        public LifeSupportEntryControl(LSEntryData data, bool expanded, bool isActiveVessel) : this()
        {
            Data = data;
            _expanded = expanded;
            SetValues(data, isActiveVessel);
        }

        public LifeSupportEntryControl()
        {
            AddToClassList(UssClassName);
            Header = new LifeSupportSubEntryControl();
            Header.TitleLabel.AddManipulator(new Clickable(() => SetExpanded(!_expanded)));
            Header.TitleLabel.EnableInClassList(UssVesselNameClassName, true);
            hierarchy.Add(Header);

            CurrentCew = new LifeSupportSubEntryControl();
            CurrentCew.style.display = DisplayStyle.None;
            hierarchy.Add(CurrentCew);

            MaximumCrew = new LifeSupportSubEntryControl();
            MaximumCrew.style.display = DisplayStyle.None;
            hierarchy.Add(MaximumCrew);

            Divider = new LifeSupportEntryDividerControl();
            hierarchy.Add(Divider);
        }

        public void SetExpanded(bool newValue) { _expanded = newValue; }

        public struct LSEntryData
        {
            public IGGuid Id;
            public string VesselName;
            public int CurrentCrew;
            public int MaximumCrew;
            public double CurFood;
            public double CurWater;
            public double CurOxygen;
            public double MaxFood;
            public double MaxWater;
            public double MaxOxygen;
        }
    }
}
