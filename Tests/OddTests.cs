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

		private int zero = 0;

		[Test, Explicit]
		public void MeasureMoreDelegatePerformances()
		{
			int n = 50000000;
			Action<int>[] d = {M};
			Action<int> a = M;
			M(1);
			d[zero](1);
			a(1);
			Action<int> e = i =>
			{
				for (int l = 0; l < d.Length; ++l)
					d[l](i);
			};
			var stopwatch = Stopwatch.StartNew();
			for (int i = 0; i < n; ++i)
			{
				e(i);
			}
			Console.WriteLine(stopwatch.Elapsed.ToEnglishString());
			stopwatch = Stopwatch.StartNew();
			for (int i = 0; i < n; ++i)
			{
				a(i);
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