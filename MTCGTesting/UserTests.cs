using MonsterTradingCardGame.Models;
using System.Data;
using Npgsql;
using Newtonsoft.Json;
using System.Net;

namespace MonsterTradingCardGame.MTCGTesting.UserTests
{
    public class UserTests
    {
        private HTTPRequest request { get; set; } = null!;
        private UserHandler userHandler { get; set; } = null!;
        private HTTPResponse response { get; set; } = null!;
        private IDbConnection connection { get; set; } = null!;

        [Test]
        public void ConvertJsonToUserTest()
        {
            string requestString = "POST /users HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Content-Type: application-json\n"
                + "Content-Length: 46\n"
                + "{\"Username\":\"test\", \"Password\":\"user\"}";
            request = new HTTPRequest(requestString);
            string userJson = request.Body;
            User user = JsonConvert.DeserializeObject<User>(userJson);

            Assert.That(user.password, Is.EqualTo("user"));
        }

        [Test]
        public void CreateUserTest()
        {
            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();
            UserHandler userHandler = new UserHandler(connection);
            string requestString = "POST /users HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Content-Type: application-json\n"
                + "Content-Length: 46\n"
                + "{\"Username\":\"test\", \"Password\":\"user\"}";
            request = new HTTPRequest(requestString);
            string userJson = request.Body;
            User user = JsonConvert.DeserializeObject<User>(userJson);

            userHandler.handleUser(request); //should create user

            //now check if user got created (only works if the user wasnt already created before the test)
            IDbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM users WHERE username = @username";
            NpgsqlCommand? c = command as NpgsqlCommand;

            IDbDataParameter username = c.CreateParameter();
            username.DbType = DbType.String;
            username.ParameterName = "username";
            c.Parameters.Add(username);
            c.Prepare();
            c.Parameters["username"].Value = user.username;

            bool created = false;
            using (IDataReader reader = c.ExecuteReader())
            {
                if (reader.Read())
                {
                    // A user with this username exists
                    created = true;
                }
            }

            Assert.IsTrue(created);
        }

        [Test]
        public void LoginUserTest()
        {
            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();
            UserHandler userHandler = new UserHandler(connection);
            string requestString = "POST /sessions HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Content-Type: application-json\n"
                + "Content-Length: 46\n"
                + "{\"Username\":\"test\", \"Password\":\"user\"}";
            request = new HTTPRequest(requestString);
            response = new HTTPResponse();

            response = userHandler.handleUser(request);

            Assert.That(response.ResponseContent, Is.EqualTo("test-mtcgToken"));
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();
            IDbCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM users WHERE username = 'test'";
            NpgsqlCommand c = command as NpgsqlCommand;
            int userid = (int)c.ExecuteNonQuery();
        }
    }
}
