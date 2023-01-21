using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonsterTradingCardGame.Models
{
    public class HTTPResponse
    {
        public HTTPResponse()
        {
            this.Message = "";
            this.ResponseContent = "";
            this.StatusCode = 500;
            this.ContentType = "";
        }

        public string Message;
        public int StatusCode;
        public string ResponseContent;
        public string ContentType;
    }
}
