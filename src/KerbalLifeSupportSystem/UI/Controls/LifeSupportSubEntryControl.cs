using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.UI;

public class LifeSupportSubEntryControl : VisualElement
{
    private const string ClassName = "ls-vessel-sub-entry";

    private const string TitleClassName = ClassName + "__title";
    private const string ContentClassName = ClassName + "__content";
    private const string ValueClassName = ClassName + "__value";
    private const string DividerClassName = ClassName + "__divider";

    private const string GrayClassName = "ls-monitor-gray";
    private const string WhiteClassName = "ls-monitor-white";
    private const string OrangeClassName = "ls-monitor-orange";
    private const string RedClassName = "ls-monitor-red";

    private readonly Dictionary<string, RemainingTimeLabel> _resourceLabels = new();

    public readonly Label TitleLabel;

    public LifeSupportSubEntryControl(string title, Dictionary<string, double> resourceCountdowns, int crew,
        bool displayTimes) : this()
    {
        SetValues(title, resourceCountdowns, crew, displayTimes);
    }

    public LifeSupportSubEntryControl()
    {
        AddToClassList(ClassName);

        TitleLabel = new Label
        {
            name = "title",
            text = "-"
        };
        TitleLabel.AddToClassList(TitleClassName);
        TitleLabel.AddToClassList(GrayClassName);
        hierarchy.Add(TitleLabel);

        var contentContainer1 = new VisualElement();
        contentContainer1.AddToClassList(ContentClassName);
        hierarchy.Add(contentContainer1);

        {
            var iter = 1;
            foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
            {
                _resourceLabels[resource] = new RemainingTimeLabel
                {
                    name = resource,
                    text = "-"
                };
                _resourceLabels[resource].AddToClassList(ValueClassName);
                _resourceLabels[resource].AddToClassList(WhiteClassName);
                contentContainer1.Add(_resourceLabels[resource]);

                if (iter >= KerbalLifeSupportSystemPlugin.Instance.LsInputResources.Length) continue;
                var dividerLabel = new Label
                {
                    name = "divider-0",
                    text = "|"
                };
                dividerLabel.AddToClassList(DividerClassName);
                dividerLabel.AddToClassList(GrayClassName);
                contentContainer1.Add(dividerLabel);

                ++iter;
            }
        }
    }

    public string Title
    {
        get => TitleLabel.text;
        set => TitleLabel.text = value;
    }

    public void SetValues(string title, Dictionary<string, double> resourceCountdowns, int crew, bool displayTimes)
    {
        Title = title;
        foreach (var (resource, value) in resourceCountdowns)
            _resourceLabels[resource].SetValue(value, crew, displayTimes);
    }

    private class RemainingTimeLabel : Label
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
            EnableInClassList(OrangeClassName, false);
            EnableInClassList(WhiteClassName, true);
            EnableInClassList(RedClassName, false);
        }

        private void SetOrange()
        {
            EnableInClassList(OrangeClassName, true);
            EnableInClassList(WhiteClassName, false);
            EnableInClassList(RedClassName, false);
        }

        private void SetRed()
        {
            EnableInClassList(OrangeClassName, false);
            EnableInClassList(WhiteClassName, false);
            EnableInClassList(RedClassName, true);
        }

        public void SetValue(double time, int crew, bool displayTimes)
        {
            if (!displayTimes)
            {
                text = "";
                return;
            }

            text = crew == 0 || time > 1e11 ? "∞" : ToDateTime(time);

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