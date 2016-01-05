#if false
using System.Linq;
using NUnit.Framework;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Tests
{
	[TestFixture]
	public class UnrelatedTests
	{
		[Test, Ignore("to move")]
		public async Task  HitHttpsEndpoint()
		{
			using (WebRequestHandler handler = new WebRequestHandler())
			{
				var certificate = GetCertificate();
				handler.ClientCertificates.Add(certificate);
				using (HttpClient client = new HttpClient(handler))
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
	}
}
#endif