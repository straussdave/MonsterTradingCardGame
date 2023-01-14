using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonsterTradingCardGame.Models
{
    internal class userStats
    {
        public string Name { get; set; }
        public string Elo { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
    }
}
