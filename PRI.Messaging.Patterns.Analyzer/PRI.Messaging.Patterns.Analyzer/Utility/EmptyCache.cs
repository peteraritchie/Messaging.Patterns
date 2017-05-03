using System;

namespace PRI.Messaging.Patterns.Analyzer.Utility
{
	public static class EmptyCache<T>
	{
		private static readonly Lazy<T[]> _lazyArray = new Lazy<T[]>(() => new T[0]);
		public static T[] EmptyArray => _lazyArray.Value;
	}
}