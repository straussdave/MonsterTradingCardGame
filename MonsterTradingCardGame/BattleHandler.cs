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
            int battleId = BattleHistory.Count;
            string[] new_entry = { battleId.ToString(), winner.Name };
            FinishedBattles.Add(battleId);
            BattleHistory.Add(new_entry);
        }
    }
}
