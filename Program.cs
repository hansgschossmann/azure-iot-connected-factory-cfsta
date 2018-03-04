using Mono.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Station
{
    using Serilog;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using static Opc.Ua.CertificateStoreType;
    using static OpcStackConfiguration;
    using static StationNodeManager;
    using static System.Console;

    public class Program
    {
        public static Serilog.Core.Logger Logger = null;

        /// <summary>
        /// Synchronous main method of the app.
        /// </summary>
        public static void Main(string[] args)
        {
            Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .MinimumLevel.Debug()
                .CreateLogger();

            MainAsync(args).Wait();
        }

        /// <summary>
        /// Asynchronous part of the main method of the app.
        /// </summary>
        public async static Task MainAsync(string[] args)
        {
            var shouldShowHelp = false;

            // command line options
            OptionSet options = new OptionSet {
                // opc server configuration options
                { "lf|logfile=", $"the filename of the logfile to use.\nDefault: './Logs/{ApplicationName}.log.txt'", (string l) => LogFileName = l },
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
                { "st|opcstacktracemask=", $"the trace mask for the OPC stack. See github OPC .NET stack for definitions.\nTo enable IoTHub telemetry tracing set it to 711.\nDefault: {OpcStackTraceMask:X}  ({OpcStackTraceMask})", (int i) => {
                        if (i >= 0)
                        {
                            OpcStackTraceMask = i;
                        }
                        else
                        {
                            throw new OptionException("The OPC stack trace mask must be larger or equal 0.", "opcstacktracemask");
                        }
                    }
                },
                { "aa|autoacceptcerts", $"all certs are trusted when a connection is established.\nDefault: {_autoAcceptCerts}", a => _autoAcceptCerts = a != null },

                // trust own public cert option
                { "tm|trustmyself", $"the server certificate is put into the trusted certificate store automatically.\nDefault: {TrustMyself}", t => TrustMyself = t != null },

                // own cert store options
                { "at|appcertstoretype=", $"the own application cert store type. \n(allowed values: Directory, X509Store)\nDefault: '{OpcOwnCertStoreType}'", (string s) => {
                        if (s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) || s.Equals(Directory, StringComparison.OrdinalIgnoreCase))
                        {
                            OpcOwnCertStoreType = s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) ? X509Store : Directory;
                            OpcOwnCertStorePath = s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) ? OpcOwnCertX509StorePathDefault : OpcOwnCertDirectoryStorePathDefault;
                        }
                        else
                        {
                            throw new OptionException();
                        }
                    }
                },
                { "ap|appcertstorepath=", $"the path where the own application cert should be stored\nDefault (depends on store type):\n" +
                        $"X509Store: '{OpcOwnCertX509StorePathDefault}'\n" +
                        $"Directory: '{OpcOwnCertDirectoryStorePathDefault}'", (string s) => OpcOwnCertStorePath = s
                },

                // trusted cert store options
                {
                "tt|trustedcertstoretype=", $"the trusted cert store type. \n(allowed values: Directory, X509Store)\nDefault: {OpcTrustedCertStoreType}", (string s) => {
                        if (s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) || s.Equals(Directory, StringComparison.OrdinalIgnoreCase))
                        {
                            OpcTrustedCertStoreType = s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) ? X509Store : Directory;
                            OpcTrustedCertStorePath = s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) ? OpcTrustedCertX509StorePathDefault : OpcTrustedCertDirectoryStorePathDefault;
                        }
                        else
                        {
                            throw new OptionException();
                        }
                    }
                },
                { "tp|trustedcertstorepath=", $"the path of the trusted cert store\nDefault (depends on store type):\n" +
                        $"X509Store: '{OpcTrustedCertX509StorePathDefault}'\n" +
                        $"Directory: '{OpcTrustedCertDirectoryStorePathDefault}'", (string s) => OpcTrustedCertStorePath = s
                },

                // rejected cert store options
                { "rt|rejectedcertstoretype=", $"the rejected cert store type. \n(allowed values: Directory, X509Store)\nDefault: {OpcRejectedCertStoreType}", (string s) => {
                        if (s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) || s.Equals(Directory, StringComparison.OrdinalIgnoreCase))
                        {
                            OpcRejectedCertStoreType = s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) ? X509Store : Directory;
                            OpcRejectedCertStorePath = s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) ? OpcRejectedCertX509StorePathDefault : OpcRejectedCertDirectoryStorePathDefault;
                        }
                        else
                        {
                            throw new OptionException();
                        }
                    }
                },
                { "rp|rejectedcertstorepath=", $"the path of the rejected cert store\nDefault (depends on store type):\n" +
                        $"X509Store: '{OpcRejectedCertX509StorePathDefault}'\n" +
                        $"Directory: '{OpcRejectedCertDirectoryStorePathDefault}'", (string s) => OpcRejectedCertStorePath = s
                },

                // issuer cert store options
                {
                "it|issuercertstoretype=", $"the trusted issuer cert store type. \n(allowed values: Directory, X509Store)\nDefault: {OpcIssuerCertStoreType}", (string s) => {
                        if (s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) || s.Equals(Directory, StringComparison.OrdinalIgnoreCase))
                        {
                            OpcIssuerCertStoreType = s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) ? X509Store : Directory;
                            OpcIssuerCertStorePath = s.Equals(X509Store, StringComparison.OrdinalIgnoreCase) ? OpcIssuerCertX509StorePathDefault : OpcIssuerCertDirectoryStorePathDefault;
                        }
                        else
                        {
                            throw new OptionException();
                        }
                    }
                },
                { "ip|issuercertstorepath=", $"the path of the trusted issuer cert store\nDefault (depends on store type):\n" +
                        $"X509Store: '{OpcIssuerCertX509StorePathDefault}'\n" +
                        $"Directory: '{OpcIssuerCertDirectoryStorePathDefault}'", (string s) => OpcIssuerCertStorePath = s
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
                // show message
                Logger.Fatal(e, "Error in command line options");
                // show usage
                Usage(options);
                return;
            }

            if (extraArgs.Count != 0)
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
            OpcStackConfiguration opcStackConfiguration = new OpcStackConfiguration();
            await opcStackConfiguration.ConfigureAsync();

            // handle cert validation
            if (_autoAcceptCerts)
            {
                Logger.Warning("WARNING: Automatically accepting certificates. This is a security risk.");
                OpcApplicationConfiguration.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;
            }
            OpcApplicationConfiguration.CertificateValidator = new Opc.Ua.CertificateValidator();
            OpcApplicationConfiguration.CertificateValidator.CertificateValidation += new Opc.Ua.CertificateValidationEventHandler(CertificateValidator_CertificateValidation);

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
            Logger.Information($"Starting server on endpoint {OpcApplicationConfiguration.ServerConfiguration.BaseAddresses[0].ToString()} ...");
            Logger.Information($"Server simulation settings are:");
            Logger.Information($"Ideal cycle time of this station is {IdealCycleTimeDefault} msec");
            Logger.Information($"Power consumption when operating at ideal cycle time is {PowerConsumption} kW");
            Logger.Information($"{(GenerateAlerts ? "Periodically " : "Not ")}generating high pressure for alert simulation.");
            StationServer stationServer = new StationServer();
            stationServer.Start(OpcApplicationConfiguration);
            Logger.Information("OPC UA Server started. Press CTRL-C to exit.");

            // wait for Ctrl-C
            quitEvent.WaitOne(Timeout.Infinite);
        }

        /// <summary>
        /// Usage message.
        /// </summary>
        private static void Usage(OptionSet options)
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

        private static void CertificateValidator_CertificateValidation(Opc.Ua.CertificateValidator validator, Opc.Ua.CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = _autoAcceptCerts;
                if (_autoAcceptCerts)
                {
                    Logger.Information($"Accepting Certificate: {e.Certificate.Subject}");
                }
                else
                {
                    Logger.Information($"Rejecting Certificate: {e.Certificate.Subject}");
                }
            }
        }

        private static bool _autoAcceptCerts = false;
    }
}
