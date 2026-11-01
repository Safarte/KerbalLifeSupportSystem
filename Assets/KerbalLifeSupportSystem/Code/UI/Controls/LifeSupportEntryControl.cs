// ReSharper disable CheckNamespace

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.UI.Controls
{
    [UxmlElement]
    public partial class LifeSupportEntryControl : VisualElement
    {
        // USS Class names
        private const string ClassName = "lsEntry";
        private const string PinnedClassName = ClassName + "__pinned";
        private const string ActiveClassName = ClassName + "__active";
        private const string DividerClassName = "lsHeader__divider";

        private string _vesselName = "Test";
        private int _resourceCount = 3;

        [UxmlAttribute]
        public string VesselName
        {
            get => _vesselName;
            set
            {
                _vesselName = value;
                _nameCell.Name = _vesselName;
            }
        }

        [UxmlAttribute]
        public int ResourceCount
        {
            get => _resourceCount;
            set
            {
                _resourceCount = value;
                var resourceRemainingTimes = new List<double>();
                for (var i = 0; i < _resourceCount; i++)
                    resourceRemainingTimes.Add(50);
                SetResourceCountdowns(resourceRemainingTimes);
            }
        }

        // Internal elements
        private readonly EntryNameCell _nameCell;
        private readonly List<EntryCell> _cells = new();

        // Is vessel active
        public bool IsActive;

        // List of currently displayed remaining times
        public List<double> Times;

        public LifeSupportEntryControl()
        {
            AddToClassList(ClassName);

            _nameCell = new EntryNameCell();
            _nameCell.TogglePin += () => EnableInClassList(PinnedClassName, _nameCell.Pinned);
            hierarchy.Add(_nameCell);
        }

        public LifeSupportEntryControl(LsEntryData data) : this()
        {
            SetData(data);
        }

        public bool IsPinned => _nameCell.Pinned;
        public string Name => _nameCell.Name;
        public int CurrentCrew => _nameCell.KerbalCounter.CurrentCrew;

        private void SetResourceCountdowns(List<double> resourceRemainingTimes)
        {
            if (resourceRemainingTimes.Count != _cells.Count)
            {
                _cells.Clear();
                hierarchy.Clear();
                hierarchy.Add(_nameCell);

                foreach (var t in resourceRemainingTimes)
                {
                    // Vertical divider
                    var divider = new Label { name = "cell-divider", text = "|" };
                    divider.AddToClassList(DividerClassName);
                    hierarchy.Add(divider);

                    var cell = new EntryCell(t);
                    _cells.Add(cell);
                    hierarchy.Add(cell);
                }
            }
            else
            {
                for (var i = 0; i < resourceRemainingTimes.Count; i++) _cells[i].SetTime(resourceRemainingTimes[i]);
            }

            Times = resourceRemainingTimes;
        }

        public void SetData(LsEntryData data)
        {
            _nameCell.Name = data.VesselName;

            EnableInClassList(ActiveClassName, data.IsActive);
            IsActive = data.IsActive;

            _nameCell.KerbalCounter.CurrentCrew = data.CurrentCrew;
            _nameCell.KerbalCounter.MaximumCrew = data.MaximumCrew;

            SetResourceCountdowns(_nameCell.KerbalCounter.IsMaxCrewSelected
                ? data.MaxCrewRemainingTimes
                : data.CurCrewRemainingTimes);
        }

        public struct LsEntryData
        {
            public string VesselName;
            public bool IsActive;
            public int CurrentCrew;
            public int MaximumCrew;
            public List<double> CurCrewRemainingTimes;
            public List<double> MaxCrewRemainingTimes;
        }
    }
}