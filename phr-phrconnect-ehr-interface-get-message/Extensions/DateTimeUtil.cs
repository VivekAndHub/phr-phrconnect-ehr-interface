using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.Extensions
{
     static class DateTimeUtil
    {
        /// <summary>
        /// Add a DateTime and a TimeSpan.
        /// The maximum time is DateTime.MaxTime.  It is not an error if time + timespan > MaxTime.
        /// Just return MaxTime.
        /// </summary>
        /// <param name="time">Initial <see cref="DateTime"/> value.</param>
        /// <param name="timespan"><see cref="TimeSpan"/> to add.</param>
        /// <returns></returns>
        public static DateTime Add(this DateTime datetime,  TimeSpan timespan)
        {
            if (timespan >= TimeSpan.Zero && DateTime.MaxValue - datetime <= timespan)
            {
                return GetMaxValue(datetime.Kind);
            }

            if (timespan <= TimeSpan.Zero && DateTime.MinValue - datetime >= timespan)
            {
                return GetMinValue(datetime.Kind);
            }

            return datetime + timespan;
        }

        public static DateTime GetMaxValue(DateTimeKind datetimeKind)
        {
            
            return new DateTime(DateTime.MaxValue.Ticks, datetimeKind);
        }

        public static DateTime GetMinValue(DateTimeKind datetimeKind)
        {
            return new DateTime(DateTime.MinValue.Ticks, datetimeKind);
        }

        public static DateTime? ToUniversalTime(this DateTime? datetime)
        {
            if (null == datetime || datetime.Value.Kind == DateTimeKind.Utc)
            {
                return datetime;
            }
            return ToUniversalTime(datetime.Value);
        }

        public static DateTime ToUniversalTime(this DateTime datetime)
        {

            if (datetime.Kind == DateTimeKind.Utc)
            {
                return datetime;
            }

            return datetime.ToUniversalTime();
        }
    }
}
