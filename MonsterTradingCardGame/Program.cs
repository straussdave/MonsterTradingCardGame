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
using System.Text.Json;
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
                            Console.WriteLine("unsupported HTTP verb");
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
                            if(!Authorize(request, username))
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
                            Console.WriteLine("unsupported HTTP verb");
                            break;
                    }
                }
                else if(request.RawUrl == "/sessions") //Login with existing user
                {
                    if(request.HttpMethod == "POST")
                    {
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
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
                
        }
    }
}