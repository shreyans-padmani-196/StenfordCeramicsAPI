using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stenford.Common.Utility
{
	public class StringUtility
	{
		//public static string ToRelativeTimeString(DateTime dateTime)
		//{	
		//	var span = DateTime.Now - dateTime;

		//	if (span.TotalDays < 1) return "Today";
		//	if (span.TotalDays < 2) return "Yesterday";
		//	if (span.TotalDays < 7) return $"{(int)span.TotalDays} days ago";
		//	if (span.TotalDays < 14) return "1 week ago";
		//	if (span.TotalDays < 30) return $"{(int)(span.TotalDays / 7)} weeks ago";
		//	if (span.TotalDays < 60) return "1 month ago";
		//	return $"{(int)(span.TotalDays / 30)} months ago";
		//}

		public static string ToRelativeTimeString(DateTime dateTime)
		{
			int daysSince = (DateTime.Now.Date - dateTime.Date).Days;

			if (daysSince == 0) return "Today";
			if (daysSince == 1) return "Yesterday";
			if (daysSince < 7) return $"{daysSince} days ago";
			if (daysSince < 14) return "1 week ago";
			if (daysSince < 30) return $"{daysSince / 7} weeks ago";
			if (daysSince < 60) return "1 month ago";
			return $"{daysSince / 30} months ago";
		}
	}
}
