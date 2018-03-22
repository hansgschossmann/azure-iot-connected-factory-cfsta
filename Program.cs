using Mono.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CfStation
{
    using Opc.Ua;
    using Serilog;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Text;
    using static OpcApplicationConfiguration;
    using static StationNodeManager;

    public class Program
    {
        public static Serilog.Core.Logger Logger = null;

        /// <summary>
        /// Synchronous main method of the app.
        /// </summary>
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        /// <summary>
        /// Asynchronous part of the main method of the app.
        /// </summary>
        public async static Task MainAsync(string[] args)
        {
            var shouldShowHelp = false;

            // command line options
            Mono.Options.OptionSet options = new Mono.Options.OptionSet {
                // opc server configuration options
                { "lf|logfile=", $"the filename of the logfile to use.\nDefault: '{_logFileName}'", (string l) => _logFileName = l },
                { "ll|loglevel=", $"the loglevel to use (allowed: fatal, error, warn, info, debug, verbose).\nDefault: info", (string l) => {
                        List<string> logLevels = new List<string> {"fatal", "error", "warn", "info", "debug", "verbose"};
                        if (logLevels.Contains(l.ToLowerInvariant()))
                        {
                            _logLevel = l.ToLowerInvariant();
                        }
                        else
                        {
                            throw new OptionException("The loglevel must be one of: fatal, error, warn, info, debug, verbose", "loglevel");
                        }
                    }
                },
                { "pn|portnum=", $"the server port of the OPC server endpoint.\nDefault: {ServerPort}", (ushort p) => ServerPort = p },
                { "op|path=", $"the enpoint URL path part of the OPC server endpoint.\nDefault: '{ServerPath}'", (string a) => ServerPath = a },
                { "sh|stationhostname=", $"the fullqualified hostname of the station.\nDefault: {StationHostname}", (string a) => StationHostname = a },
                { "ga|generatealerts", $"the station should generate alerts.\nDefault: {GenerateAlerts}", g => GenerateAlerts = g != null},
                { "pc|powerconsumption=", $"the stations average power consumption in kW\nDefault:  {PowerConsumption} kW", (double d) => PowerConsumption = d },
                { "ct|cycletime=", $"the stations cycle time in seconds\nDefault:  {IdealCycleTimeDefault} sec", (ulong ul) => IdealCycleTimeDefault = ul * 1000 },
                { "lr|ldsreginterval=", $"the LDS(-ME) registration interval in ms. If 0, then the registration is disabled.\nDefault: {LdsRegistrationInterval}", (int i) => {
                        if (i >= 0)
                        {
                            LdsRegistrationInterval = i;
                        }
                        else
                        {
                            throw new OptionException("The ldsreginterval must be larger or equal 0.", "ldsreginterval");
                        }
                    }
                },
                { "st|opcstacktracemask=", $"the trace mask in hex digits for the OPC stack. See github OPC .NET stack for definitions.\nDefault: {OpcStackTraceMask:X}", (string s) => {
                        int i = OpcStackTraceMask;
                        if (int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out i) && i >= 0)
                        {
                            OpcStackTraceMask = i;
                        }
                        else
                        {
                            throw new Mono.Options.OptionException("The OPC stack trace mask must be larger or equal 0 and hexadecimal (no 0x prefix allowed).", "opcstacktracemask");
                        }
                    }
                },
                { "aa|autoacceptcerts", $"all certs are trusted when a connection is established.\nDefault: {AutoAcceptCerts}", a => AutoAcceptCerts = a != null },

                // trust own public cert option
                { "tm|trustmyself", $"the server certificate is put into the trusted certificate store automatically.\nDefault: {TrustMyself}", t => TrustMyself = t != null },

                { "ap|appcertstorepath=", $"the path where the own application cert should be stored\nDefault '{OpcOwnCertX509StorePathDefault}'", (string s) => OpcOwnCertStorePath = s
                },

                { "tp|trustedcertstorepath=", $"the path of the trusted cert store\nDefault '{OpcTrustedCertDirectoryStorePathDefault}'", (string s) => OpcTrustedCertStorePath = s
                },

                { "rp|rejectedcertstorepath=", $"the path of the rejected cert store\nDefault '{OpcRejectedCertDirectoryStorePathDefault}'", (string s) => OpcRejectedCertStorePath = s
                },

                { "ip|issuercertstorepath=", $"the path of the trusted issuer cert store\nDefault '{OpcIssuerCertDirectoryStorePathDefault}'", (string s) => OpcIssuerCertStorePath = s
                },

                // misc
                { "h|help", "show this message and exit", h => shouldShowHelp = h != null },
            };

            List<string> extraArgs = new List<string>();
            try
            {
                // parse the command line
                extraArgs = options.Parse(args);
            }
            catch (OptionException e)
            {
                // initialize logging
                InitLogging();

                // show message
                Logger.Fatal(e, "Error in command line options");
                // show usage
                Usage(options);
                return;
            }

            // initialize logging
            InitLogging();

            // check args
            if (extraArgs.Count != 0 || shouldShowHelp)
            {
                // show usage
                Usage(options);
                return;
            }

            try
            {
                await ConsoleServerAsync(args);
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "OPC UA server failed unexpectedly.");
            }
            Logger.Information("OPC UA server exiting...");
        }

        private static async Task ConsoleServerAsync(string[] args)
        {
            var quitEvent = new ManualResetEvent(false);

            // init OPC configuration and tracing
            OpcApplicationConfiguration stationOpcApplicationConfiguration = new OpcApplicationConfiguration();
            Opc.Ua.ApplicationConfiguration stationApplicationConfiguration = await stationOpcApplicationConfiguration.ConfigureAsync();

            // allow canceling the connection process
            try
            {
                Console.CancelKeyPress += (sender, eArgs) =>
                {
                    quitEvent.Set();
                    eArgs.Cancel = true;
                };
            }
            catch
            {
            }

            // start the server.
            Logger.Information($"Starting server on endpoint {stationApplicationConfiguration.ServerConfiguration.BaseAddresses[0].ToString()} ...");
            Logger.Information($"Server simulation settings are:");
            Logger.Information($"Ideal cycle time of this station is {IdealCycleTimeDefault} msec");
            Logger.Information($"Power consumption when operating at ideal cycle time is {PowerConsumption} kW");
            Logger.Information($"{(GenerateAlerts ? "Periodically " : "Not ")}generating high pressure for alert simulation.");
            StationServer stationServer = new StationServer();
            stationServer.Start(stationApplicationConfiguration);
            Logger.Information("OPC UA Server started. Press CTRL-C to exit.");

            // wait for Ctrl-C
            quitEvent.WaitOne(Timeout.Infinite);
        }

        /// <summary>
        /// Usage message.
        /// </summary>
        private static void Usage(Mono.Options.OptionSet options)
        {

            // show usage
            Logger.Information("");
            Logger.Information("Usage: {0}.exe [<options>]", Assembly.GetEntryAssembly().GetName().Name);
            Logger.Information("");
            Logger.Information("OPC UA Connectedfactory station for the factory simulation");
            Logger.Information("To exit the application, just press ENTER while it is running.");
            Logger.Information("");

            // output the options
            Logger.Information("Options:");
            StringBuilder stringBuilder = new StringBuilder();
            System.IO.StringWriter stringWriter = new System.IO.StringWriter(stringBuilder);
            options.WriteOptionDescriptions(stringWriter);
            string[] helpLines = stringBuilder.ToString().Split("\r\n");
            foreach (var line in helpLines)
            {
                Logger.Information(line);
            }
            return;
        }

        /// <summary>
        /// Initialize logging.
        /// </summary>
        private static void InitLogging()
        {
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration();

            // set the log level
            switch (_logLevel)
            {
                case "fatal":
                    loggerConfiguration.MinimumLevel.Fatal();
                    OpcTraceToLoggerFatal = 0;
                    break;
                case "error":
                    loggerConfiguration.MinimumLevel.Error();
                    OpcStackTraceMask = OpcTraceToLoggerError = Utils.TraceMasks.Error;
                    break;
                case "warn":
                    loggerConfiguration.MinimumLevel.Warning();
                    OpcTraceToLoggerWarning = 0;
                    break;
                case "info":
                    loggerConfiguration.MinimumLevel.Information();
                    OpcStackTraceMask = OpcTraceToLoggerInformation = 0;
                    break;
                case "debug":
                    loggerConfiguration.MinimumLevel.Debug();
                    OpcStackTraceMask = OpcTraceToLoggerDebug = Utils.TraceMasks.ServiceDetail | Utils.TraceMasks.OperationDetail | Utils.TraceMasks.Service |
                        Utils.TraceMasks.StartStop | Utils.TraceMasks.ExternalSystem | Utils.TraceMasks.Security;
                    break;
                case "verbose":
                    loggerConfiguration.MinimumLevel.Verbose();
                    OpcStackTraceMask = OpcTraceToLoggerVerbose = Utils.TraceMasks.StackTrace | Utils.TraceMasks.Operation | Utils.TraceMasks.Information | Utils.TraceMasks.All;
                    break;
            }

            // set logging sinks
            loggerConfiguration.WriteTo.Console();

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("_GW_LOGP")))
            {
                _logFileName = Environment.GetEnvironmentVariable("_GW_LOGP");
            }

            if (!string.IsNullOrEmpty(_logFileName))
            {
                // configure rolling file sink
                const int MAX_LOGFILE_SIZE = 1024 * 1024;
                const int MAX_RETAINED_LOGFILES = 2;
                loggerConfiguration.WriteTo.File(_logFileName, fileSizeLimitBytes: MAX_LOGFILE_SIZE, rollOnFileSizeLimit: true, retainedFileCountLimit: MAX_RETAINED_LOGFILES);
            }

            Logger = loggerConfiguration.CreateLogger();
            Logger.Information($"Current directory is: {System.IO.Directory.GetCurrentDirectory()}");
            Logger.Information($"Log file is: {System.IO.Path.GetFullPath(_logFileName)}");
            Logger.Information($"Log level is: {_logLevel}");
            return;
        }

        private static string _logFileName = $"{System.Net.Dns.GetHostName().Split('.')[0].ToLowerInvariant()}-station.log";
        private static string _logLevel = "info";
    }
}
