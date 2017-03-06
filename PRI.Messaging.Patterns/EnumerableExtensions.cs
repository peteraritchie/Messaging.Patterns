using System;
using System.Collections.Generic;
using System.Linq;

namespace PRI.Messaging.Patterns
{
	public static class EnumerableExtensions
	{
		// TODO: Move to productivity extensions
		public static Action<T> Sum<T>(this IEnumerable<Action<T>> coll)
		{
			Action<T> result = coll.ElementAt(0);
			foreach (var d in coll.Skip(1))
				result += d;
			return result;
		}
		public static Func<T1, T2> Sum<T1, T2>(this IEnumerable<Func<T1, T2>> coll)
		{
			Func<T1, T2> result = coll.ElementAt(0);
			foreach (var d in coll.Skip(1))
				result += d;
			return result;
		}
	}
}