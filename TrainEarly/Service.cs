using Alchemy;
using Alchemy.Classes;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Configuration;
using System.Configuration.Install;
using System.Diagnostics;
using System.Reflection;
using System.ServiceProcess;
using TweetSharp;

namespace TrainEarly
{
    partial class Service : ServiceBase
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0] == "install")
                {
                    ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                }
                else if (args[0] == "uninstall")
                {
                    ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
                }
                else if (args[0] == "console")
                {
                    Service service = new Service();
                    service.OnStart(null);

                    System.Console.WriteLine("Press any key to disconnect");
                    System.Console.ReadLine();

                    service.OnStop();
                    System.Console.WriteLine("Press any key to quit");
                    System.Console.ReadLine();
                }
            }
            else
            {
                ServiceBase.Run(new Service());
            }
        }

        public Service()
        {
            InitializeComponent();

            TraceHelper.SetupTrace();

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Trace.TraceError("Unhandled Exception: {0}", e.ExceptionObject);
                TraceHelper.FlushLog();
                ExitCode = -1;
            };
        }

        private WebSocketClient _wsClient;
        private static readonly string _url = ConfigurationManager.AppSettings["train:url"];
        private static readonly string _twitterConsumerKey = ConfigurationManager.AppSettings["twitter:consumerKey"];
        private static readonly string _twitterConsumerSecret = ConfigurationManager.AppSettings["twitter:consumerSecret"];
        private static readonly string _twitterAccessToken = ConfigurationManager.AppSettings["twitter:accessToken"];
        private static readonly string _twitterAccessTokenSecret = ConfigurationManager.AppSettings["twitter:accessTokenSecret"];

        protected override void OnStart(string[] args)
        {
            string wsServer = ConfigurationManager.AppSettings["ws-server"];
            string stanox = ConfigurationManager.AppSettings["stanox"];
            if (string.IsNullOrEmpty(wsServer) || string.IsNullOrEmpty(stanox))
                throw new ArgumentNullException("", "both ws-server and stanox must be set in app.config");

            Trace.TraceInformation("Connecting to server on {0}", wsServer);
            _wsClient = new WebSocketClient(wsServer)
            {
                OnReceive = OnReceive
            };
            _wsClient.Connect();
            Trace.TraceInformation("Subscribing to stanox {0}", stanox);
            _wsClient.Send(string.Format("substanox:{0}", stanox));
        }

        private static void OnReceive(UserContext context)
        {
            var data = JsonConvert.DeserializeObject<dynamic>(context.DataFrame.ToString());
            var response = data.Response[0];

            if (Convert.ToByte((string)response.header.msg_type) == 3 && ((string)response.body.event_type).Equals("DEPARTURE", StringComparison.InvariantCultureIgnoreCase))
            {
                DateTime expectedTs = UnixTsToDateTime(double.Parse((string)response.body.gbtt_timestamp));
                DateTime? actualTs = null;
                // actual time stamp property name seems to get corrupted
                // so just search for anything containing "act"
                foreach (var value in response.body)
                {
                    if (value.Name.Contains("act"))
                    {
                        actualTs = UnixTsToDateTime(double.Parse((string)value.Value));
                        break;
                    }
                }

                if (actualTs.HasValue && actualTs < expectedTs)
                {
                    string tweet = string.Format("Train {0} expected to depart at {1:HH:mm:ss} actual departure {2:HH:mm:ss} - {3}{4}",
                        (string)response.body.train_id,
                        expectedTs,
                        actualTs,
                        _url,
                        (string)response.body.train_id);

                    Trace.TraceInformation(tweet);
                    Trace.Flush();

                    var server = new TwitterService(_twitterConsumerKey, _twitterConsumerSecret, _twitterAccessToken, _twitterAccessTokenSecret);
                    var status = server.SendTweet(new SendTweetOptions
                    {
                        Status = tweet
                    });
                }
            }
        }

        private static readonly DateTime _epoch = new DateTime(1970, 1, 1);

        private static DateTime UnixTsToDateTime(double timeStamp)
        {
            return _epoch.AddMilliseconds(timeStamp);
        }

        protected override void OnStop()
        {
            Trace.TraceInformation("Stopping Service");
            if (_wsClient != null)
            {
                _wsClient.Send("unsubstanox");
                _wsClient.Disconnect();
            }
        }      
    }

    [RunInstaller(true)]
    public class TrainEarlyServiceInstaller : Installer
    {
        public TrainEarlyServiceInstaller()
        {
            var processInstaller = new ServiceProcessInstaller();
            var serviceInstaller = new ServiceInstaller();

            //set the privileges
            processInstaller.Account = ServiceAccount.LocalSystem;

            serviceInstaller.DisplayName = "TrainNotifier Train Early Twitter Notifier";
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            //must be the same as what was set in Program's constructor
            serviceInstaller.ServiceName = "TrainNotiferTrainEarlyTwitterNofifier";
            this.Installers.Add(processInstaller);
            this.Installers.Add(serviceInstaller);
        }
    }
}
