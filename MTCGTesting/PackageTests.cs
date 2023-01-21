using MonsterTradingCardGame;
using MonsterTradingCardGame.Models;
using Npgsql;
using NUnit.Framework;
using System.Data;

namespace MonsterTradingCardGame.MTCGTesting.PackageTests
{
    public class PackageTests
    {
        [OneTimeSetUp] 
        public void SetUp() 
        {
            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();
            string requestString = "POST /users HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Content-Type: application-json\n"
                + "Content-Length: 46\n"
                + "{\"Username\":\"package\", \"Password\":\"tester\"}";
            HTTPRequest request = new HTTPRequest(requestString);
            UserHandler userHandler = new UserHandler(connection);
            userHandler.handleUser(request);
        }

        [Test, Order(0)]
        public void TestPackageCreation() //creates 4 packages
        {
            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();
            PackageHandler packageHandler = new PackageHandler(connection);
            HTTPResponse response = new HTTPResponse();

            string requestString = "POST /packages HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Content-Type: application/json\n"
                + "Authorization: Bearer admin-token\n"
                + "Content-Length: 418\n"
            + "[{\"Id\":\"1\", \"Name\":\"WaterGoblin\", \"Damage\": 10.0}, " +
              " {\"Id\":\"2\", \"Name\":\"RegularSpell\", \"Damage\": 50.0}, " +
              " {\"Id\":\"3\", \"Name\":\"Knight\", \"Damage\": 20.0}, " +
              " {\"Id\":\"4\", \"Name\":\"RegularSpell\", \"Damage\": 45.0}, " +
              " {\"Id\":\"5\", \"Name\":\"FireElf\", \"Damage\": 25.0}]";
            HTTPRequest request = new HTTPRequest(requestString);
            response = packageHandler.handlePackage(request);

            requestString = "POST /packages HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Content-Type: application/json\n"
                + "Authorization: Bearer admin-token\n"
                + "Content-Length: 412\n"
            + "[{\"Id\":\"6\", \"Name\":\"WaterGoblin\", \"Damage\": 10.0}, " +
               "{\"Id\":\"7\", \"Name\":\"Dragon\", \"Damage\": 50.0}, " +
               "{\"Id\":\"8\", \"Name\":\"WaterSpell\", \"Damage\": 20.0}, " +
               "{\"Id\":\"9\", \"Name\":\"Ork\", \"Damage\": 45.0}, " +
               "{\"Id\":\"10\", \"Name\":\"FireSpell\",    \"Damage\": 25.0}]";
            request = new HTTPRequest(requestString);
            response = packageHandler.handlePackage(request);

            requestString = "POST /packages HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Content-Type: application/json\n"
                + "Authorization: Bearer admin-token\n"
                + "Content-Length: 412\n"
            + "[{\"Id\":\"11\", \"Name\":\"WaterGoblin\", \"Damage\":  9.0}, " +
              "{\"Id\":\"12\", \"Name\":\"Dragon\", \"Damage\": 55.0}, " +
              "{\"Id\":\"13\", \"Name\":\"WaterSpell\", \"Damage\": 21.0}, " +
              "{\"Id\":\"14\", \"Name\":\"Ork\", \"Damage\": 55.0}, " +
              "{\"Id\":\"15\", \"Name\":\"WaterSpell\",   \"Damage\": 23.0}]";
            request = new HTTPRequest(requestString);
            response = packageHandler.handlePackage(request);

            requestString = "POST /packages HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Content-Type: application/json\n"
                + "Authorization: Bearer admin-token\n"
                + "Content-Length: 412\n"
            + "[{\"Id\":\"16\", \"Name\":\"WaterGoblin\", \"Damage\": 11.0}, " +
              "{\"Id\":\"17\", \"Name\":\"Dragon\", \"Damage\": 70.0}, " +
              "{\"Id\":\"18\", \"Name\":\"WaterSpell\", \"Damage\": 22.0}, " +
              "{\"Id\":\"19\", \"Name\":\"Ork\", \"Damage\": 40.0}, " +
              "{\"Id\":\"20\", \"Name\":\"RegularSpell\", \"Damage\": 28.0}]";
            request = new HTTPRequest(requestString);
            response = packageHandler.handlePackage(request);

            Assert.That(response.Message, Is.EqualTo("Package and cards successfully created"));
        }

