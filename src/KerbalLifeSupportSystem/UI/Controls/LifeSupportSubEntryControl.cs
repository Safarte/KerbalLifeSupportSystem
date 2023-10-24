using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.UI;

public class LifeSupportSubEntryControl : VisualElement
{
    private const string UssClassName = "ls-vessel-sub-entry";

    private const string UssTitleClassName = UssClassName + "__title";
    private const string UssContentClassName = UssClassName + "__content";
    private const string UssValueClassName = UssClassName + "__value";
    private const string UssDividerClassName = UssClassName + "__divider";

    private const string UssGrayClassName = "ls-monitor-gray";
    private const string UssWhiteClassName = "ls-monitor-white";
    private const string UssOrangeClassName = "ls-monitor-orange";
    private const string UssRedClassName = "ls-monitor-red";

    private readonly RemainingTimeLabel _foodLabel;
    private readonly RemainingTimeLabel _oxygenLabel;
    private readonly RemainingTimeLabel _waterLabel;

    public readonly Label TitleLabel;

    public LifeSupportSubEntryControl(string title, double food, double water, double oxygen, int crew,
        bool displayTimes) : this()
    {
        SetValues(title, food, water, oxygen, crew, displayTimes);
    }

    public LifeSupportSubEntryControl()
    {
        AddToClassList(UssClassName);

        TitleLabel = new Label
        {
            name = "title",
            text = "-"
        };
        TitleLabel.AddToClassList(UssTitleClassName);
        TitleLabel.AddToClassList(UssGrayClassName);
        hierarchy.Add(TitleLabel);

        var contentContainer1 = new VisualElement();
        contentContainer1.AddToClassList(UssContentClassName);
        hierarchy.Add(contentContainer1);

        {
            _foodLabel = new RemainingTimeLabel
            {
                name = "food",
                text = "-"
            };
            _foodLabel.AddToClassList(UssValueClassName);
            _foodLabel.AddToClassList(UssWhiteClassName);
            contentContainer1.Add(_foodLabel);

            var firstDividerLabel = new Label
            {
                name = "divider-0",
                text = "|"
            };
            firstDividerLabel.AddToClassList(UssDividerClassName);
            firstDividerLabel.AddToClassList(UssGrayClassName);
            contentContainer1.Add(firstDividerLabel);

            _waterLabel = new RemainingTimeLabel
            {
                name = "water",
                text = "-"
            };
            _waterLabel.AddToClassList(UssValueClassName);
            _waterLabel.AddToClassList(UssWhiteClassName);
            contentContainer1.Add(_waterLabel);

            var secondDividerLabel = new Label
            {
                name = "divider-1",
                text = "|"
            };
            secondDividerLabel.AddToClassList(UssDividerClassName);
            secondDividerLabel.AddToClassList(UssGrayClassName);
            contentContainer1.Add(secondDividerLabel);

            _oxygenLabel = new RemainingTimeLabel
            {
                name = "oxygen",
                text = "-"
            };
            _oxygenLabel.AddToClassList(UssValueClassName);
            _oxygenLabel.AddToClassList(UssWhiteClassName);
            contentContainer1.Add(_oxygenLabel);
        }
    }

    public string Title
    {
        get => TitleLabel.text;
        set => TitleLabel.text = value;
    }

    public void SetValues(string title, double food, double water, double oxygen, int crew, bool displayTimes)
    {
        Title = title;
        _foodLabel.SetValue(food, crew, displayTimes);
        _waterLabel.SetValue(water, crew, displayTimes);
        _oxygenLabel.SetValue(oxygen, crew, displayTimes);
    }

    public class RemainingTimeLabel : Label
    {
        private static string ToDateTime(double time)
        {
            var num = (long)Math.Truncate(time);
            var seconds = (int)(num % 60L);
            num -= seconds;
            num /= 60L;
            var minutes = (int)(num % 60L);
            num -= minutes;
            num /= 60L;
            var hours = (int)(num % 6L);
            num -= hours;
            num /= 6L;
            var days = (int)(num % 426L);
            num -= days;
            num /= 426L;
            var years = (int)num;

            var res = $"{minutes:d2}m{seconds:d2}s";

            if (hours > 0)
                res = hours + "h" + res;

            if (days > 0)
                res = $"{days}d " + res;

            if (years > 0)
                res = $"{years}y " + res;

            return res;
        }

        private void SetWhite()
        {
            EnableInClassList(UssOrangeClassName, false);
            EnableInClassList(UssWhiteClassName, true);
            EnableInClassList(UssRedClassName, false);
        }

        private void SetOrange()
        {
            EnableInClassList(UssOrangeClassName, true);
            EnableInClassList(UssWhiteClassName, false);
            EnableInClassList(UssRedClassName, false);
        }

        private void SetRed()
        {
            EnableInClassList(UssOrangeClassName, false);
            EnableInClassList(UssWhiteClassName, false);
            EnableInClassList(UssRedClassName, true);
        }

        public void SetValue(double time, int crew, bool displayTimes)
        {
            if (!displayTimes)
            {
                text = "";
                return;
            }

            text = crew == 0 || time > 1e6 ? "∞" : ToDateTime(time);

            if (time < 1800)
            {
                SetOrange();
                text += " /!\\";

                if (time < 10)
                    SetRed();
            }
            else
            {
                SetWhite();
            }
        }
    }
}