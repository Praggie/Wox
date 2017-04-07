using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using Firebase.Database;
using Firebase.Database.Streaming;
using FireSharp.EventStreaming;
using Wox.Core;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Image;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.ViewModel;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox
{
    public partial class App : IDisposable, ISingleInstanceApp
    {
        public static PublicAPIInstance API { get; private set; }
        private const string Unique = "Wox_Unique_Application_Mutex";
        private static bool _disposed;
        private Settings _settings;
        private MainViewModel _mainVM;
        private SettingWindowViewModel _settingsVM;
        private const string BasePath = "https://alexa-fd19b.firebaseio.com/";
        private const string FirebaseSecret = "SS3jBGy29CD5CY0BXkg5GAzfOXSQNoHLKUgeN0vF";
        private static FirebaseClient _client;
        private static DateTime _appStartDateTime;

        [STAThread]
        public static void Main()
        {
            RegisterAppDomainExceptions();

            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                using (var application = new App())
                {
                    application.InitializeComponent();
                    application.Run();
                }
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            Stopwatch.Normal("|App.OnStartup|Startup cost", () =>
            {
                Log.Info("|App.OnStartup|Begin Wox startup ----------------------------------------------------");
                RegisterDispatcherUnhandledException();


               // IFirebaseClient _client = new FirebaseClient(config);

                            var _client = new FirebaseClient(
              BasePath,
              new FirebaseOptions
              {
                  AuthTokenAsyncFactory = () => Task.FromResult(FirebaseSecret)
              });

                RegisterFirebaseCallback(_client);


                ImageLoader.Initialize();
                Alphabet.Initialize();

                _settingsVM = new SettingWindowViewModel();
                _settings = _settingsVM.Settings;

                PluginManager.LoadPlugins(_settings.PluginSettings);
                _mainVM = new MainViewModel(_settings);
                var window = new MainWindow(_settings, _mainVM);
                API = new PublicAPIInstance(_settingsVM, _mainVM);
                PluginManager.InitializePlugins(API);

                Current.MainWindow = window;
                Current.MainWindow.Title = Constant.Wox;

                // happlebao todo temp fix for instance code logic
                // load plugin before change language, because plugin language also needs be changed
                InternationalizationManager.Instance.Settings = _settings;
                InternationalizationManager.Instance.ChangeLanguage(_settings.Language);
                // main windows needs initialized before theme change because of blur settigns
                ThemeManager.Instance.Settings = _settings;
                ThemeManager.Instance.ChangeTheme(_settings.Theme);

                Http.Proxy = _settings.Proxy;

                RegisterExitEvents();

                AutoStartup();
                AutoUpdates();

                _mainVM.MainWindowVisibility = _settings.HideOnStartup ? Visibility.Hidden : Visibility.Visible;
                Log.Info("|App.OnStartup|End Wox startup ----------------------------------------------------  ");
            });
        }

        private static async Task RegisterFirebaseCallback(FirebaseClient _client)
        {
            // await _client.DeleteAsync("Commands/command1");
            _appStartDateTime = DateTime.Now;
            
                var observable = _client
      .Child("Commands")
      .AsObservable<Command>()
      .Subscribe(OnNext);

          
        }

        private static void OnNext(FirebaseEvent<Command> firebaseEvent)
        {
            if (firebaseEvent.EventType == FirebaseEventType.InsertOrUpdate)
            {
                var command = firebaseEvent.Object;
                if (command != null)
                {
                    ExecuteQuery(command);
                }
            }
        }

        public static void ExecuteQuery(Command command)
        {
            Query query = PluginManager.QueryInit(command.name);

            if (query != null)
            {
                // handle the exclusiveness of plugin using action keyword
                
                var plugins = PluginManager.ValidPluginsForQuery(query);
                if (plugins.Count > 0)
                {
                    plugins = plugins.Where(x => x.Metadata.Name == command.pluginname).ToList();
                    var allresults = new List<Result>();
                    Task.Run(() =>
                    {
                        Parallel.ForEach(plugins, plugin =>
                        {
                            var results = PluginManager.QueryForPlugin(plugin, query);

                            allresults.AddRange(results);
                        });
                    }).Wait();
                        
                    if (allresults.Any())
                    {
                        var firstOrDefault = allresults.FirstOrDefault();
                        firstOrDefault?.Action.BeginInvoke(null, null, null);
                    }
                    Console.Write(allresults.ToString());
                }
            }
        }


        private void AutoStartup()
        {
            if (_settings.StartWoxOnSystemStartup)
            {
                if (!SettingWindow.StartupSet())
                {
                    SettingWindow.SetStartup();
                }
            }
        }

        [Conditional("RELEASE")]
        private void AutoUpdates()
        {
            Task.Run(async () =>
            {
                if (_settings.AutoUpdates)
                {
                    // check udpate every 5 hours
                    var timer = new Timer(1000 * 60 * 60 * 5);
                    timer.Elapsed += async (s, e) =>
                    {
                        await Updater.UpdateApp();
                    };
                    timer.Start();

                    // check updates on startup
                    await Updater.UpdateApp();
                }
            });
        }

        private void RegisterExitEvents()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Dispose();
            Current.Exit += (s, e) => Dispose();
            Current.SessionEnding += (s, e) => Dispose();
        }

        /// <summary>
        /// let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private void RegisterDispatcherUnhandledException()
        {
            DispatcherUnhandledException += ErrorReporting.DispatcherUnhandledException;
        }


        /// <summary>
        /// let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private static void RegisterAppDomainExceptions()
        {
            AppDomain.CurrentDomain.UnhandledException += ErrorReporting.UnhandledExceptionHandle;
            AppDomain.CurrentDomain.FirstChanceException +=
                (s, e) => { Log.Exception("|App.RegisterAppDomainExceptions|First Chance Exception:", e.Exception); };
        }

        public void Dispose()
        {
            // if sessionending is called, exit proverbially be called when log off / shutdown
            // but if sessionending is not called, exit won't be called when log off / shutdown
            if (!_disposed)
            {
                _mainVM.Save();
                _settingsVM.Save();

                PluginManager.Save();
                ImageLoader.Save();
                Alphabet.Save();

                _disposed = true;
            }
        }

        public void OnSecondAppStarted()
        {
            Current.MainWindow.Visibility = Visibility.Visible;
        }
    }

    public class Command
    {
        public string name { get; set; }
        public string device { get; set; }
        public string pluginname { get; set; }
    }
}