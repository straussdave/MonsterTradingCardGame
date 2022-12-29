using System.Text.Json.Serialization;

namespace MonsterTradingCardGame.Models
{
    internal class UserData
    {
        //The variables start with upper case to get the correct format in JSON output
        public string Name { get; set; } 
        public string Bio { get; set; }
        public string Image { get; set; }

    }
}
