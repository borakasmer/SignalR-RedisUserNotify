using Microsoft.AspNet.SignalR;
using ServiceStack.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Mvc;

namespace SignalRUserNotify.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home
        public ActionResult Index()
        {
            Session.Add("UserName", "Bora Kaşmer");
            ViewBag.SessionID = Session.SessionID;
            return View();
        }
    }

    public class Session : Hub
    {
        static RedisEndpoint conf = new RedisEndpoint() { Host = "127.0.0.1", Port = 6379 };
        public override async Task OnConnected()
        {
            var isFromConsole = Context.QueryString["console"];
            var sessionID = Context.QueryString["sessionID"];
            Configuration config = WebConfigurationManager.OpenWebConfiguration("~/Web.Config");
            SessionStateSection section = (SessionStateSection)config.GetSection("system.web/sessionState");
            try
            {
                using (IRedisClient client = new RedisClient(conf))
                {
                    if (isFromConsole == "0" && !client.ContainsKey(Context.ConnectionId)) // client(Redis)'de ilgili connection yok..
                    {
                        //Eğer Önceden eklenmiş Listede Yok ise
                        //Liste yapmadan amaç önceden bu kayıt var ve Sayfayı Refresh yapmış ise önceki connectionID'sini Redisden yenileyebilmek.
                        if (!PersonList.HasSession(sessionID))
                        {
                            DateTime sessionExpireDate = DateTime.Now.AddMinutes(section.Timeout.TotalMinutes);
                            client.Set(Context.ConnectionId, sessionExpireDate);
                            var _clientData = new ClientData() { ClientConnectionID = Context.ConnectionId, ClientSessionTime = sessionExpireDate };
                            PersonList.Add(sessionID, _clientData);
                        }
                        else
                        {
                            //Eğer Önceden eklenmiş Listede Var İse ama redis'de yok ise kısa bir süre önce sayfadan ayrılınmış demektir.
                            //PersonList'e eklenen süre, session'ın süresini geçmiş ise redis'e  atılmadan yani client'a gönderilmeden ilgili client function tetiklenir.
                            DateTime sessionTime = PersonList.GetSession(sessionID).ClientSessionTime;
                            var seconds = sessionTime.Subtract(DateTime.Now).TotalSeconds;
                            if(seconds>30)
                                client.Set(Context.ConnectionId, PersonList.GetSession(sessionID).ClientSessionTime);
                            else
                            {
                                //string message = seconds < 30 ? "Session Süreniz Dolmuştur" : "Session Sürenizin Dolmasına çok az kalmıştır";
                                string message = seconds <= 0 ? "Session Süreniz Dolmuştur" : "Session Sürenizin Dolmasına çok az kalmıştır";
                                await Clients.Caller.notifyUser(message);
                            }
                        }
                        PersonList.ClearExpiredPersonList(); //Süresi dolanlar listeden kaldırılır.
                    }
                    await Clients.Caller.notifySession(Context.ConnectionId + ":" + DateTime.Now.AddMinutes(section.Timeout.TotalMinutes));
                }
            }
            catch (Exception ex)
            {
                int i = 0;
            }
        }
        public override Task OnDisconnected(bool stopCalled)
        {
            var isFromConsole = Context.QueryString["console"];
            using (IRedisClient client = new RedisClient(conf))
            {
                if (isFromConsole == "0")
                {
                    var sessionID = Context.QueryString["sessionID"];
                    client.Remove(Context.ConnectionId);
                    //PersonList.Remove(sessionID);
                }
                return base.OnDisconnected(stopCalled);
            } 
        }

        public async Task notifyClient(string connectionID,string message)
        {
            await Clients.Client(connectionID).notifyUser(message);
        }
    }
    public static class PersonList
    {
        static private Dictionary<string, ClientData> _personList = new Dictionary<string, ClientData>();

        private static Dictionary<string, ClientData> _PersonList
        {
            get
            {
                return _personList;
            }
        }
        public static void Add(string SessionID, ClientData _clientData)
        {
            if (!_PersonList.ContainsValue(_clientData))
            {
                _PersonList[SessionID] = _clientData;
            }
        }
        public static void Remove(string SessionID)
        {
            _PersonList.Remove(SessionID);
        }
        public static bool HasSession(string SessionID)
        {
            return _PersonList.Any(cl => cl.Key.ToUpper() == SessionID.ToUpper());
        }
        public static string GetSessionByConnectionID(string ConnectionID)
        {
            return _PersonList.FirstOrDefault(cl => cl.Value.ClientConnectionID == ConnectionID).Key;
        }
        public static ClientData GetSession(string SessionID)
        {
            return _PersonList[SessionID];
        }
        public static void ClearExpiredPersonList()
        {
            _PersonList.Where(per => per.Value.ClientSessionTime.Subtract(DateTime.Now).TotalSeconds < 0).ToList()
                .ForEach(key => _PersonList.Remove(key.Key));
        }
    }
    public class ClientData
    {
        public DateTime ClientSessionTime { get; set; }
        public string ClientConnectionID { get; set; }
    }
}