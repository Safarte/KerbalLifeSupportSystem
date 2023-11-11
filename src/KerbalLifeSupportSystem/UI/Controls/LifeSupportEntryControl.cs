using KSP.Sim.impl;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.UI;

public class LifeSupportEntryControl : VisualElement
{
    private const string ClassName = "ls-vessel-entry";
    private const string VesselNameClassName = "ls-vessel-sub-entry__vessel-name";
    private readonly LifeSupportSubEntryControl _currentCew;
    private readonly LifeSupportSubEntryControl _header;
    private readonly LifeSupportSubEntryControl _maximumCrew;

    private bool _expanded;
    public LsEntryData Data;

    public LifeSupportEntryControl(LsEntryData data, bool expanded, bool isActiveVessel) : this()
    {
        Data = data;
        _expanded = expanded;
        SetValues(data, isActiveVessel);
    }

    private LifeSupportEntryControl()
    {
        AddToClassList(ClassName);

        _header = new LifeSupportSubEntryControl();
        _header.TitleLabel.AddManipulator(new Clickable(() => SetExpanded(!_expanded)));
        _header.TitleLabel.EnableInClassList(VesselNameClassName, true);
        hierarchy.Add(_header);

        _currentCew = new LifeSupportSubEntryControl();
        _currentCew.style.display = DisplayStyle.None;
        hierarchy.Add(_currentCew);

        _maximumCrew = new LifeSupportSubEntryControl();
        _maximumCrew.style.display = DisplayStyle.None;
        hierarchy.Add(_maximumCrew);

        var divider = new LifeSupportEntryDividerControl();
        hierarchy.Add(divider);
    }

    public void SetValues(LsEntryData data, bool isActiveVessel)
    {
        name = "ls-entry__" + data.VesselName;
        Data = data;

        var headerTitle = data.VesselName + " " + (_expanded ? "▼" : "▶");
        _header.SetValues(headerTitle, data.CurrentResourcesCountdowns, data.CurrentCrew, !_expanded);
        _header.TitleLabel.EnableInClassList("ls-vessel-sub-entry__title_active", isActiveVessel);

        var curCrewTitle = $"Current Crew ({data.CurrentCrew}):";
        _currentCew.SetValues(curCrewTitle, data.CurrentResourcesCountdowns, data.CurrentCrew, _expanded);
        _currentCew.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;

        var maxCrewTitle = $"Maximum Crew ({data.MaximumCrew}):";
        _maximumCrew.SetValues(maxCrewTitle, data.MaxResourcesCountdowns, data.MaximumCrew, _expanded);
        _maximumCrew.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetExpanded(bool newValue)
    {
        _expanded = newValue;
    }

    public struct LsEntryData
    {
        public IGGuid Id;
        public string VesselName;
        public int CurrentCrew;
        public int MaximumCrew;
        public Dictionary<string, double> CurrentResourcesCountdowns;
        public Dictionary<string, double> MaxResourcesCountdowns;
    }
}