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
            string[] lines = req.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            int i = 0;
            foreach(var l in lines)
            {
                if(i == 0)
                {
                    var lineSplit = l.Split(' ');
                    this.Method = lineSplit[0];
                    this.Url = lineSplit[1];
                    this.QueryString = "";
                    if (this.Url.Contains("?"))
                    {
                        var urlSplit = this.Url.Split('?');
                        this.Url = urlSplit[0];
                        this.QueryString = urlSplit[1];
                    }
                    this.Version = lineSplit[2];
                }
                else
                {
                    if (l.Contains("Host"))
                    {
                        var lineSplit = l.Split(' ');
                        this.Host = lineSplit[1];
                    }
                    else if (l.Contains("User-Agent"))
                    {
                        var lineSplit = l.Split(' ');
                        this.UserAgent = lineSplit[1];
                    }
                    else if (l.Contains("Accept"))
                    {
                        var lineSplit = l.Split(' ');
                        this.Accept = lineSplit[1];
                    }
                    else if (l.Contains("Content-Type"))
                    {
                        var lineSplit = l.Split(' ');
                        this.ContentType = lineSplit[1];
                    }
                    else if (l.Contains("Authorization"))
                    {
                        var lineSplit = l.Split(':');
                        this.Authorization = lineSplit[1].Substring(1);
                    }
                    else if (l.Contains("Content-Length"))
                    {
                        var lineSplit = l.Split(' ');
                        this.ContentLength = Int32.Parse(lineSplit[1]);
                    }
                    else if (l != "")
                    {
                        this.Body = l;
                    }
                }
                i++;
            }
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
        public string QueryString;
    }
}
