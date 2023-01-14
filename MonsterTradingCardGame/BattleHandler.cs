using MonsterTradingCardGame.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MonsterTradingCardGame
{
    internal class BattleHandler
    {
        public Queue<Player> WaitingPlayers = new Queue<Player>();
        public List<string[]> BattleHistory = new List<string[]>();
        public List<int> FinishedBattles = new List<int>();
        public int Enqueue(Player player)
        {//will return the id of the battle which the user will join
            WaitingPlayers.Enqueue(player);
            return BattleHistory.Count;
        }
        public void Battle()
        {
            Player player1 = WaitingPlayers.Dequeue();
            Player player2 = WaitingPlayers.Dequeue();
            Player winner = player1;
            Player loser = player2;

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

            int player1RandomCard = random.Next(player1Deck.Count);
            Card player1FightingCard = player1Deck[player1RandomCard];

            int player2RandomCard = random.Next(player2Deck.Count);
            Card player2FightingCard = player2Deck[player2RandomCard];

            

            if(player1FightingCard.Damage > player2FightingCard.Damage)
            {
                winner = player1;
                loser = player2;
                player1Deck.Add(player2FightingCard);
                player2Deck.RemoveAt(player2RandomCard);
            }
            else if(player2FightingCard.Damage > player1FightingCard.Damage)
            {
                winner = player2;
                loser = player1;
                player2Deck.Add(player1FightingCard);
                player1Deck.RemoveAt(player1RandomCard);
            }



            int battleId = BattleHistory.Count;
            string[] new_entry = { battleId.ToString(), winner.Name , loser.Name};
            FinishedBattles.Add(battleId);
            BattleHistory.Add(new_entry);
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
            return card.Name.Substring(element.Length);
        }

    }
}
