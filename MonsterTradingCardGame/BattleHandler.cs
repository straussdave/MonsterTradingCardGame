using MonsterTradingCardGame.Models;
using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MonsterTradingCardGame
{
    public class BattleHandler
    {
        public IDbConnection connection;

        public BattleHandler(IDbConnection connection)
        {
            this.connection = connection;
        }

        public HTTPResponse handleRequest(HTTPRequest request, BattleManager battleManager, object _locker)
        {
            HTTPResponse response = new HTTPResponse();
            if (request.Url == "/battles")
            {
                switch (request.Method)
                {
                    case "POST":
                        response = HandleBattle(request, battleManager, _locker);
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
                        response = GetStats(request);
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
                        response = GetScoreboard(request);
                        break;
                    default:
                        break;
                }
            }
            return response;
        }

        public HTTPResponse HandleBattle(HTTPRequest request, BattleManager battleManager, object _locker)
        {
            //1. get player cards, store it in player variable
            //2. pass this player variable to battlemanager to store in queue
            //3. wait for battle to start/finish
            HTTPResponse response = new HTTPResponse();
            string header = request.Authorization;
            int startOfToken = header.IndexOf("Bearer ") + 7;
            string user = header.Substring(startOfToken, header.IndexOf("-") - startOfToken);
            bool hasCards = false;
            List<Card> deck = new List<Card>();
            IDbCommand command = null;
            int userid;

            lock (_locker)
            {
                command = connection.CreateCommand();
                command.CommandText = "SELECT uid FROM users WHERE username = @usernameParam";
                NpgsqlCommand c = command as NpgsqlCommand;

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
            }

            if (!hasCards)
            {
                response.StatusCode = (int)HttpStatusCode.NoContent;
                response.Message = "The request was fine, but the deck doesn't have any cards";
                return response;
            }
            Player player = new Player();
            List<string> log = new List<string>();
            player.Name = user;
            player.Deck = deck;
            bool found = false;
            int battleId = battleManager.Enqueue(player);
            Console.WriteLine("Enqueued Player");
            while (true)
            {
                Thread.Sleep(1000);

                foreach (List<string> battleLog in battleManager.BattleHistory)
                {
                    if (battleLog.ElementAt(0) == battleId.ToString())
                    {
                        found = true;
                        response.ResponseContent = JsonConvert.SerializeObject(battleLog, Formatting.Indented);
                        log = battleLog;
                    }
                }
                if (found)
                    break;
            }

            if (log.Contains("result: draw")) //draw
            {
                Console.WriteLine("result is draw");
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Message = "The battle has been carried out successfully.";
                return response;
            }
            else if (log.Contains("result: " + user)) //win
            {
                lock (_locker)
                {
                    NpgsqlCommand c = command as NpgsqlCommand;
                    c.CommandText = "UPDATE users SET elo = elo + 3, wins = wins + 1 WHERE uid = @useridParam";
                    IDbDataParameter useridParam = c.CreateParameter();
                    useridParam.DbType = DbType.Int32;
                    useridParam.ParameterName = "useridParam";
                    c.Parameters.Add(useridParam);
                    c.Parameters["useridParam"].Value = userid;

                    c.ExecuteNonQuery();
                }
            }
            else //lose
            {
                lock (_locker)
                {
                    NpgsqlCommand c = command as NpgsqlCommand;
                    c.CommandText = "UPDATE users SET elo = elo - 5, losses = losses + 1 WHERE uid = @useridParam";
                    IDbDataParameter useridParam = c.CreateParameter();
                    useridParam.DbType = DbType.Int32;
                    useridParam.ParameterName = "useridParam";
                    c.Parameters.Add(useridParam);
                    c.Parameters["useridParam"].Value = userid;

                    c.ExecuteNonQuery();
                }
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            response.Message = "The battle has been carried out successfully.";
            return response;
        }

        public HTTPResponse GetStats(HTTPRequest request)
        {
            HTTPResponse response = new HTTPResponse();
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
                    return response;
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

                    return response;
                }
            }
        }

        public HTTPResponse GetScoreboard(HTTPRequest request)
        {
            HTTPResponse response = new HTTPResponse();
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
            response.Message = "The scoreboard could be retrieved successfully.";
            return response;
        }


    }
}
