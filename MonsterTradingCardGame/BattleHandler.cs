using Microsoft.Extensions.Logging;
using MonsterTradingCardGame.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MonsterTradingCardGame
{
    internal class BattleHandler
    {
        public Queue<Player> WaitingPlayers = new Queue<Player>();
        public List<List<string>> BattleHistory = new List<List<string>>();
        public int Enqueue(Player player)
        {//will return the id of the battle which the user will join
            WaitingPlayers.Enqueue(player);
            return BattleHistory.Count;
        }
        public void Battle()
        {
            int roundNr = 0;
            List<string> battleLog = new List<string>
            {
                BattleHistory.Count.ToString()
            };
            Player player1 = WaitingPlayers.Dequeue();
            Player player2 = WaitingPlayers.Dequeue();

            List<Card> player1Deck = new List<Card>();
            List<Card> player2Deck = new List<Card>();
            foreach(Card card in player1.Deck)
            {
                player1Deck.Add(card);
            }
            foreach(Card card in player2.Deck)
            {
                player2Deck.Add(card);
            }

            var random = new Random();

            while(roundNr <= 100)
            {
                int player1Index = random.Next(player1Deck.Count);
                Card player1FightingCard = player1Deck[player1Index];

                int player2Index = random.Next(player2Deck.Count);
                Card player2FightingCard = player2Deck[player2Index];

                if (CalculateDamage(player1FightingCard, player2FightingCard) == player1FightingCard)
                {
                    string battleDescription =
                        "Round " + roundNr + ": "
                        + player1.Name + "'s card "
                        + player1FightingCard.Name
                        + "(" + player1FightingCard.Damage + " damage)"
                        + " won against " + player2.Name + "'s card "
                        + player2FightingCard.Name
                        + "(" + player2FightingCard.Damage + " damage)";
                    battleLog.Add(battleDescription);
                    player1Deck.Add(player2FightingCard);
                    player2Deck.RemoveAt(player2Index);
                }
                else if (CalculateDamage(player1FightingCard, player2FightingCard) == player2FightingCard)
                {
                    string battleDescription =
                        "Round " + roundNr + ": "
                        + player2.Name + "'s card "
                        + player2FightingCard.Name
                        + "(" + player2FightingCard.Damage + " damage)"
                        + " won against " + player1.Name + "'s card "
                        + player1FightingCard.Name
                        + "(" + player1FightingCard.Damage + " damage)";
                    battleLog.Add(battleDescription);
                    player2Deck.Add(player1FightingCard);
                    player1Deck.RemoveAt(player1Index);
                }
                roundNr++;

                if (player1Deck.Count == 0)
                {
                    Console.WriteLine("Battle finished");
                    battleLog.Add("result: " + player1.Name);
                    BattleHistory.Add(battleLog);
                    break;
                }
                else if (player2Deck.Count == 0)
                {
                    Console.WriteLine("Battle finished");
                    battleLog.Add("result: " + player2.Name);
                    BattleHistory.Add(battleLog);
                    break;
                }
                else if (roundNr >= 100)
                {
                    Console.WriteLine("Battle finished");
                    battleLog.Add("result: draw");
                    BattleHistory.Add(battleLog);
                    break;
                }
            }
        }

        public Card CalculateDamage(Card card1, Card card2)
        {
            string card1Element = GetElement(card1);
            string card2Element = GetElement(card2);

            string card1Type = GetType(card1, card1Element);
            string card2Type = GetType(card2, card2Element);

            switch (card1Type)
            {
                case "Goblin":
                    if(card2Type == "Dragon")
                    {
                        return card2;
                    }
                    break;
                case "Dragon":
                    if(card2Type == "Goblin")
                    {
                        return card1;
                    }
                    if(card2.Name == "FireElf")
                    {
                        return card2;
                    }
                    break;
                case "Wizzard":
                    if(card2Type == "Ork")
                    {
                        return card1;
                    }
                    break;
                case "Ork":
                    if (card2Type == "Wizzard")
                    {
                        return card2;
                    }
                    break;
                case "Kraken":
                    if (card2Type == "Spell")
                    {
                        return card1;
                    }
                    break;
                case "Knight":
                    if(card2.Name == "WaterSpell")
                    {
                        return card2;
                    }
                    break;
                case "Spell":
                    if(card1Element == "Water" && card2Type == "Knight")
                    {
                        return card1;
                    }
                    if(card2Type == "Kraken")
                    {
                        return card2;
                    }
                    break;
                case "Elf":
                    if(card1Element == "Fire" && card2Type == "Dragon")
                    {
                        return card1;
                    }
                    break;
                default:
                    break;
            }

            if(card1Type != "Spell" && card2Type != "Spell")
            {
                if(card1.Damage > card2.Damage)
                {
                    return card1;
                }
                else if(card2.Damage > card1.Damage)
                {
                    return card2;
                }
                else if(card1.Damage == card2.Damage)
                {
                    return null;
                }
            }
            if(card1Type == "Spell" || card2Type == "Spell")
            {
                switch (card1Type)
                {
                    case "Regular":
                        if(card2Type == "Regular")
                        {
                            if (card1.Damage > card2.Damage)
                            {
                                return card1;
                            }
                            else if (card2.Damage > card1.Damage)
                            {
                                return card2;
                            }
                            else if (card1.Damage == card2.Damage)
                            {
                                return null;
                            }
                        }
                        else if(card2Type == "Fire")
                        {
                            if (card1.Damage / 2 > card2.Damage * 2)
                            {
                                return card1;
                            }
                            else if (card2.Damage / 2 > card1.Damage * 2)
                            {
                                return card2;
                            }
                            else if (card1.Damage / 2 == card2.Damage * 2)
                            {
                                return null;
                            }
                        }
                        else if(card2Type == "Water")
                        {
                            if (card1.Damage * 2 > card2.Damage / 2)
                            {
                                return card1;
                            }
                            else if (card2.Damage * 2 > card1.Damage / 2)
                            {
                                return card2;
                            }
                            else if (card1.Damage * 2 == card2.Damage / 2)
                            {
                                return null;
                            }
                        }
                        break;
                    case "Water":
                        if (card2Type == "Regular")
                        {
                            if (card1.Damage / 2 > card2.Damage * 2)
                            {
                                return card1;
                            }
                            else if (card2.Damage / 2 > card1.Damage * 2)
                            {
                                return card2;
                            }
                            else if (card1.Damage / 2 == card2.Damage * 2)
                            {
                                return null;
                            }
                        }
                        else if (card2Type == "Fire")
                        {
                            if (card1.Damage * 2 > card2.Damage / 2)
                            {
                                return card1;
                            }
                            else if (card2.Damage * 2 > card1.Damage / 2)
                            {
                                return card2;
                            }
                            else if (card1.Damage * 2 == card2.Damage / 2)
                            {
                                return null;
                            }
                        }
                        else if (card2Type == "Water")
                        {
                            if (card1.Damage > card2.Damage)
                            {
                                return card1;
                            }
                            else if (card2.Damage > card1.Damage)
                            {
                                return card2;
                            }
                            else if (card1.Damage == card2.Damage)
                            {
                                return null;
                            }
                        }
                        break;
                    case "Fire":
                        if (card2Type == "Regular")
                        {
                            if (card1.Damage * 2 > card2.Damage / 2)
                            {
                                return card1;
                            }
                            else if (card2.Damage * 2 > card1.Damage / 2)
                            {
                                return card2;
                            }
                            else if (card1.Damage * 2 == card2.Damage / 2)
                            {
                                return null;
                            }
                        }
                        else if (card2Type == "Fire")
                        {
                            if (card1.Damage > card2.Damage)
                            {
                                return card1;
                            }
                            else if (card2.Damage > card1.Damage)
                            {
                                return card2;
                            }
                            else if (card1.Damage == card2.Damage)
                            {
                                return null;
                            }
                        }
                        else if (card2Type == "Water")
                        {
                            if (card1.Damage / 2 > card2.Damage * 2)
                            {
                                return card1;
                            }
                            else if (card2.Damage / 2 > card1.Damage * 2)
                            {
                                return card2;
                            }
                            else if (card1.Damage / 2 == card2.Damage * 2)
                            {
                                return null;
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            return null;
        }

        public string GetElement(Card card)
        {
            if (card.Name.Contains("Fire"))
            {
                return "Fire";
            }
            else if (card.Name.Contains("Water"))
            {
                return "Water";
            }
            else if (card.Name.Contains("Regular"))
            {
                return "Regular";
            }
            else
            {
                return "Regular";
            }
        }

        public string GetType(Card card, string element)
        {
            if(!card.Name.Contains("Fire") || !card.Name.Contains("Water") || !card.Name.Contains("Regular"))
            {
                return card.Name;
            }
            else
            {
                return card.Name.Substring(element.Length);
            }
        }

    }
}
