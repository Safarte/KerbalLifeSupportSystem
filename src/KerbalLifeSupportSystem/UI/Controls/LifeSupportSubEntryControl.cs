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

        // Initialize title label
        TitleLabel = new Label
        {
            name = "title",
            text = "-"
        };
        TitleLabel.AddToClassList(TitleClassName);
        TitleLabel.AddToClassList(GrayClassName);
        hierarchy.Add(TitleLabel);

        // Container for the resource countdowns
        var contentContainer1 = new VisualElement();
        contentContainer1.AddToClassList(ContentClassName);
        hierarchy.Add(contentContainer1);

        {
            var iter = 1;
            // Add a resource countdown for each LS supply resource
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

                // Add a divider between the resource countdowns
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
        set => TitleLabel.text = value;
    }

    public void SetValues(string title, Dictionary<string, double> resourceCountdowns, int crew, bool displayTimes)
    {
        Title = title;
        foreach (var (resource, value) in resourceCountdowns)
            _resourceLabels[resource].SetValue(value, crew, displayTimes);
    }

    /// <summary>
    ///     Label for a time countdown
    /// </summary>
    private class RemainingTimeLabel : Label
    {
        private const long SecondsInMinute = 60;
        private const long MinutesInHour = 60;
        private const long HoursInDay = 6;
        private const long DaysInYear = 426;

        /// <summary>
        ///     Converts a time in seconds to a KSP formatted datetime string
        /// </summary>
        /// <param name="time">Duration in seconds</param>
        /// <returns>Formatted KSP datetime string</returns>
        private static string ToDateTime(double time)
        {
            var num = (long)Math.Truncate(time);

            // Seconds
            var seconds = (int)(num % SecondsInMinute);
            num -= seconds;
            num /= SecondsInMinute;

            // Minutes
            var minutes = (int)(num % MinutesInHour);
            num -= minutes;
            num /= MinutesInHour;

            // Hours
            var hours = (int)(num % HoursInDay);
            num -= hours;
            num /= HoursInDay;

            // Days
            var days = (int)(num % DaysInYear);
            num -= days;
            num /= DaysInYear;

            // Years
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

        /// <summary>
        ///     Update the countdown label value
        /// </summary>
        /// <param name="time">Remaining time</param>
        /// <param name="crew">Crew amount</param>
        /// <param name="displayTimes">Should the countdown time be displayed</param>
        public void SetValue(double time, int crew, bool displayTimes)
        {
            if (!displayTimes)
            {
                text = "";
                return;
            }

            // Set the label to infinity if crew is 0 or time is too large
            text = crew == 0 || time > 1e11 ? "∞" : ToDateTime(time);

            // Add a warning symbol and set text to orange if less than 30 minutes remaining
            if (time < 30 * SecondsInMinute)
            {
                SetOrange();
                text += " /!\\";

                // Set text to red if less than 10 seconds remaining
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