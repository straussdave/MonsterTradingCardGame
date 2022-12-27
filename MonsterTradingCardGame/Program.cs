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

namespace HttpServer
{
    class Program
    {
        static void Main()
        {
            //string cs = "Host=localhost;Username=postgres;Password=011001;Database=postgres";
            //NpgsqlConnection con = new NpgsqlConnection(cs);
            //con.Open();

            //string sql = "INSERT INTO test2 values (3)";

            //NpgsqlCommand cmd = new NpgsqlCommand(sql, con);

            //cmd.ExecuteNonQuery();
            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();
            

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

                    // Determine the HTTP verb of the request
                    string responseString = "";
                    if (request.RawUrl == "/users")
                    {
                        switch (request.HttpMethod)
                        {
                            case "GET":
                                Console.WriteLine("GET users");
                                responseString = "GET users";
                                break;
                            case "POST":
                                //get username and password from request body
                                //insert new user into db

                                string userJson = GetRequestData(request);
                                User user = JsonConvert.DeserializeObject<User>(userJson);

                                Console.WriteLine("new user: " + user.username + " " + user.password);

                                IDbCommand command = connection.CreateCommand();
                                command.CommandText = @"insert into users 
                                       (username, password)
                                       values
                                       (@username, @password)";

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

                                string salt = SecurityHelper.GenerateSalt(70);
                                string pwdHashed = SecurityHelper.HashPassword(user.password, salt, 10101, 70);

                                c.Parameters["username"].Value = user.username;
                                c.Parameters["password"].Value = pwdHashed;

                                command.ExecuteNonQuery();
                                Console.WriteLine("POST users");
                                responseString = "POST users";
                                break;
                            default:
                                // Return an error for other HTTP verbs
                                Console.WriteLine("unsupported HTTP verb");
                                responseString = "Unsupported HTTP verb.";
                                break;
                        }
                    }
                    else if (request.RawUrl.StartsWith("/users/"))
                    {
                        string user = request.RawUrl.Substring(7);
                        switch (request.HttpMethod)
                        {
                            case "GET":
                                Console.WriteLine("GET " + user);
                                responseString = "GET " + user;
                                break;
                            case "POST":
                                Console.WriteLine("POST " + user);
                                responseString = "POST " + user;

                                break;
                            default:
                                // Return an error for other HTTP verbs
                                Console.WriteLine("unsupported HTTP verb");
                                responseString = "Unsupported HTTP verb.";
                                break;
                        }
                    }

                    // Set the response content and content type
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/plain";

                    // Send the response to the client
                    Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);

                    // Close the output stream and release resources
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

        public class SecurityHelper
        {
            public static string GenerateSalt(int nSalt)
            {
                byte[] saltBytes = new byte[nSalt];

                using (RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider())
                {
                    provider.GetNonZeroBytes(saltBytes);
                }

                return Convert.ToBase64String(saltBytes);
            }

            public static string HashPassword(string password, string salt, int nIterations, int nHash)
            {
                byte[] saltBytes = Convert.FromBase64String(salt);

                using (Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, saltBytes, nIterations))
                {
                    return Convert.ToBase64String(rfc2898DeriveBytes.GetBytes(nHash));
                }
            }
        }
    }
}