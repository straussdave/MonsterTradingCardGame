using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using MonsterTradingCardGame.Models;
using Newtonsoft.Json;
using Npgsql;

namespace MonsterTradingCardGame
{
    public class UserHandler
    {
        public IDbConnection connection;

        public UserHandler(IDbConnection con)
        {
            this.connection = con;
        }

        public HTTPResponse handleUser(HTTPRequest request)
        {
            HTTPResponse response = new HTTPResponse();
            if (request.Url == "/users") //register user
            {
                if(request.Method == "POST")
                {
                    response = createUser(request);
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Message = "Unsupported HTTP Verb";
                }
            }
            else if (request.Url.StartsWith("/users/"))
            {
                switch (request.Method)
                {
                    case "GET": //Retrieve user data for the given username
                        response = GetUserData(request);
                        break;
                    case "PUT": //Updates user data for given username
                        response = UpdateUserData(request);
                        break;
                    case "DELETE":
                        response = DeleteUser(request);
                        break;
                    default:
                        // Return an error for other HTTP verbs
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if(request.Url == "/sessions") //Login with existing user
            {
                switch (request.Method)
                {
                    case "POST":
                        response = LoginUser(request);
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            return response;
        }

        private HTTPResponse createUser(HTTPRequest request)
        {
            HTTPResponse response = new HTTPResponse();
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
            return response;
        }

        private HTTPResponse GetUserData(HTTPRequest request)
        {
            string username = request.Url.Substring(7);
            UserData userData = new UserData();
            HTTPResponse response = new HTTPResponse();

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
                    return response;
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
            return response;   
        }
        private HTTPResponse UpdateUserData(HTTPRequest request)
        {
            string username = request.Url.Substring(7);
            string userDataJson = request.Body;
            UserData userData = JsonConvert.DeserializeObject<UserData>(userDataJson);
            HTTPResponse response = new HTTPResponse();

            IDbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM users WHERE username = @usernameParam";

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
                    response.Message = "User not found";
                    return response;
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
                return response;
            }
        }

        private HTTPResponse LoginUser(HTTPRequest request)
        {
            string userJson = request.Body;
            User user = JsonConvert.DeserializeObject<User>(userJson);
            HTTPResponse response = new HTTPResponse();

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
            return response;
        }

        private HTTPResponse DeleteUser(HTTPRequest request)
        { //requires username and password in body, only if credentials exist, the user gets deleted
            HTTPResponse response = new HTTPResponse();
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
                    reader.Close();
                    c.CommandText = "DELETE FROM users WHERE username = @usernameParam";
                    IDbDataParameter usernameParam = c.CreateParameter();
                    usernameParam.DbType = DbType.String;
                    usernameParam.ParameterName = "usernameParam";
                    c.Parameters.Add(usernameParam);
                    c.Parameters["usernameParam"].Value = user.username;
                    c.ExecuteNonQuery();
                    response.Message = "User succesfully deleted";
                    response.StatusCode = (int)HttpStatusCode.OK;
                }
                else
                {
                    // No match found
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Message = "Invalid username/password provided";
                }
            }
            return response;
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
    }
}
