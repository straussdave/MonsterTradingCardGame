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
using System.Diagnostics.Metrics;

namespace HttpServer
{
    class Program
    {
        static readonly object _locker = new object();

        static void Main()
        {
            IDbConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=011001;Database=postgres");
            connection.Open();
            UserHandler userHandler = new UserHandler(connection);
            PackageHandler packageHandler = new PackageHandler(connection);
            CardHandler cardHandler = new CardHandler(connection);
            BattleHandler battleHandler = new BattleHandler(connection);
            TcpListener listener = new TcpListener(IPAddress.Any, 10001);
            listener.Start();
            Console.WriteLine("Listening for incoming connections...");
            BattleManager bm = new BattleManager();
            Thread battleThread = new Thread(() => HandleBattles(bm));
            battleThread.Start();

            while (true)
            {
                // Wait for a client to connect
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("Client connected!");

                object args = new object[6] { bm, client, userHandler, packageHandler, cardHandler, battleHandler };
                Thread t = new Thread(() => HandleClient(args));
                t.Start();
            }
        }

        static void HandleClient(object args)
        {
            Array argArray = new object[6];
            argArray = (Array)args;

            TcpClient client = (TcpClient)argArray.GetValue(1);

            // Get the client's stream
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[4096];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string req = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            HTTPRequest request = new HTTPRequest(req);
            HTTPResponse response = new HTTPResponse();

            Console.Write(req);

            BattleManager battleManager = (BattleManager)argArray.GetValue(0);
            UserHandler userHandler = (UserHandler)argArray.GetValue(2);
            PackageHandler packageHandler = (PackageHandler)argArray.GetValue(3);
            CardHandler cardHandler = (CardHandler)argArray.GetValue(4);
            BattleHandler battleHandler = (BattleHandler)argArray.GetValue(5);

            // Determine the HTTP verb of the request
            if (request.Url == "/users" || request.Url == "/sessions")
            {
                response = userHandler.handleUser(request);
            }
            else if (request.Url.StartsWith("/users/"))
            {
                string username = request.Url.Substring(7);
                if (!Authorize(request, username))
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Message = "Access token is missing or invalid";
                }
                else
                {
                    response = userHandler.handleUser(request);
                }
            }
            else if (request.Url == "/packages")
            {
                switch (request.Method)
                {
                    case "POST":
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
                        response = packageHandler.handlePackage(request);
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            else if (request.Url == "/transactions/packages")
            {
                if (!Authorize(request))
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Message = "Access token is missing or invalid";
                }
                else
                {
                    response = packageHandler.handlePackage(request);
                }

            }
            else if (request.Url == "/cards" || request.Url == "/deck")
            {
                if (!Authorize(request))
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Message = "Access token is missing or invalid";
                }
                else
                {
                    response = cardHandler.handleCards(request);
                }
            }
            else if (request.Url == "/battles" || request.Url == "/stats" || request.Url == "/score")
            {
                if (!Authorize(request))
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Message = "Access token is missing or invalid";
                }
                else
                {
                    response = battleHandler.handleRequest(request, battleManager, _locker);
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

        static public void HandleBattles(BattleManager bm)
        {
            while (true)
            {
                Thread.Sleep(500);
                if (bm.WaitingPlayers.Count >= 2)
                {
                    Console.WriteLine("start battle");
                    bm.Battle();
                }
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
            catch (NullReferenceException)
            {
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