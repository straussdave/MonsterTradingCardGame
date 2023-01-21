using MonsterTradingCardGame.Models;
using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MonsterTradingCardGame
{
    public class PackageHandler
    {
        public IDbConnection connection;
        public PackageHandler(IDbConnection con)
        {
            this.connection = con;
        }

        public HTTPResponse handlePackage(HTTPRequest request)
        {
            HTTPResponse response = new HTTPResponse();
            if (request.Url == "/packages")
            {
                switch (request.Method)
                {
                    case "POST":
                        response = createPackage(request);
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.Url == "/transactions/packages")
            {
                switch (request.Method)
                {
                    case "POST":
                        response = buyPackage(request);
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            return response;
        }

        public HTTPResponse createPackage(HTTPRequest request)
        {
            //Get data from request, store all 5 cards from request in cards table with the same package number, 
            //get highest package number from table and increment it by 1
            HTTPResponse response = new HTTPResponse();
            string packagesJson = request.Body;
            Console.WriteLine(packagesJson);
            Card[] cards = JsonConvert.DeserializeObject<Card[]>(packagesJson);

            IDbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM cards WHERE id = @id";
            NpgsqlCommand c = command as NpgsqlCommand;

            IDbDataParameter id = c.CreateParameter();
            id.DbType = DbType.String;
            id.ParameterName = "id";
            c.Parameters.Add(id);

            int i = 0;
            bool error = false;
            foreach (Card card in cards)
            {
                c.Parameters["id"].Value = cards[i].Id;
                using (IDataReader reader = c.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        //id already exists
                        error = true;
                        break;
                    }
                    else
                    {
                        i++;
                    }
                }
            }

            if (error == true)
            {
                response.StatusCode = (int)HttpStatusCode.Conflict;
                response.Message = "At least one card in the packages already exists";
                return response;
            }

            c.CommandText = "SELECT COUNT(*) FROM cards";

            int packagenr = -1;

            object result = command.ExecuteScalar();
            int rowCount = Convert.ToInt32(result);
            if (rowCount > 0)
            {
                //There is at least 1 card
                c.CommandText = "SELECT MAX(packagenr) FROM cards";
                result = command.ExecuteScalar();
                try
                {
                    packagenr = Convert.ToInt32(result) + 1;
                }
                catch
                {
                    packagenr = 0;
                }
            }
            else
            {
                // There is no card yet
                packagenr = 0;
            }

            c.CommandText = "INSERT INTO cards (id, name, damage, packagenr) " +
                            "VALUES (@idParam, @name, @damage, @packagenr)";

            IDbDataParameter idParam = c.CreateParameter();
            idParam.DbType = DbType.String;
            idParam.ParameterName = "idParam";
            c.Parameters.Add(idParam);

            IDbDataParameter name = c.CreateParameter();
            name.DbType = DbType.String;
            name.ParameterName = "name";
            c.Parameters.Add(name);

            IDbDataParameter damage = c.CreateParameter();
            damage.DbType = DbType.Decimal;

            damage.ParameterName = "damage";
            c.Parameters.Add(damage);

            IDbDataParameter packagenrParam = c.CreateParameter();
            packagenrParam.DbType = DbType.Int16;
            packagenrParam.ParameterName = "packagenr";
            c.Parameters.Add(packagenrParam);

            c.Prepare();

            i = 0;
            foreach (Card card in cards)
            {

                c.Parameters["idParam"].Value = cards[i].Id;
                c.Parameters["name"].Value = cards[i].Name;
                c.Parameters["damage"].Value = cards[i].Damage;
                c.Parameters["packagenr"].Value = packagenr;

                try
                {
                    c.ExecuteNonQuery();
                }
                catch
                {
                    error = true;
                    break;
                }
                i++;
            }
            if (error == true)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Message = "error unknown";
                return response;
            }
            response.StatusCode = (int)HttpStatusCode.Created;
            response.Message = "Package and cards successfully created";
            return response;
        }

        public HTTPResponse buyPackage(HTTPRequest request)
        {
            //Buys a card package with the money of the provided user
            //looks for the cards with the smallest packageNR
            //adds the cards uuids and the user id to the stacks table
            //sets packageNR of these cards to NULL
            //substract 5 coins from user
            HTTPResponse response = new HTTPResponse();
            string header = request.Authorization;
            int startOfToken = header.IndexOf("Bearer ") + 7;
            string user = header.Substring(startOfToken, header.IndexOf("-") - startOfToken);

            IDbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT id FROM cards WHERE packagenr = (SELECT MIN(packagenr) FROM cards)";
            NpgsqlCommand c = command as NpgsqlCommand;

            c.CommandText = "SELECT money FROM users WHERE username = @username";
            IDbDataParameter username = c.CreateParameter();
            username.DbType = DbType.String;
            username.ParameterName = "username";
            c.Parameters.Add(username);
            c.Parameters["username"].Value = user;

            int money = 0;
            int i = 0;
            using (IDataReader reader = c.ExecuteReader())
            {
                if (reader.Read())
                {
                    money = reader.GetInt16(0);
                }
                else
                {
                    //no rows found
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Message = "No user found";
                    return response;
                }
            }

            if (money >= 5)
            {
                c.CommandText = "SELECT id FROM cards WHERE packagenr = (SELECT MIN(packagenr) FROM cards)";
                string[] cardIds = new string[5];

                i = 0;
                using (IDataReader reader = c.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cardIds[i] = reader[0] as string;
                        i++;
                    }
                    if (i == 0)
                    {
                        //no rows found
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        response.Message = "No card package available for buying";
                        return response;
                    }
                }

                c.CommandText = "SELECT uid FROM users WHERE username = @usernameParam";

                IDbDataParameter usernameParam = c.CreateParameter();
                usernameParam.DbType = DbType.String;
                usernameParam.ParameterName = "usernameParam";
                c.Parameters.Add(usernameParam);
                c.Parameters["usernameParam"].Value = user;

                var userId = command.ExecuteScalar();

                c.CommandText = "INSERT INTO stacks (userid, carduuid) VALUES (@userid, @carduuid)";

                IDbDataParameter userid = c.CreateParameter();
                userid.DbType = DbType.Int32;
                userid.ParameterName = "userid";
                c.Parameters.Add(userid);
                c.Parameters["userid"].Value = userId;

                IDbDataParameter carduuid = c.CreateParameter();
                carduuid.DbType = DbType.String;
                carduuid.ParameterName = "carduuid";
                c.Parameters.Add(carduuid);

                c.Prepare();

                i = 0;
                foreach (string cardId in cardIds)
                {
                    c.Parameters["carduuid"].Value = cardIds[i];
                    c.ExecuteNonQuery();
                    i++;
                }

                c.CommandText = "UPDATE cards SET packagenr = NULL WHERE id = @id";
                IDbDataParameter id = c.CreateParameter();
                id.DbType = DbType.String;
                id.ParameterName = "id";
                c.Parameters.Add(id);
                c.Prepare();

                i = 0;
                foreach (string cardId in cardIds)
                {
                    c.Parameters["id"].Value = cardIds[i];
                    c.ExecuteNonQuery();
                    i++;
                }

                c.CommandText = "UPDATE users SET money = money - 5 WHERE uid = @uid";
                IDbDataParameter uid = c.CreateParameter();
                uid.DbType = DbType.Int32;
                uid.ParameterName = "uid";
                c.Parameters.Add(uid);
                c.Parameters["uid"].Value = userId;
                c.ExecuteNonQuery();

                response.StatusCode = (int)HttpStatusCode.Created;
                response.Message = "A package has been successfully bought";
                return response;
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                response.Message = "Not enough money for buying a card package";
                return response;
            }
        }
    }
}
