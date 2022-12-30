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

namespace HttpServer
{
    class Program
    {
        static void Main()
        {
            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();

            Guid myuuid = Guid.NewGuid();
            string myuuidAsString = myuuid.ToString();

            Console.WriteLine("Your UUID is: " + myuuidAsString);

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://+:10001/");
            listener.Start();
            Console.WriteLine("Listening for requests...");


            while (true)
            {
                // Wait for a request to be made
                HttpListenerContext context = listener.GetContext();

                // Get the request and response objects
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

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
                                if (!(reader.Read()))
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
                                if (!AuthorizeAdmin(request))
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
                            command.CommandText = "SELECT COUNT(*) FROM cards";
                            NpgsqlCommand c = command as NpgsqlCommand;

                            int packagenr = -1;

                            object result = command.ExecuteScalar();
                            int rowCount = Convert.ToInt32(result);
                            if (rowCount > 0)
                            {
                                //There is at least 1 card
                                c.CommandText = "SELECT MAX(packagenr) FROM cards";
                                result = command.ExecuteScalar();
                                packagenr = Convert.ToInt32(result) + 1;

                            }
                            else
                            {
                                // There is no card yet
                                packagenr = 0;
                            }

                            c.CommandText = "INSERT INTO cards (id, name, damage, packagenr) " +
                                            "VALUES (@id, @name, @damage, @packagenr)";

                            IDbDataParameter id = c.CreateParameter();
                            id.DbType = DbType.String;
                            id.ParameterName = "id";
                            c.Parameters.Add(id);

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

                            int i = 0;
                            foreach (Card card in cards)
                            {
                                c.Parameters["id"].Value = cards[i].Id;
                                c.Parameters["name"].Value = cards[i].Name;
                                c.Parameters["damage"].Value = cards[i].Damage;
                                c.Parameters["packagenr"].Value = packagenr;

                                try
                                {
                                    c.ExecuteNonQuery();
                                }
                                catch
                                {
                                    response.StatusCode = (int)HttpStatusCode.Conflict;
                                    response.StatusDescription = "At least one card in the packages already exists";
                                    break;
                                }
                                i++;

                                response.StatusCode = (int)HttpStatusCode.Created;
                                response.StatusDescription = "Package and cards successfully created";
                                break;
                            }
                            break;
                        default:
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                            response.StatusDescription = "Unsupported HTTP Verb";
                            break;
                    }
                }
                else if (request.RawUrl == "/transactions/packages/")
                {
                    switch (request.HttpMethod)
                    {
                        case "POST":
                            //Buys a card package with the money of the provided user

                            break;
                        default:

                            break;
                    }
                }

                //Set the response content and content type
                byte[] buffer = Encoding.UTF8.GetBytes(responseContent);
                response.ContentLength64 = buffer.Length;

                // Send the response to the client
                Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);

                output.Close();
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

        static bool Authorize(HttpListenerRequest request, string username)
        {
            string authToken = request.Headers["Authorization"];
            if (authToken != null && authToken.StartsWith("Bearer"))
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

        static bool AuthorizeAdmin(HttpListenerRequest request)
        {
            string authToken = request.Headers["Authorization"];
            if (authToken != null && authToken.StartsWith("Bearer"))
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