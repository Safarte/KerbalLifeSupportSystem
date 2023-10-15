using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.UI
{
    public class LifeSupportSubEntryControl : VisualElement
    {
        public static string UssClassName = "ls-vessel-sub-entry";

        public static string UssTitleClassName = UssClassName + "__title";
        public static string UssContentClassName = UssClassName + "__content";
        public static string UssValueClassName = UssClassName + "__value";
        public static string UssDividerClassName = UssClassName + "__divider";

        public static string UssGrayClassName = "ls-monitor-gray";
        public static string UssWhiteClassName = "ls-monitor-white";
        public static string UssOrangeClassName = "ls-monitor-orange";
        public static string UssRedClassName = "ls-monitor-red";

        public Label TitleLabel;
        public string Title { get => TitleLabel.text; set => TitleLabel.text = value; }

        public VisualElement ContentContainer;

        public RemainingTimeLabel FoodLabel;
        public RemainingTimeLabel WaterLabel;
        public RemainingTimeLabel OxygenLabel;

        public Label FirstDividerLabel;
        public Label SecondDividerLabel;

        public void SetValues(string title, double food, double water, double oxygen, bool displayTimes)
        {
            Title = title;
            FoodLabel.SetValue(food, displayTimes);
            WaterLabel.SetValue(water, displayTimes);
            OxygenLabel.SetValue(oxygen, displayTimes);
        }

        public LifeSupportSubEntryControl(string title, double food, double water, double oxygen, bool displayTimes) : this()
        {
            SetValues(title, food, water, oxygen, displayTimes);
        }

        public LifeSupportSubEntryControl()
        {
            AddToClassList(UssClassName);

            TitleLabel = new Label()
            {
                name = "title",
                text = "-"
            };
            TitleLabel.AddToClassList(UssTitleClassName);
            TitleLabel.AddToClassList(UssGrayClassName);
            hierarchy.Add(TitleLabel);

            ContentContainer = new VisualElement();
            ContentContainer.AddToClassList(UssContentClassName);
            hierarchy.Add(ContentContainer);

            {
                FoodLabel = new RemainingTimeLabel()
                {
                    name = "food",
                    text = "-"
                };
                FoodLabel.AddToClassList(UssValueClassName);
                FoodLabel.AddToClassList(UssWhiteClassName);
                ContentContainer.Add(FoodLabel);

                FirstDividerLabel = new Label()
                {
                    name = "divider-0",
                    text = "|"
                };
                FirstDividerLabel.AddToClassList(UssDividerClassName);
                FirstDividerLabel.AddToClassList(UssGrayClassName);
                ContentContainer.Add(FirstDividerLabel);

                WaterLabel = new RemainingTimeLabel()
                {
                    name = "water",
                    text = "-"
                };
                WaterLabel.AddToClassList(UssValueClassName);
                WaterLabel.AddToClassList(UssWhiteClassName);
                ContentContainer.Add(WaterLabel);

                SecondDividerLabel = new Label()
                {
                    name = "divider-1",
                    text = "|"
                };
                SecondDividerLabel.AddToClassList(UssDividerClassName);
                SecondDividerLabel.AddToClassList(UssGrayClassName);
                ContentContainer.Add(SecondDividerLabel);

                OxygenLabel = new RemainingTimeLabel()
                {
                    name = "oxygen",
                    text = "-"
                };
                OxygenLabel.AddToClassList(UssValueClassName);
                OxygenLabel.AddToClassList(UssWhiteClassName);
                ContentContainer.Add(OxygenLabel);
            }
        }

        public class RemainingTimeLabel : Label
        {
            private string ToDateTime(double time)
            {
                if (time > 1e6 || time < 0)
                    return "∞";

                long num = (long)Math.Truncate(time);
                int seconds = (int)(num % 60L);
                num -= seconds;
                num /= 60L;
                int minutes = (int)(num % 60L);
                num -= minutes;
                num /= 60L;
                int hours = (int)(num % 6L);
                num -= hours;
                num /= 6L;
                int days = (int)(num % 426L);
                num -= days;
                num /= 426L;
                int years = (int)num;

                string res = $"{minutes:d2}m{seconds:d2}s";

                if (hours > 0)
                    res = hours.ToString() + "h" + res;

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

            public void SetValue(double time, bool displayTimes)
            {
                if (!displayTimes)
                {
                    text = "";
                    return;
                }

                text = ToDateTime(time);

                if (time < 1800)
                {
                    SetOrange();
                    text += " /!\\";

                    if (time < 10)
                        SetRed();
                }
                else
                    SetWhite();
            }
        }
    }
}
