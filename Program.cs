using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TL;
using MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TeamerTelegramParser
{
    class Program
    {
        private static string _apiId;
        private static string _apiHash;
        private static string _phoneNumber;
        private static List<NewsItem> _news;
        
        static async Task Main(string[] _)
        {
            string dbLink;
            using (var fs = File.OpenText(".config"))
            {
                dbLink = fs.ReadLine();
                _apiId = fs.ReadLine();
                _apiHash = fs.ReadLine();
                _phoneNumber = fs.ReadLine();
            }

            _news = new List<NewsItem>();
            var dbClient = new MongoClient(dbLink);
            using var client = new WTelegram.Client(Config);
            await client.LoginUserIfNeeded();
            
            while(true)
            {
                var chats = await client.Messages_GetAllChats(null);
                InputPeer peer = chats.chats[1169443045];
                var messages = await client.Messages_GetHistory(peer, 0, default, 0, 20, 0, 0, 0);
                var lastCalls = messages
                    .Messages
                    .OfType<Message>()
                    .Where(x => x.message.ToLower().Contains("#lastcall"));
                var news = messages
                    .Messages
                    .OfType<Message>()
                    .Except(lastCalls);

                var newsItems = new List<NewsItem>();
                foreach (var newsItem in news)
                {
                    var json = newsItem.media.ToJson();
                    var regex = new Regex("(?<=(\"url\": \")).*?(?=(\"))");
                    var link = regex.Match(json).Value;
                    var i = new NewsItem(link, newsItem.message, newsItem.id);
                    newsItems.Add(i);
                }

                var dist = newsItems.Except(_news).ToList();
                
                var collection = dbClient.GetDatabase("news").GetCollection<NewsItem>("news");
                foreach (var item in dist)
                {
                    await collection.InsertOneAsync(item);
                    _news.Add(item);
                }

                Thread.Sleep(5 * 60 * 1000);
            }
        }

        private static string Config(string what)
        {
            return what switch
            {
                "api_id" => _apiId,
                "api_hash" => _apiHash,
                "phone_number" => _phoneNumber,
                _ => null
            };
        }
    }

    class NewsItem
    {
        public string mediaLink;
        public string text;
        public int id;

        public NewsItem(string mediaLink, string text, int id)
        {
            this.mediaLink = mediaLink;
            this.text = text;
            this.id = id;
        }
    }
}