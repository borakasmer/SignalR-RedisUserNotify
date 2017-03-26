using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using ServiceStack.Redis;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Client;

namespace CheckSession
{
    class Program
    {
        static RedisEndpoint conf = new RedisEndpoint() { Host = "127.0.0.1", Port = 6379 };
        static Timer _Timer = new Timer(10000);
        static void Main(string[] args)
        {
            _Timer.Elapsed += CheckRedis;
            _Timer.Start();
            Console.ReadLine();
        }

        private static void CheckRedis(object sender, ElapsedEventArgs e)
        {
            _Timer.Stop();
            using (IRedisClient client = new RedisClient(conf))
            {
                var keys = client.SearchKeys("*");
                Console.Clear();
                foreach (string key in keys)
                {
                    DateTime sessionTime = client.Get<DateTime>(key);
                    var seconds = sessionTime.Subtract(DateTime.Now).TotalSeconds;
                    Console.WriteLine("key: "+key+" Sn: "+seconds);
                    if (seconds <= 30)
                    {
                        var connection = new HubConnection("http://localhost:2533/", "console=1");
                        //Make proxy to hub based on hub name on server
                        var myHub = connection.CreateHubProxy("Session");
                        connection.Start().Wait();
                        myHub.Invoke("notifyClient", new object[]{ key, "Session Süreniz 30sn Sonra Dolucak!."}).Wait();
                        client.Remove(key);
                    }
                }
                //Console.WriteLine("In TimerCallback: " + DateTime.Now);
                GC.Collect();
            }

            _Timer.Start();
        }
    }
}
