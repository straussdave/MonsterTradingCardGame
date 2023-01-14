using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonsterTradingCardGame.Models
{
    internal class HTTPRequest
    {

        public HTTPRequest(string req) 
        {
            var lines = req.Split('\n');

            var line = lines[0];
            var lineSplit = line.Split(' ');
            this.Method = lineSplit[0];
            this.Url = lineSplit[1];
            this.Version = lineSplit[2];

            line = lines[1];
            lineSplit = line.Split(' ');
            this.Host = lineSplit[1];

            line = lines[5];
            lineSplit = line.Split(':');
            this.Authorization = lineSplit[1].Substring(1);

            line = lines[6];
            lineSplit = line.Split(' ');
            this.ContentLength = Int32.Parse(lineSplit[1]);

            line = lines[8];
            this.Body = line;
        }

        public string Method;
        public string Url;
        public string Version;
        public string Host;
        public string UserAgent;
        public string Accept;
        public string ContentType;
        public string Authorization;
        public int ContentLength;
        public string Body;
    }
}
