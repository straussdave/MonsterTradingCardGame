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
using System.Runtime.InteropServices.ComTypes;

namespace HttpServer
{
    class Program
    {
        static void Main()
        {
            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();

            TcpListener listener = new TcpListener(IPAddress.Any, 10001);
            listener.Start();
            Console.WriteLine("Listening for incoming connections...");
            BattleHandler bh = new BattleHandler();
            Thread battleThread = new Thread(() => HandleBattles(bh));
            battleThread.Start();

            while (true)
            {
                // Wait for a client to connect
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("Client connected!");

                object args = new object[3] { connection, bh, client };
                Thread t = new Thread(() => HandleClient(args));
                t.Start();
            }
            connection.Close();
            listener.Stop();
        }

        static void HandleClient(object args)
        {
            Array argArray = new object[3];
            argArray = (Array)args;
            IDbConnection connection = (IDbConnection)argArray.GetValue(0);

            BattleHandler battleHandler = (BattleHandler)argArray.GetValue(1);

            TcpClient client = (TcpClient)argArray.GetValue(2);

            // Get the client's stream
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[4096];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string req = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            HTTPRequest request = new HTTPRequest(req);

            HTTPResponse response = new HTTPResponse();
            response.ResponseContent = "";
            // Determine the HTTP verb of the request
            if (request.Url == "/users")
            {
                switch (request.Method)
                {
                    case "POST": //register a new user

                        //get username and password from request body
                        //insert new user into db

                        string userJson = request.Body;
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
                                response.Message = "User with same username already registered";
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
                                response.Message = "User successfully created";

                            }
                        }
                        break;
                    default:
                        // Return an error for other HTTP verbs
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.Url.StartsWith("/users/"))
            {
                string username = request.Url.Substring(7);
                UserData userData = new UserData();
                switch (request.Method)
                {
                    case "GET": //Retrieve user data for the given username
                        if (!Authorize(request, username))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.Message = "Access token is missing or invalid";
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
                                response.Message = "User not found";
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

                                response.ResponseContent = JsonConvert.SerializeObject(userData);

                                response.StatusCode = (int)HttpStatusCode.OK;
                                response.Message = "Data sucessfully retrieved";
                            }
                        }
                        break;
                    case "PUT": //Updates user data for given username
                        string userDataJson = request.Body;
                        userData = JsonConvert.DeserializeObject<UserData>(userDataJson);

