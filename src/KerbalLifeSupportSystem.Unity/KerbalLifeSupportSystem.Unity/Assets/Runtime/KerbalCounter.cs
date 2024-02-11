// ReSharper disable CheckNamespace

using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.Unity.Runtime
{
    public class KerbalCounter : Button
    {
        // USS Class names
        private const string ClassName = "kerbalCounter";
        private const string IconClassName = ClassName + "__icon";
        private const string TextClassName = ClassName + "__text";
        private const string BoldTextClassName = TextClassName + "__bold";
        private const string CurrentTextClassName = TextClassName + "__current";
        private const string MaxTextClassName = TextClassName + "__max";
        private const string SlashClassName = ClassName + "__slash";

        // Crew count Labels
        private readonly Label _curCrewLabel;
        private readonly Label _maxCrewLabel;

        // Crew counts backing variables
        private int _curCrew;
        private int _maxCrew;

        public bool IsMaxCrewSelected;

        public int CurrentCrew
        {
            get => _curCrew;
            set
            {
                _curCrew = value;
                _curCrewLabel.text = $"{_curCrew:d3}";
            }
        }

        public int MaximumCrew
        {
            set
            {
                _maxCrew = value;
                _maxCrewLabel.text = $"{_maxCrew:d3}";
            }
        }

        public KerbalCounter()
        {
            AddToClassList(ClassName);

            // Kerbal icon
            var kerbalIcon = new VisualElement { name = "kerbal-counter-icon" };
            kerbalIcon.AddToClassList(IconClassName);
            hierarchy.Add(kerbalIcon);

            // Current crew counter
            _curCrewLabel = new Label { name = "kerbal-counter-current", text = "001" };
            _curCrewLabel.AddToClassList(TextClassName);
            _curCrewLabel.AddToClassList(CurrentTextClassName);
            hierarchy.Add(_curCrewLabel);

            // Separator slash
            var slash = new Label { text = "/" };
            slash.AddToClassList(SlashClassName);
            hierarchy.Add(slash);

            // Max crew counter
            _maxCrewLabel = new Label { name = "kerbal-counter-max", text = "888" };
            _maxCrewLabel.AddToClassList(TextClassName);
            _maxCrewLabel.AddToClassList(MaxTextClassName);
            hierarchy.Add(_maxCrewLabel);

            // Set current crew to selected
            _curCrewLabel.AddToClassList(BoldTextClassName);

            clicked += OnClick;
        }

        private void OnClick()
        {
            IsMaxCrewSelected = !IsMaxCrewSelected;
            _curCrewLabel.EnableInClassList(BoldTextClassName, !IsMaxCrewSelected);
            _maxCrewLabel.EnableInClassList(BoldTextClassName, IsMaxCrewSelected);
        }
    }
}