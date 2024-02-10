// ReSharper disable CheckNamespace

using System;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.Unity.Runtime
{
    public class EntryNameCell : VisualElement
    {
        // USS Class Names
        private const string ClassName = "lsEntryNameCell";
        private const string TextClassName = ClassName + "__text";
        private const string PinButtonClassName = ClassName + "__pinButton";
        private const string PinButtonIconClassName = PinButtonClassName + "__icon";
        private const string PinButtonIconEnabledClassName = PinButtonIconClassName + "__enabled";

        // Internal elements
        private readonly Button _pinButton;
        private readonly VisualElement _pinButtonIcon;
        private readonly Label _nameLabel;
        public readonly KerbalCounter KerbalCounter;

        // Event called when pinned button is pressed
        public event Action TogglePin;

        // Is pin button enabled
        public bool Pinned;

        public EntryNameCell()
        {
            AddToClassList(ClassName);

            // Pin Button
            _pinButton = new Button { name = "name-cell-pin" };
            _pinButton.AddToClassList(PinButtonClassName);
            // Pin Button Icon
            _pinButtonIcon = new VisualElement { name = "name-cell-pin-icon" };
            _pinButtonIcon.AddToClassList(PinButtonIconClassName);
            _pinButton.hierarchy.Add(_pinButtonIcon);
            // Pin Button click action
            _pinButton.clicked += OnPinButton;
            hierarchy.Add(_pinButton);

            // Vessel Name
            _nameLabel = new Label { name = "name-cell-label", text = "FlySafe" };
            _nameLabel.AddToClassList(TextClassName);
            hierarchy.Add(_nameLabel);

            // Current / Max Crew Button
            KerbalCounter = new KerbalCounter();
            hierarchy.Add(KerbalCounter);
        }

        public string Name
        {
            get => _nameLabel.text;
            set => _nameLabel.text = value;
        }

        private void OnPinButton()
        {
            Pinned = !Pinned;
            _pinButtonIcon.EnableInClassList(PinButtonIconEnabledClassName, Pinned);
            TogglePin?.Invoke();
        }
    }
}