                        if (!Authorize(request, username))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.Message = "Access token is missing or invalid";
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
                                response.Message = "User not found";
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
                                response.Message = "User sucessfully updated.";
                            }
                        }
                        break;
                    default:
                        // Return an error for other HTTP verbs
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.Url == "/sessions") //Login with existing user
            {
                switch (request.Method)
                {
                    case "POST":
                        string userJson = request.Body;
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
                                response.Message = "User login successful";
                                response.ResponseContent = user.username + "-mtcgToken";
                                response.ContentType = "json/application";
                            }
                            else
                            {
                                // No match found
                                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                                response.Message = "Invalid username/password provided";
                            }
                        }
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.Url == "/packages")
            {
                switch (request.Method)
                {
                    case "POST":
                        //Get data from request, store all 5 cards from request in cards table with the same package number, 
                        //get highest package number from table and increment it by 1

                        if (request.Authorization != null && request.Authorization.StartsWith("Bearer"))
                        {
                            if (!AuthorizeAdmin(request, true))
                            {
                                response.StatusCode = (int)HttpStatusCode.Forbidden;
                                response.Message = "Provided user is not \"admin\"";
                                break;
                            }
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.Message = "Access token is missing or invalid";
                            break;
                        }

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
                            response.Message = "error unknown";
                            break;
                        }
                        response.StatusCode = (int)HttpStatusCode.Created;
                        response.Message = "Package and cards successfully created";
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
                        //Buys a card package with the money of the provided user
                        //looks for the cards with the smallest packageNR
                        //adds the cards uuids and the user id to the stacks table
                        //sets packageNR of these cards to NULL
                        //substract 5 coins from user

                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.Message = "Access token is missing or invalid";
                            break;
                        }

                        string header = request.Authorization;
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
                                response.Message = "No user found";
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
                                    response.Message = "No card package available for buying";
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
                            response.Message = "A package has been successfully bought";
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.Forbidden;
                            response.Message = "Not enough money for buying a card package";
                            break;
                        }

                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.Url == "/cards")
            {
                switch (request.Method)
                {
                    case "GET":

                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.Message = "Access token is missing or invalid";
                            break;
                        }

                        string header = request.Authorization;
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
                            response.Message = "The request was fine, but the user doesn't have any cards";
                            break;
                        }
                        response.ResponseContent = JsonConvert.SerializeObject(cards, Formatting.Indented);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.Message = "The user has cards, the response contains these";
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.Url.StartsWith("/deck"))
            {
                switch (request.Method)
                {
                    case "PUT":
                        //Send four card IDs to configure a new full deck. A failed request will not change the previously defined deck.

                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.Message = "Access token is missing or invalid";
                            break;
                        }

                        string cardIds = request.Body;
                        List<string> cardList = cardIds.Split(',').ToList();
                        List<string> cardListTrimmed = new List<string>();
                        foreach (string cardId in cardList)
                        {
                            cardListTrimmed.Add(cardId.Trim(new Char[] { '"', '[', ']', ' ' }));
                        }

                        if (cardListTrimmed.Count < 4 || cardListTrimmed.Count > 4)
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                            response.Message = "The provided deck did not include the required amount of cards";
                            break;
                        }

                        //check if user is owner of these cards

                        string header = request.Authorization;
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
                                response.Message = "At least one of the provided cards does not belong to the user or is not available.";
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
                        response.Message = "The deck has been successfully configured";
                        break;
                    case "GET":
                        //Returns the cards that are owned by the users and are put into the deck
                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.Message = "Access token is missing or invalid";
                            break;
                        }

                        header = request.Authorization;
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
                            response.Message = "The request was fine, but the deck doesn't have any cards";
                            break;
                        }

                        string queryString = request.QueryString;
                        if (queryString == "format=plain")
                        {
                            int i = 0;
                            foreach (Card card in deck)
                            {
                                response.ResponseContent += deck.ElementAt(i).Id + '\n';
                                response.ResponseContent += deck.ElementAt(i).Name + '\n';
                                response.ResponseContent += deck.ElementAt(i).Damage.ToString() + '\n';
                                i++;
                            }

                            response.StatusCode = (int)HttpStatusCode.OK;
                            response.Message = "The deck has cards, the response contains these";
                            break;
                        }

                        response.ResponseContent = JsonConvert.SerializeObject(deck, Formatting.Indented);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.Message = "The deck has cards, the response contains these";
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.Url == "/battles")
            {
                switch (request.Method)
                {
                    case "POST":
                        Console.WriteLine("Battle request");
                        //1. get player cards, store it in player variable
                        //2. pass this player variable to battlehandler to store in queue
                        //3. when 2 players are in queue -> battle
                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.Message = "Access token is missing or invalid";
                            break;
                        }
                        string header = request.Authorization;
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
                            response.Message = "The request was fine, but the deck doesn't have any cards";
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
                            }
                        }
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.Message = "The battle has been carried out successfully.";
                        break;
                    default:
                        break;
                }
            }
            else if (request.Url == "/stats")
            {
                switch (request.Method)
                {
                    case "GET":
                        //Retrieves the stats for the requesting user.
                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.Message = "Access token is missing or invalid";
                            break;
                        }
                        string header = request.Authorization;
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
                                response.Message = "User not found";
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

                                response.ResponseContent = JsonConvert.SerializeObject(stats);

                                response.StatusCode = (int)HttpStatusCode.OK;
                                response.Message = "The stats could be retrieved successfully.";
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            else if (request.Url == "/score")
            {
                switch (request.Method)
                {
                    case "GET":
                        //Lists all users ordered by elo
                        if (!Authorize(request))
                        {
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            response.Message = "Access token is missing or invalid";
                            break;
                        }
                        IDbCommand command = connection.CreateCommand();
                        command.CommandText = "SELECT name, elo FROM users ORDER BY elo DESC";

                        NpgsqlCommand c = command as NpgsqlCommand;

                        List<string[]> scoreboard = new List<string[]>();
                        using (IDataReader reader = c.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string[] new_entry = { reader["name"].ToString(), reader["elo"].ToString() };
                                if (reader["name"].ToString() == "")
                                {
                                    new_entry[0] = "anonymous";
                                }
                                scoreboard.Add(new_entry);
                            }
                        }
                        response.ResponseContent = JsonConvert.SerializeObject(scoreboard, Formatting.Indented);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.Message = "The stats could be retrieved successfully.";
                        break;
                    default:
                        break;
                }
            }

            // Send a response to the client
            string responseString = "";
            responseString = request.Version + " "
                + response.StatusCode + " "
                + "\r\nContent-Length: "
                + (response.ResponseContent.Length + response.Message.Length).ToString()
                + "\r\n\r\n" + response.Message
                + "\r\n\r\n" + response.ResponseContent;

            byte[] responseBytes = Encoding.ASCII.GetBytes(responseString);
            stream.Write(responseBytes, 0, responseBytes.Length);

            // Close the client connection
            client.Close();
        }

        static public void HandleBattles(BattleHandler bh)
        {
            while (true)
            {
                Thread.Sleep(500);
                if (bh.WaitingPlayers.Count >= 2)
                {
                    bh.Battle();
                }
                Console.Write(bh.WaitingPlayers.Count);
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

        static bool Authorize(HTTPRequest request)
        {
            try
            {
                string authToken = request.Authorization;
                if (authToken.StartsWith("B"))
                {
                    return true;
                }
                else
                    return false;
            }
            catch(NullReferenceException e){
                return false;
            }
            
        }

        static bool Authorize(HTTPRequest request, string username)
        {
            string authToken = request.Authorization;
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

        static bool AuthorizeAdmin(HTTPRequest request, bool checkAdmin)
        {
            string authToken = request.Authorization;
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