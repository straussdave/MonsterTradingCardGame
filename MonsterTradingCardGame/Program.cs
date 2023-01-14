using Npgsql;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Configuration;
using Microsoft.AspNetCore.Http;
using System.Web.Http;
using MonsterTradingCardGame.Models;
using Newtonsoft.Json;
using NpgsqlTypes;
using System.Data;
using System.Security.Cryptography;
using System.Data.SqlClient;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Contexts;
using MonsterTradingCardGame;
using System.Runtime.InteropServices;
using System.Net.Sockets;

namespace HttpServer
{
    class Program
    {
        static void Main()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 10001);
            listener.Start();
            Console.WriteLine("Listening for incoming connections...");

            // Wait for a client to connect
            TcpClient client = listener.AcceptTcpClient();
            Console.WriteLine("Client connected!");

            // Get the client's stream
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[4096];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.Write(request);
            HTTPRequest req = new HTTPRequest(request);

            var lines = request.Split('\n');

            var line = lines[0];
            var lineSplit = line.Split(' ');
            req.Method = lineSplit[0];
            req.Url = lineSplit[1];
            req.Version = lineSplit[2];

            line = lines[1];
            lineSplit = line.Split(' ');
            req.Host = lineSplit[1];

            line = lines[5];
            lineSplit = line.Split(':');
            req.Authorization = lineSplit[1].Substring(1);

            line = lines[6];
            lineSplit = line.Split(' ');
            req.ContentLength = Int32.Parse(lineSplit[1]);

            line = lines[8];
            req.Body = line;

            Console.Write("\n"req.Body);

