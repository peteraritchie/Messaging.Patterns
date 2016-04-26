using System;
using NUnit.Framework;
using PRI.Messaging.Patterns;
using PRI.Messaging.Primitives;

namespace Tests
{
	[TestFixture]
	public class CreatingActionConsumers
	{
		[Test]
		public void NullDelegateCausesException()
		{
			Assert.Throws<ArgumentNullException>(() =>
			{
				var ac = new ActionConsumer<IMessage>(null);
			});
		}
	}
}