using System.Collections.Generic;

namespace NobleTitles
{
	public class LogBase
	{
		public virtual void Print(string text) { }
		public virtual void Print(List<string> lines) { }
	}
}
