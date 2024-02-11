// ReSharper disable CheckNamespace

using System;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.Unity.Runtime
{
    public class HeaderCell : VisualElement
    {
        public enum SortType
        {
            Increasing,
            Decreasing,
            None
        }

        private const string ClassName = "lsHeaderCell";
        private const string SortIndicatorClassName = ClassName + "__sortIndicator";
        private const string TextClassName = ClassName + "__text";

        private readonly Label _sortDirectionIndicator;

        private readonly Label _text;
        public SortType SortDirection;

        private HeaderCell()
        {
            AddToClassList(ClassName);
            SortDirection = SortType.None;

            _text = new Label { name = "cell-text-header", text = "HEADER" };
            _text.AddToClassList(TextClassName);
            hierarchy.Add(_text);

            _sortDirectionIndicator = new Label { text = " ", name = "cell-sort-header" };
            _sortDirectionIndicator.AddToClassList(SortIndicatorClassName);
            hierarchy.Add(_sortDirectionIndicator);
        }

        public HeaderCell(string text, SortType sortDirection = SortType.None) : this()
        {
            SortDirection = sortDirection;
            UpdateSortDirectionIndicator();
            _text.name = "cell-text-" + text;
            Text = text;
        }

        public string Text
        {
            set => _text.text = value;
        }

        public void UpdateSortDirectionIndicator()
        {
            _sortDirectionIndicator.text = SortDirection switch
            {
                SortType.Increasing => "\u2191",
                SortType.Decreasing => "\u2193",
                SortType.None => "=",
                _ => throw new Exception("Invalid starting sort direction, should not happen")
            };
        }

        public void CycleSortDirection()
        {
            SortDirection = SortDirection switch
            {
                SortType.Increasing => SortType.Decreasing,
                SortType.Decreasing => SortType.Increasing,
                SortType.None => SortType.Decreasing,
                _ => throw new Exception("Invalid starting sort direction, should not happen")
            };
        }
    }
}