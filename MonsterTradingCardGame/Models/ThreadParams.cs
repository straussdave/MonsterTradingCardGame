using System.Data;
using System.Net;

namespace MonsterTradingCardGame.Models
{
    class ContextConnectionParams
    {
        public HttpListenerContext Context { get; set; }
        public IDbConnection Connection { get; set; }
    }
}
