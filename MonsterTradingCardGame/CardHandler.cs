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
    public class CardHandler
    {
        public IDbConnection connection;
        public CardHandler(IDbConnection con)
        {
            this.connection = con;
        }

        public HTTPResponse handleCards(HTTPRequest request)
        {
            HTTPResponse response = new HTTPResponse();
            if (request.Url == "/cards")
            {
                switch (request.Method)
                {
                    case "GET":
                        response = getCards(request);
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
                        response = configureDeck(request);
                        break;
                    case "GET":
                        response = getDeck(request);
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Unsupported HTTP Verb";
                        break;
                }
            }
            return response;
        }

        public HTTPResponse getCards(HTTPRequest request)
        {
            HTTPResponse response = new HTTPResponse();
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
                return response;
            }
            response.ResponseContent = JsonConvert.SerializeObject(cards, Formatting.Indented);
            response.StatusCode = (int)HttpStatusCode.OK;
            response.Message = "The user has cards, the response contains these";
            return response;
        }

        public HTTPResponse configureDeck(HTTPRequest request)
        {
            //Send four card IDs to configure a new full deck. A failed request will not change the previously defined deck.
            HTTPResponse response = new HTTPResponse();
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
                return response;
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
                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                    response.Message = "At least one of the provided cards does not belong to the user or is not available.";
                    return response;
                }
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
            return response;
        }

        public HTTPResponse getDeck(HTTPRequest request)
        {
            HTTPResponse response = new HTTPResponse();
            //Returns the cards that are owned by the users and are put into the deck
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
                return response;
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
                return response;
            }

            response.ResponseContent = JsonConvert.SerializeObject(deck, Formatting.Indented);
            response.StatusCode = (int)HttpStatusCode.OK;
            response.Message = "The deck has cards, the response contains these";
            return response;
        }

    }
}