            // Send a response to the client
            string response = "HTTP/1.1 200 OK\r\nContent-Length: 12\r\n\r\nHello World!";
            byte[] responseBytes = Encoding.ASCII.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);

            // Close the client connection
            client.Close();
            listener.Stop();
        }
    }
}
        /*
            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://+:10001/");
            listener.Start();
            Console.WriteLine("Listening for requests...");
            BattleHandler bh = new BattleHandler();
            Thread thread = new Thread(() => HandleBattles(bh));
            thread.Start();

            while (true)
            {
                // Wait for a request to be made
                HttpListenerContext context = listener.GetContext();
                object args = new object[3] { context, connection, bh };
                Thread t = new Thread(() => HandleClient(args));
                t.Start();
            }
        }

        static void HandleClient(object args)
        {
            Array argArray = new object[3];
            argArray = (Array)args;
            IDbConnection connection = (IDbConnection)argArray.GetValue(1);
            connection.Open();
            HttpListenerContext context = (HttpListenerContext)argArray.GetValue(0);
            // Get the request and response objects
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            BattleHandler battleHandler = (BattleHandler)argArray.GetValue(2);

            string responseContent = "";
            // Determine the HTTP verb of the request
            if (request.RawUrl == "/users")
            {
                switch (request.HttpMethod)
                {
                    case "POST": //register a new user

                        //get username and password from request body
                        //insert new user into db

                        string userJson = GetRequestData(request);
                        User user = JsonConvert.DeserializeObject<User>(userJson);

                        IDbCommand command = connection.CreateCommand();

                        //check if user exists, if it does not exist then insert it into db
                        command.CommandText = "SELECT * FROM users WHERE username = @username";

                        NpgsqlCommand c = command as NpgsqlCommand;

                        IDbDataParameter username = c.CreateParameter();
                        username.DbType = DbType.String;
                        username.ParameterName = "username";
                        c.Parameters.Add(username);
                        c.Prepare();
                        c.Parameters["username"].Value = user.username;

                        using (IDataReader reader = c.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // A user with this username already exists
                                response.StatusCode = (int)HttpStatusCode.Conflict;
                                response.StatusDescription = "User with same username already registered";
                            }
                            else
                            {
                                // The username is available
                                reader.Close();
                                IDbDataParameter password = c.CreateParameter();
                                password.DbType = DbType.String;
                                password.ParameterName = "password";
                                c.Parameters.Add(password);
                                c.Prepare();

                                string pwdHashed = ComputeSha256Hash(user.password);
                                c.Parameters["password"].Value = pwdHashed;

                                c.CommandText = "INSERT INTO users (username, password) VALUES (@username, @password)";
                                c.Prepare();
                                c.ExecuteNonQuery();

                                response.StatusCode = (int)HttpStatusCode.Created;
                                response.StatusDescription = "User successfully created";

                            }
                        }
                        break;
                    default:
                        // Return an error for other HTTP verbs
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.StatusDescription = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.RawUrl.StartsWith("/users/"))
            {
                string username = request.RawUrl.Substring(7);
                UserData userData = new UserData();
                switch (request.HttpMethod)
                {
                    case "GET": //Retrieve user data for the given username
                        if (!Authorize(request, username))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.StatusDescription = "Access token is missing or invalid";
                            break;
                        }

                        IDbCommand command = connection.CreateCommand();
                        command.CommandText = "SELECT name, bio, image FROM users WHERE username = @usernameParam";

                        NpgsqlCommand c = command as NpgsqlCommand;

                        IDbDataParameter usernameParam = c.CreateParameter();
                        usernameParam.DbType = DbType.String;
                        usernameParam.ParameterName = "usernameParam";
                        c.Parameters.Add(usernameParam);
                        c.Prepare();
                        c.Parameters["usernameParam"].Value = username;

                        using (IDataReader reader = c.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                // No match found
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                response.StatusDescription = "User not found";
                                break;
                            }
                            else
                            {
                                //retrieve user data
                                string name = reader["name"].ToString();
                                string bio = reader["bio"].ToString();
                                string image = reader["image"].ToString();

                                userData.Bio = bio;
                                userData.Name = name;
                                userData.Image = image;

                                responseContent = JsonConvert.SerializeObject(userData);

                                response.StatusCode = (int)HttpStatusCode.OK;
                                response.StatusDescription = "Data sucessfully retrieved";
                            }
                        }
                        break;
                    case "PUT": //Updates user data for given username
                        string userDataJson = GetRequestData(request);
                        userData = JsonConvert.DeserializeObject<UserData>(userDataJson);

                        if (!Authorize(request, username))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.StatusDescription = "Access token is missing or invalid";
                            break;
                        }

                        command = connection.CreateCommand();
                        command.CommandText = "SELECT * FROM users WHERE username = @usernameParam";

                        c = command as NpgsqlCommand;

                        usernameParam = c.CreateParameter();
                        usernameParam.DbType = DbType.String;
                        usernameParam.ParameterName = "usernameParam";
                        c.Parameters.Add(usernameParam);
                        c.Prepare();
                        c.Parameters["usernameParam"].Value = username;

                        using (IDataReader reader = c.ExecuteReader())
                        {
                            if (!(reader.Read()))
                            {
                                // No match found
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                response.StatusDescription = "User not found";
                                break;
                            }
                            else
                            {
                                //update user data
                                reader.Close();
                                c.CommandText = "UPDATE users SET " +
                                    "bio = @bioParam, name = @nameParam, image = @imageParam " +
                                    "WHERE username = @usernameParam";

                                IDbDataParameter bioParam = c.CreateParameter();
                                bioParam.DbType = DbType.String;
                                bioParam.ParameterName = "bioParam";
                                c.Parameters.Add(bioParam);

                                IDbDataParameter nameParam = c.CreateParameter();
                                nameParam.DbType = DbType.String;
                                nameParam.ParameterName = "nameParam";
                                c.Parameters.Add(nameParam);

                                IDbDataParameter imageParam = c.CreateParameter();
                                imageParam.DbType = DbType.String;
                                imageParam.ParameterName = "imageParam";
                                c.Parameters.Add(imageParam);

                                c.Prepare();
                                c.Parameters["nameParam"].Value = userData.Name;
                                c.Parameters["bioParam"].Value = userData.Bio;
                                c.Parameters["imageParam"].Value = userData.Image;

                                c.ExecuteNonQuery();

                                response.StatusCode = (int)HttpStatusCode.OK;
                                response.StatusDescription = "User sucessfully updated.";
                            }
                        }
                        break;
                    default:
                        // Return an error for other HTTP verbs
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.StatusDescription = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.RawUrl == "/sessions") //Login with existing user
            {
                switch (request.HttpMethod)
                {
                    case "POST":
                        string userJson = GetRequestData(request);
                        User user = JsonConvert.DeserializeObject<User>(userJson);
                        IDbCommand command = connection.CreateCommand();
                        //check if user exists
                        command.CommandText = "SELECT * FROM users WHERE " +
                                              "username = @username and password = @password";

                        NpgsqlCommand c = command as NpgsqlCommand;

                        IDbDataParameter username = c.CreateParameter();
                        username.DbType = DbType.String;
                        username.ParameterName = "username";
                        c.Parameters.Add(username);

                        IDbDataParameter password = c.CreateParameter();
                        password.DbType = DbType.String;
                        password.ParameterName = "password";
                        c.Parameters.Add(password);

                        c.Prepare();

                        string pwdHashed = ComputeSha256Hash(user.password);
                        c.Parameters["password"].Value = pwdHashed;
                        c.Parameters["username"].Value = user.username;

                        using (IDataReader reader = c.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Match found
                                response.StatusCode = (int)HttpStatusCode.OK;
                                response.StatusDescription = "User login successful";
                                responseContent = user.username + "-mtcgToken";
                                response.ContentType = "json/application";
                            }
                            else
                            {
                                // No match found
                                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                                response.StatusDescription = "Invalid username/password provided";
                            }
                        }
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.StatusDescription = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.RawUrl == "/packages")
            {
                switch (request.HttpMethod)
                {
                    case "POST":
                        //Get data from request, store all 5 cards from request in cards table with the same package number, 
                        //get highest package number from table and increment it by 1

                        if (request.Headers["Authorization"] != null && request.Headers["Authorization"].StartsWith("Bearer"))
                        {
                            if (!AuthorizeAdmin(request, true))
                            {
                                response.StatusCode = (int)HttpStatusCode.Forbidden;
                                response.StatusDescription = "Provided user is not \"admin\"";
                                break;
                            }
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.StatusDescription = "Access token is missing or invalid";
                            break;
                        }

                        string packagesJson = GetRequestData(request);
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
                            response.StatusDescription = "At least one card in the packages already exists";
                            break;
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
                            response.StatusDescription = "error unknown";
                            break;
                        }
                        response.StatusCode = (int)HttpStatusCode.Created;
                        response.StatusDescription = "Package and cards successfully created";
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.StatusDescription = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.RawUrl == "/transactions/packages")
            {
                switch (request.HttpMethod)
                {
                    case "POST":
                        //Buys a card package with the money of the provided user
                        //looks for the cards with the smallest packageNR
                        //adds the cards uuids and the user id to the stacks table
                        //sets packageNR of these cards to NULL
                        //substract 5 coins from user

                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.StatusDescription = "Access token is missing or invalid";
                            break;
                        }

                        string header = request.Headers["Authorization"];
                        int startOfToken = header.IndexOf("Bearer ") + 7;
                        string user = header.Substring(startOfToken, header.IndexOf("-") - startOfToken);

                        Console.WriteLine(user);

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
                                response.StatusDescription = "No user found";
                                break;
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
                                    response.StatusDescription = "No card package available for buying";
                                    break;
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
                            response.StatusDescription = "A package has been successfully bought";
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.Forbidden;
                            response.StatusDescription = "Not enough money for buying a card package";
                            break;
                        }

                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.StatusDescription = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.RawUrl == "/cards")
            {
                switch (request.HttpMethod)
                {
                    case "GET":

                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.StatusDescription = "Access token is missing or invalid";
                            break;
                        }

                        string header = request.Headers["Authorization"];
                        int startOfToken = header.IndexOf("Bearer ") + 7;
                        string user = header.Substring(startOfToken, header.IndexOf("-") - startOfToken);
                        List<Card> cards = new List<Card>();

                        IDbCommand command = connection.CreateCommand();
                        command.CommandText = "SELECT uid FROM users WHERE username = @username";
                        NpgsqlCommand c = command as NpgsqlCommand;

                        IDbDataParameter username = c.CreateParameter();
                        username.DbType = DbType.String;
                        username.ParameterName = "username";
                        c.Parameters.Add(username);
                        c.Parameters["username"].Value = user;
                        int userid = Convert.ToInt16(c.ExecuteScalar());

                        c.CommandText = "SELECT cards.id, cards.name, cards.damage " +
                                        "FROM users JOIN stacks ON users.uid = stacks.userid " +
                                        "JOIN cards ON cards.id = stacks.carduuid " +
                                        "WHERE users.uid = @uid";

                        IDbDataParameter uid = c.CreateParameter();
                        uid.DbType = DbType.Int32;
                        uid.ParameterName = "uid";
                        c.Parameters.Add(uid);
                        c.Parameters["uid"].Value = userid;

                        bool hasCards = false;
                        using (IDataReader reader = c.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Card newCard = new Card();
                                newCard.Id = reader["id"] as string;
                                newCard.Name = reader["name"] as string;
                                newCard.Damage = (float)Convert.ToDecimal(reader["damage"]);
                                cards.Add(newCard);
                                hasCards = true;
                            }
                        }

                        if (!hasCards)
                        {
                            response.StatusCode = (int)HttpStatusCode.NoContent;
                            response.StatusDescription = "The request was fine, but the user doesn't have any cards";
                            break;
                        }
                        responseContent = JsonConvert.SerializeObject(cards, Formatting.Indented);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.StatusDescription = "The user has cards, the response contains these";
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.StatusDescription = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.RawUrl.StartsWith("/deck"))
            {
                switch (request.HttpMethod)
                {
                    case "PUT":
                        //Send four card IDs to configure a new full deck. A failed request will not change the previously defined deck.

                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.StatusDescription = "Access token is missing or invalid";
                            break;
                        }

                        string cardIds = GetRequestData(request);
                        List<string> cardList = cardIds.Split(',').ToList();
                        List<string> cardListTrimmed = new List<string>();
                        foreach (string cardId in cardList)
                        {
                            cardListTrimmed.Add(cardId.Trim(new Char[] { '"', '[', ']', ' ' }));
                        }

                        if (cardListTrimmed.Count < 4 || cardListTrimmed.Count > 4)
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                            response.StatusDescription = "The provided deck did not include the required amount of cards";
                            break;
                        }

                        //check if user is owner of these cards

                        string header = request.Headers["Authorization"];
                        int startOfToken = header.IndexOf("Bearer ") + 7;
                        string user = header.Substring(startOfToken, header.IndexOf("-") - startOfToken);

                        IDbCommand command = connection.CreateCommand();
                        command.CommandText = "SELECT uid FROM users WHERE username = @username";
                        NpgsqlCommand c = command as NpgsqlCommand;

                        IDbDataParameter username = c.CreateParameter();
                        username.DbType = DbType.String;
                        username.ParameterName = "username";
                        c.Parameters.Add(username);
                        c.Parameters["username"].Value = user;
                        int userid = Convert.ToInt16(c.ExecuteScalar());

                        c.CommandText = "SELECT userid FROM stacks WHERE carduuid = @carduuid";
                        IDbDataParameter carduuid = c.CreateParameter();
                        carduuid.DbType = DbType.String;
                        carduuid.ParameterName = "carduuid";
                        c.Parameters.Add(carduuid);

                        c.Prepare();

                        bool error = false;
                        foreach (string cardId in cardListTrimmed)
                        {
                            c.Parameters["carduuid"].Value = cardId;
                            bool cardExists = false;
                            int owner = -1;
                            using (IDataReader reader = c.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    Console.WriteLine(reader["userid"]);
                                    owner = (int)reader["userid"];
                                    cardExists = true;
                                }
                            }
                            if (!cardExists || owner != userid)
                            {
                                error = true;
                                response.StatusCode = (int)HttpStatusCode.Forbidden;
                                response.StatusDescription = "At least one of the provided cards does not belong to the user or is not available.";
                                break;
                            }
                        }
                        if (error)
                        {
                            break;
                        }

                        c.CommandText = "UPDATE stacks SET is_current_deck = True WHERE carduuid = @carduuidParam";
                        IDbDataParameter carduuidParam = c.CreateParameter();
                        carduuidParam.DbType = DbType.String;
                        carduuidParam.ParameterName = "carduuidParam";
                        c.Parameters.Add(carduuidParam);

                        c.Prepare();

                        foreach (string cardId in cardListTrimmed)
                        {
                            c.Parameters["carduuidParam"].Value = cardId;
                            c.ExecuteNonQuery();
                        }

                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.StatusDescription = "The deck has been successfully configured";
                        break;
                    case "GET":
                        //Returns the cards that are owned by the users and are put into the deck
                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.StatusDescription = "Access token is missing or invalid";
                            break;
                        }

                        header = request.Headers["Authorization"];
                        startOfToken = header.IndexOf("Bearer ") + 7;
                        user = header.Substring(startOfToken, header.IndexOf("-") - startOfToken);

                        command = connection.CreateCommand();
                        command.CommandText = "SELECT uid FROM users WHERE username = @usernameParam";
                        c = command as NpgsqlCommand;

                        IDbDataParameter usernameParam = c.CreateParameter();
                        usernameParam.DbType = DbType.String;
                        usernameParam.ParameterName = "usernameParam";
                        c.Parameters.Add(usernameParam);
                        c.Parameters["usernameParam"].Value = user;
                        userid = Convert.ToInt16(c.ExecuteScalar());

                        c.CommandText = "SELECT cards.id, cards.name, cards.damage " +
                                        "FROM users JOIN stacks ON users.uid = stacks.userid " +
                                        "JOIN cards ON cards.id = stacks.carduuid " +
                                        "WHERE users.uid = @uid AND stacks.is_current_deck = true";

                        IDbDataParameter uid = c.CreateParameter();
                        uid.DbType = DbType.Int32;
                        uid.ParameterName = "uid";
                        c.Parameters.Add(uid);
                        c.Parameters["uid"].Value = userid;

                        List<Card> deck = new List<Card>();
                        bool hasCards = false;
                        using (IDataReader reader = c.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Card newCard = new Card();
                                newCard.Id = reader["id"] as string;
                                newCard.Name = reader["name"] as string;
                                newCard.Damage = (float)Convert.ToDecimal(reader["damage"]);
                                deck.Add(newCard);
                                hasCards = true;
                            }
                        }
                        if (!hasCards)
                        {
                            response.StatusCode = (int)HttpStatusCode.NoContent;
                            response.StatusDescription = "The request was fine, but the deck doesn't have any cards";
                            break;
                        }

                        string[] keys = request.QueryString.AllKeys;
                        if (keys.Length > 0)
                        {
                            if (request.QueryString.GetValues(keys[0])[0] == "plain")
                            {
                                int i = 0;
                                foreach (Card card in deck)
                                {
                                    responseContent += deck.ElementAt(i).Id + '\n';
                                    responseContent += deck.ElementAt(i).Name + '\n';
                                    responseContent += deck.ElementAt(i).Damage.ToString() + '\n';
                                    i++;
                                }

                                response.StatusCode = (int)HttpStatusCode.OK;
                                response.StatusDescription = "The deck has cards, the response contains these";
                                break;
                            }
                        }

                        responseContent = JsonConvert.SerializeObject(deck, Formatting.Indented);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.StatusDescription = "The deck has cards, the response contains these";
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.StatusDescription = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.RawUrl == "/battles")
            {
                switch (request.HttpMethod)
                {
                    case "POST":
                        Console.WriteLine("Battle request");
                        //1. get player cards, store it in player variable
                        //2. pass this player variable to battlehandler to store in queue
                        //3. when 2 players are in queue -> battle
                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.StatusDescription = "Access token is missing or invalid";
                            break;
                        }
                        string header = request.Headers["Authorization"];
                        int startOfToken = header.IndexOf("Bearer ") + 7;
                        string user = header.Substring(startOfToken, header.IndexOf("-") - startOfToken);

                        IDbCommand command = connection.CreateCommand();
                        command.CommandText = "SELECT uid FROM users WHERE username = @usernameParam";
                        NpgsqlCommand c = command as NpgsqlCommand;

                        IDbDataParameter usernameParam = c.CreateParameter();
                        usernameParam.DbType = DbType.String;
                        usernameParam.ParameterName = "usernameParam";
                        c.Parameters.Add(usernameParam);
                        c.Parameters["usernameParam"].Value = user;
                        int userid = Convert.ToInt16(c.ExecuteScalar());

                        c.CommandText = "SELECT cards.id, cards.name, cards.damage " +
                                        "FROM users JOIN stacks ON users.uid = stacks.userid " +
                                        "JOIN cards ON cards.id = stacks.carduuid " +
                                        "WHERE users.uid = @uid AND stacks.is_current_deck = true";

                        IDbDataParameter uid = c.CreateParameter();
                        uid.DbType = DbType.Int32;
                        uid.ParameterName = "uid";
                        c.Parameters.Add(uid);
                        c.Parameters["uid"].Value = userid;

                        List<Card> deck = new List<Card>();
                        bool hasCards = false;

                        using (IDataReader reader = c.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Card newCard = new Card();
                                newCard.Id = reader["id"] as string;
                                newCard.Name = reader["name"] as string;
                                newCard.Damage = (float)Convert.ToDecimal(reader["damage"]);
                                deck.Add(newCard);
                                hasCards = true;
                            }
                        }
                        if (!hasCards)
                        {
                            response.StatusCode = (int)HttpStatusCode.NoContent;
                            response.StatusDescription = "The request was fine, but the deck doesn't have any cards";
                            break;
                        }
                        Player player = new Player();
                        string winner = "";
                        player.Name = user;
                        player.Deck = deck;
                        bool found = false;
                        int battleId = battleHandler.Enqueue(player);
                        Console.WriteLine("Enqueued Player");
                        while (true)
                        {
                            Thread.Sleep(1000);
                            if (battleHandler.FinishedBattles.Contains(battleId))
                            {
                                foreach (string[] result in battleHandler.BattleHistory)
                                {
                                    if (Int32.Parse(result[0]) == battleId)
                                    {
                                        winner = result[1];
                                        found = true;
                                        break;
                                    }
                                }
                                if (found)
                                    break;
                                Console.WriteLine("Waiting");
                            }
                        }
                        Console.WriteLine(winner);
                        break;
                    default:
                        break;
                }
            }
            else if (request.RawUrl == "/stats")
            {
                switch (request.HttpMethod)
                {
                    case "GET":
                        //Retrieves the stats for the requesting user.
                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.StatusDescription = "Access token is missing or invalid";
                            break;
                        }
                        string header = request.Headers["Authorization"];
                        int startOfToken = header.IndexOf("Bearer ") + 7;
                        string user = header.Substring(startOfToken, header.IndexOf("-") - startOfToken);

                        IDbCommand command = connection.CreateCommand();
                        command.CommandText = "SELECT name, elo, wins, losses FROM users WHERE username = @usernameParam";

                        NpgsqlCommand c = command as NpgsqlCommand;

                        IDbDataParameter usernameParam = c.CreateParameter();
                        usernameParam.DbType = DbType.String;
                        usernameParam.ParameterName = "usernameParam";
                        c.Parameters.Add(usernameParam);
                        c.Prepare();
                        c.Parameters["usernameParam"].Value = user;

                        using (IDataReader reader = c.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                // No match found
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                response.StatusDescription = "User not found";
                                break;
                            }
                            else
                            {
                                //retrieve user data
                                string name = reader["name"].ToString();
                                string elo = reader["elo"].ToString();
                                int wins = (int)reader["wins"];
                                int losses = (int)reader["losses"];
                                userStats stats = new userStats();
                                stats.Name = name;
                                stats.Elo = elo;
                                stats.Wins = wins;
                                stats.Losses = losses;

                                responseContent = JsonConvert.SerializeObject(stats);

                                response.StatusCode = (int)HttpStatusCode.OK;
                                response.StatusDescription = "The stats could be retrieved successfully.";
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            else if (request.RawUrl == "/score")
            {
                switch (request.HttpMethod)
                {
                    case "GET":
                        //Lists all users ordered by elo
                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.StatusDescription = "Access token is missing or invalid";
                            break;
                        }
                        IDbCommand command = connection.CreateCommand();
                        command.CommandText = "SELECT name, elo FROM users ORDER BY elo DESC";

                        NpgsqlCommand c = command as NpgsqlCommand;

                        List<string[]> scoreboard = new List<string[]>();
                        using (IDataReader reader = c.ExecuteReader())
                        {
                            while(reader.Read())
                            {
                                string[] new_entry = { reader["name"].ToString(), reader["elo"].ToString() };
                                if (reader["name"].ToString() == "")
                                {
                                    new_entry[0] = "anonymous";
                                }
                                scoreboard.Add(new_entry);
                            }
                        }
                        responseContent = JsonConvert.SerializeObject(scoreboard, Formatting.Indented);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.StatusDescription = "The stats could be retrieved successfully.";
                        break;
                    default:
                        break;
                }
            }

            connection.Close();
            //Set the response content and content type
            byte[] buffer = Encoding.UTF8.GetBytes(responseContent);
            response.ContentLength64 = buffer.Length;

            // Send the response to the client
            Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }

        static public void HandleBattles(BattleHandler bh)
        {
            while (true)
            {
                Thread.Sleep(250);
                if(bh.WaitingPlayers.Count >= 2)
                {
                    bh.Battle();
                }
                Console.Write(bh.WaitingPlayers.Count);
            }
        }

        public static string GetRequestData(HttpListenerRequest request)
        {
            if (!request.HasEntityBody)
            {
                return null;
            }
            using (Stream body = request.InputStream) // here we have data
            {
                using (var reader = new StreamReader(body, request.ContentEncoding))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        static bool Authorize(HttpListenerRequest request)
        {
            string authToken = request.Headers["Authorization"];
            if (authToken.StartsWith("B"))
            {
                return true;
            }
            else
                return false;
        }

        static bool Authorize(HttpListenerRequest request, string username)
        {
            string authToken = request.Headers["Authorization"];
            if (authToken.StartsWith("B"))
            {
                authToken = authToken.Substring(7);
                if (!(authToken == username + "-mtcgToken"))
                    return false;
                else
                    return true;
            }
            else
                return false;
        }

        static bool AuthorizeAdmin(HttpListenerRequest request, bool checkAdmin)
        {
            string authToken = request.Headers["Authorization"];
            if (authToken.StartsWith("B"))
            {
                authToken = authToken.Substring(7);
                if (!(authToken == "admin-mtcgToken"))
                    return false;
                else
                    return true;
            }
            else
                return false;
        }
    }
}
        */