        [Test, Order(1)]
        public void TestPackageCreationError()
        {
            string requestString = "POST /packages HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Content-Type: application/json\n"
                + "Authorization: Bearer admin-token\n"
                + "Content-Length: 418\n"
            + "[{\"Id\":\"1\", \"Name\":\"WaterGoblin\", \"Damage\": 10.0}, " +
              " {\"Id\":\"2\", \"Name\":\"RegularSpell\", \"Damage\": 50.0}, " +
              " {\"Id\":\"3\", \"Name\":\"Knight\", \"Damage\": 20.0}, " +
              " {\"Id\":\"4\", \"Name\":\"RegularSpell\", \"Damage\": 45.0}, " +
              " {\"Id\":\"5\", \"Name\":\"FireElf\", \"Damage\": 25.0}]";
            HTTPRequest request = new HTTPRequest(requestString);

            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();
            PackageHandler packageHandler = new PackageHandler(connection);
            HTTPResponse response = new HTTPResponse();

            response = packageHandler.handlePackage(request);

            Assert.That(response.Message, Is.EqualTo("At least one card in the packages already exists"));
        }

        [Test, Order(2)]
        public void TestBuyPackage() //only works if the user has 20 money
        {
            string requestString = "POST /transactions/packages HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Content-Type: application/json\n"
                + "Authorization: Bearer package-token\n"
                + "Content-Length: 0\n";
            HTTPRequest request = new HTTPRequest(requestString);

            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();
            PackageHandler packageHandler = new PackageHandler(connection);
            HTTPResponse response = new HTTPResponse();

            response = packageHandler.handlePackage(request);
            response = packageHandler.handlePackage(request);
            response = packageHandler.handlePackage(request);
            response = packageHandler.handlePackage(request);

            Assert.That(response.Message, Is.EqualTo("A package has been successfully bought"));
        }

        [Test, Order(3)]
        public void TestBuyPackageNoMoney()
        {
            string requestString = "POST /transactions/packages HTTP/1.1\n"
                + "Host: localhost:10001\n"
                + "User-Agent: curl/7.87.0\n"
                + "Accept: */*\n"
                + "Content-Type: application/json\n"
                + "Authorization: Bearer package-token\n"
                + "Content-Length: 0\n";
            HTTPRequest request = new HTTPRequest(requestString);

            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();
            PackageHandler packageHandler = new PackageHandler(connection);
            HTTPResponse response = new HTTPResponse();

            response = packageHandler.handlePackage(request);

            Assert.That(response.Message, Is.EqualTo("Not enough money for buying a card package"));
        }
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();
            IDbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT uid FROM users WHERE username = 'package'";
            NpgsqlCommand c = command as NpgsqlCommand;
            int userid = (int)c.ExecuteScalar();

            c.CommandText = "DELETE FROM stacks WHERE userid = @uid";
            IDbDataParameter uid = c.CreateParameter();
            uid.DbType = DbType.Int32;
            uid.ParameterName = "uid";
            c.Parameters.Add(uid);
            c.Parameters["uid"].Value = userid;
            c.ExecuteNonQuery();

            c.CommandText = "DELETE FROM cards WHERE id = @id";
            IDbDataParameter id = c.CreateParameter();
            id.DbType = DbType.String;
            id.ParameterName = "id";
            c.Parameters.Add(id);

            for(int i = 0; i <= 20; i++)
            {
                c.Parameters["id"].Value = i.ToString();
                c.ExecuteNonQuery();
            }
            c.CommandText = "DELETE FROM users WHERE username = 'package'";
            c.ExecuteNonQuery();
        }
    }
}
