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

        private bool expanded = false;
        private LSEntryData data;

        public void SetValues(LSEntryData data)
        {
            name = "ls-entry__" + data.VesselName;

            this.data = data;

            CurrentCew.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            MaximumCrew.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

            string headerTitle = data.VesselName + " " + (expanded ? "▼" : "▶");
            string curCrewTitle = $"Current Crew ({data.CurrentCrew}):";
            string maxCrewTitle = $"Maximum Crew ({data.MaximumCrew}):";

            Header.SetValues(headerTitle, data.CurFood, data.CurWater, data.CurOxygen, !expanded);
            CurrentCew.SetValues(curCrewTitle, data.CurFood, data.CurWater, data.CurOxygen, expanded);
            MaximumCrew.SetValues(maxCrewTitle, data.MaxFood, data.MaxWater, data.MaxOxygen, expanded);
        }

        public LifeSupportEntryControl(LSEntryData data, bool expanded) : this()
        {
            this.data = data;
            this.expanded = expanded;
            SetValues(data);
        }

        public LifeSupportEntryControl()
        {
            AddToClassList(UssClassName);
            Header = new LifeSupportSubEntryControl();
            Header.TitleLabel.AddManipulator(new Clickable(ToggleExpanded));
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

        public void ToggleExpanded()
        {
            expanded = !expanded;

            SetValues(data);
        }

        public struct LSEntryData
        {
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
