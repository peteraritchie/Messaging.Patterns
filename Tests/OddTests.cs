using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using PRI.ProductivityExtensions.TemporalExtensions;

namespace Tests
{
	[TestFixture]
	public class OddTests
	{
		/// <summary>
		/// Used to compare the performance of Bus.
		/// </summary>
		[Test, Explicit]
		public void MeasureDelegatePerformance()
		{

			int n = 50000000;
			Action<int> d = M;
			M(1);
			d(1);
			var stopwatch = Stopwatch.StartNew();
			for (int i = 0; i < n; ++i)
			{
				d(i);
			}
			Console.WriteLine(stopwatch.Elapsed.ToEnglishString());
			stopwatch = Stopwatch.StartNew();
			for (int i = 0; i < n; ++i)
			{
				M(i);
			}
			Console.WriteLine(stopwatch.Elapsed.ToEnglishString());
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void M(int x)
		{
		}
	}
}