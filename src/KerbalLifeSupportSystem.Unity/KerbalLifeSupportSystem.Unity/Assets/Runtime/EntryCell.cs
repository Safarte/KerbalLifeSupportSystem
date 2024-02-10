// ReSharper disable CheckNamespace

using System;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.Unity.Runtime
{
    public class EntryCell : VisualElement
    {
        private const string ClassName = "lsEntryCell";
        private const string TextClassName = ClassName + "__text";

        private readonly RemainingTimeLabel _label;

        private EntryCell()
        {
            AddToClassList(ClassName);
            _label = new RemainingTimeLabel { text = "-" };
            _label.AddToClassList(TextClassName);
            hierarchy.Add(_label);
        }

        public EntryCell(double time) : this()
        {
            SetTime(time);
        }

        public void SetTime(double time)
        {
            _label.SetValue(time);
        }

        /// <summary>
        ///     Label for a time countdown
        /// </summary>
        private class RemainingTimeLabel : Label
        {
            private const string GrayClassName = "ls-monitor-gray";
            private const string OrangeClassName = "ls-monitor-orange";
            private const string RedClassName = "ls-monitor-red";

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

                var totalSeconds = (long)Math.Truncate(time);

                var res = totalSeconds switch
                {
                    < MinutesInHour * SecondsInMinute => $"{minutes:d2}m{seconds:d2}s",
                    < HoursInDay * MinutesInHour * SecondsInMinute => $" {hours}h{minutes:d2}m",
                    < 100 * HoursInDay * MinutesInHour * SecondsInMinute => $"{days}d {hours}h",
                    < DaysInYear * HoursInDay * MinutesInHour * SecondsInMinute => $"{days}d {hours}h",
                    _ => $"{years}y {days}d"
                };

                return res;
            }

            private void SetGray()
            {
                EnableInClassList(GrayClassName, true);
                EnableInClassList(OrangeClassName, false);
                EnableInClassList(RedClassName, false);
            }

            private void SetOrange()
            {
                EnableInClassList(GrayClassName, false);
                EnableInClassList(OrangeClassName, true);
                EnableInClassList(RedClassName, false);
            }

            private void SetRed()
            {
                EnableInClassList(GrayClassName, false);
                EnableInClassList(OrangeClassName, false);
                EnableInClassList(RedClassName, true);
            }

            /// <summary>
            ///     Update the countdown label value
            /// </summary>
            /// <param name="time">Remaining time</param>
            public void SetValue(double time)
            {
                // Set the label to infinity if time is above 999 years
                text = time > 9192398400 ? "∞" : ToDateTime(time);

                // Add a warning symbol and set text to orange if less than an hour remaining
                if (time < MinutesInHour * SecondsInMinute)
                {
                    text = "! " + text;
                    SetOrange();

                    // Set text to red if less than 1 minute remaining
                    if (time < SecondsInMinute)
                        SetRed();
                }
                else
                    SetGray();
            }
        }
    }
}