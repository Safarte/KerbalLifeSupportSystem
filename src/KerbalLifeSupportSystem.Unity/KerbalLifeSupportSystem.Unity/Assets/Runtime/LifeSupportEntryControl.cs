// ReSharper disable CheckNamespace

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.Unity.Runtime
{
    public class LifeSupportEntryControl : VisualElement
    {
        // USS Class names
        private const string ClassName = "lsEntry";
        private const string PinnedClassName = ClassName + "__pinned";
        private const string ActiveClassName = ClassName + "__active";
        private const string DividerClassName = "lsHeader__divider";

        // Internal elements
        private readonly EntryNameCell _nameCell;
        private readonly List<EntryCell> _cells = new();

        // Is vessel active
        public bool IsActive;

        // List of currently displayed remaining times
        public List<double> Times;

        // Event sent when filtering or sorting needs to happen
        public event Action NeedsSorting;

        public LifeSupportEntryControl()
        {
            AddToClassList(ClassName);

            _nameCell = new EntryNameCell();
            _nameCell.TogglePin += OnTogglePin;
            hierarchy.Add(_nameCell);
        }

        public LifeSupportEntryControl(LsEntryData data) : this()
        {
            SetData(data);
        }

        public bool IsPinned => _nameCell.Pinned;
        public string Name => _nameCell.Name;
        public int CurrentCrew => _nameCell.KerbalCounter.CurrentCrew;

        private void OnTogglePin()
        {
            EnableInClassList(PinnedClassName, _nameCell.Pinned);
            NeedsSorting?.Invoke();
        }

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
            if (Name != data.VesselName || IsActive != data.IsActive)
                NeedsSorting?.Invoke();

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

        public new class UxmlFactory : UxmlFactory<LifeSupportEntryControl, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription _name = new()
                { name = "VesselName", defaultValue = "FlySafe" };

            private readonly UxmlIntAttributeDescription _resourcesCount = new()
                { name = "ResourceCount", defaultValue = 3 };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);

                if (ve is LifeSupportEntryControl control)
                {
                    control._nameCell.Name = _name.GetValueFromBag(bag, cc);

                    var resourceRemainingTimes = new List<double>();
                    for (var i = 0; i < _resourcesCount.GetValueFromBag(bag, cc); i++)
                        resourceRemainingTimes.Add(50);
                    control.SetResourceCountdowns(resourceRemainingTimes);
                }
            }
        }
    }
}