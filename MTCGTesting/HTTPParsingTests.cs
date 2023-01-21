using MonsterTradingCardGame.Models;

namespace MonsterTradingCardGame.MTCGTesting.HTTPTests
{
    public class HTTPParsingTests

    {
        private HTTPRequest httpRequest { get; set; } = null!;

        [SetUp]
        public void Setup()
        {
            string request = "GET /score HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Authorization: Bearer test-token\n"
                + "Content-Type: application-json\n"
                + "Content-Length: 46\n"
                + "{\"Username\":\"test\", \"Password\":\"user\"}";

            httpRequest = new HTTPRequest(request);
        }

        [Test]
        public void TestHTTPMethod()
        {
            Assert.That(httpRequest.Method, Is.EqualTo("GET"));
        }
        [Test]
        public void TestHTTPPath()
        {
            Assert.That(httpRequest.Url, Is.EqualTo("/score"));
        }
        [Test]
        public void TestHTTPAuthorization()
        {
            Assert.That(httpRequest.Authorization, Is.EqualTo("Bearer test-token"));
        }
        [Test]
        public void TestHTTPBody()
        {
            Assert.That(httpRequest.Body, Is.EqualTo("{\"Username\":\"test\", \"Password\":\"user\"}"));
        }
        [Test]
        public void TestContentLength()
        {
            Assert.That(httpRequest.ContentLength, Is.EqualTo(46));
        }
        [Test]
        public void TestContentType()
        {
            Assert.That(httpRequest.ContentType, Is.EqualTo("application-json"));
        }
        [Test]
        public void TestQueryString()
        {
            string request = "GET /deck?format=plain HTTP/1.1\r\n" +
                "Host: localhost:10001\r\n" +
                "User-Agent: curl/7.87.0\r\n" +
                "Accept: */*\r\n" +
                "Authorization: Bearer kienboec-mtcgToken";

            httpRequest = new HTTPRequest(request);

            Assert.That(httpRequest.QueryString, Is.EqualTo("format=plain"));
        }
    }
}