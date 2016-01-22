using System;
using NUnit.Framework;
#if false
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
#endif

namespace Tests
{
	[TestFixture]
	public class UnrelatedTests
	{
		[Test]
		public void CanInvokeFuncFromObject()
		{
			int count = 0;
			Func<int> func = () => count++;
			Delegate d = func;
			var r = (int)d.Method.Invoke(func.Target, null);
			Assert.AreEqual(0, r);
			Assert.AreEqual(1, count);
		}

#if false
		[Test, Ignore("to move")]
		public async Task  HitHttpsEndpoint()
		{
			using (System.Http.WebRequestHandler handler = new System.Http.WebRequestHandler())
			{
				var certificate = GetCertificate();
				handler.ClientCertificates.Add(certificate);
				using (var client = new System.Http.HttpClient(handler))
				{
					var response = await client.GetAsync("https://github.com/");
					// TODO: use response, e.g.
					var responseText = await response.Content.ReadAsStringAsync();
				}
			}
		}

		private static X509Certificate2 GetCertificate()
		{
			var store = new X509Store(StoreLocation.CurrentUser);
			store.Open(OpenFlags.OpenExistingOnly);
			X509Certificate2 certificate = store.Certificates.Cast<X509Certificate2>().First();
			return certificate;
		}
#endif
	}
}