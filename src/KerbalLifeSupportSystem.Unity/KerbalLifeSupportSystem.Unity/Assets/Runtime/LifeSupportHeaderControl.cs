// ReSharper disable CheckNamespace

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.Unity.Runtime
{
    public class LifeSupportHeaderControl : VisualElement
    {
        // USS Class names
        private const string ClassName = "lsHeader";
        private const string CellDividerClassName = ClassName + "__divider";
        private const string NameCellClassName = ClassName + "NameCell";

        // Internal elements
        public readonly HeaderCell NameCell;
        public readonly List<HeaderCell> HeaderCells = new();

        public LifeSupportHeaderControl()
        {
            AddToClassList(ClassName);

            // First column: Name cell
            NameCell = new HeaderCell("Name", HeaderCell.SortType.Decreasing);
            NameCell.AddToClassList(NameCellClassName);
            NameCell.AddManipulator(new Clickable(() => UpdateCellSort(-1)));
            hierarchy.Add(NameCell);
        }

        public void SetResources(List<string> resourceNames)
        {
            HeaderCells.Clear();
            hierarchy.Clear();

            // Name cell
            hierarchy.Add(NameCell);

            // Subsequent columns: resource cells
            for (var i = 0; i < resourceNames.Count; i++)
            {
                var resourceName = resourceNames[i];

                // Vertical divider
                var divider = new Label { name = "cell-divider", text = "|" };
                divider.AddToClassList(CellDividerClassName);
                hierarchy.Add(divider);

                // Resource name cell
                var cell = new HeaderCell(resourceName);
                var i1 = i;
                cell.AddManipulator(new Clickable(() => UpdateCellSort(i1)));
                HeaderCells.Add(cell);
                hierarchy.Add(cell);
            }
        }

        private void UpdateCellSort(int cellIndex)
        {
            // Update Name Cell sort direction
            if (cellIndex == -1) NameCell.CycleSortDirection();
            else NameCell.SortDirection = HeaderCell.SortType.None;
            NameCell.UpdateSortDirectionIndicator();

            // Update resources sort direction
            for (var i = 0; i < HeaderCells.Count; i++)
            {
                if (i == cellIndex)
                    HeaderCells[i].CycleSortDirection();
                else
                    HeaderCells[i].SortDirection = HeaderCell.SortType.None;

                HeaderCells[i].UpdateSortDirectionIndicator();
            }
        }

        public new class UxmlFactory : UxmlFactory<LifeSupportHeaderControl, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlIntAttributeDescription _resourcesCount = new()
                { name = "ResourceCount", defaultValue = 3 };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);

                if (ve is LifeSupportHeaderControl control)
                {
                    var resourceNames = new List<string>();
                    for (var i = 0; i < _resourcesCount.GetValueFromBag(bag, cc); i++)
                        resourceNames.Add($"Res-{i}");
                    control.SetResources(resourceNames);
                }
            }
        }
    }
}