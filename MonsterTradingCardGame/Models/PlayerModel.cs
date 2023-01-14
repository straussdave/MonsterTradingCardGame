using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonsterTradingCardGame.Models
{
    internal class Player
    {
        public string Name { get; set; }
        public List<Card> Deck { get; set; }
    }
}
