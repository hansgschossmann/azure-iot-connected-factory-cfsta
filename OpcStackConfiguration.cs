﻿
using Opc.Ua;
using System;
using System.Security.Cryptography.X509Certificates;

namespace Station
{
    using System.Threading.Tasks;
    using static Opc.Ua.CertificateStoreType;
    using static Program;

    public class OpcStackConfiguration
    {
        public static ApplicationConfiguration OpcApplicationConfiguration => _configuration;

        public static string StationHostname
        {
            get => _stationHostname;
            set => _stationHostname = value;
        }

        public static string StationHostnameLabel => (_stationHostname.Contains(".") ? _stationHostname.Substring(0, _stationHostname.IndexOf('.')).ToLowerInvariant() : _stationHostname);
        public static string ApplicationName => $"{_stationHostname.ToLowerInvariant()}";

        public static string ApplicationUri => $"urn:{StationHostnameLabel}{(string.IsNullOrEmpty(_serverPath) ? string.Empty : ":")}{_serverPath.Replace("/", ":").ToLowerInvariant()}";

        public static string ProductUri => $"http://contoso.com/UA/{StationHostnameLabel}";

        public static string LogFileName
        {
            get => _logFileName;
            set => _logFileName = value;
        }

        public static ushort ServerPort
        {
            get => _serverPort;
            set => _serverPort = value;
        }

        public static string ServerPath
        {
            get => _serverPath;
            set => _serverPath = value;
        }

        public static bool TrustMyself
        {
            get => _trustMyself;
            set => _trustMyself = value;
        }

        // Enable Utils.TraceMasks.OperationDetail to get output for IoTHub telemetry operations. Current: 0x287 (647), with OperationDetail: 0x2C7 (711)
        public static int OpcStackTraceMask
        {
            get => _opcStackTraceMask;
            set => _opcStackTraceMask = value;
        }

        public static string ServerSecurityPolicy
        {
            get => _serverSecurityPolicy;
            set => _serverSecurityPolicy = value;
        }

        public static string OpcOwnCertStoreType
        {
            get => _opcOwnCertStoreType;
            set => _opcOwnCertStoreType = value;
        }

        public static string OpcOwnCertDirectoryStorePathDefault => "CertificateStores/own";
        public static string OpcOwnCertX509StorePathDefault => "CurrentUser\\UA_MachineDefault";
        public static string OpcOwnCertStorePath
        {
            get => _opcOwnCertStorePath;
            set => _opcOwnCertStorePath = value;
        }

        public static string OpcTrustedCertStoreType
        {
            get => _opcTrustedCertStoreType;
            set => _opcTrustedCertStoreType = value;
        }

        public static string OpcTrustedCertDirectoryStorePathDefault => "CertificateStores/trusted";
        public static string OpcTrustedCertX509StorePathDefault => "CurrentUser\\UA_MachineDefault";
        public static string OpcTrustedCertStorePath
        {
            get => _opcTrustedCertStorePath;
            set => _opcTrustedCertStorePath = value;
        }

        public static string OpcRejectedCertStoreType
        {
            get => _opcRejectedCertStoreType;
            set => _opcRejectedCertStoreType = value;
        }

        public static string OpcRejectedCertDirectoryStorePathDefault => "CertificateStores/rejected";
        public static string OpcRejectedCertX509StorePathDefault => "CurrentUser\\UA_MachineDefault";
        public static string OpcRejectedCertStorePath
        {
            get => _opcRejectedCertStorePath;
            set => _opcRejectedCertStorePath = value;
        }

        public static string OpcIssuerCertStoreType
        {
            get => _opcIssuerCertStoreType;
            set => _opcIssuerCertStoreType = value;
        }

        public static string OpcIssuerCertDirectoryStorePathDefault => "CertificateStores/issuers";
        public static string OpcIssuerCertX509StorePathDefault => "CurrentUser\\UA_MachineDefault";
        public static string OpcIssuerCertStorePath
        {
            get => _opcIssuerCertStorePath;
            set => _opcIssuerCertStorePath = value;
        }

        public static int LdsRegistrationInterval
        {
            get => _ldsRegistrationInterval;
            set => _ldsRegistrationInterval = value;
        }

        /// <summary>
        /// Configures all OPC stack settings
        /// </summary>
        public async Task ConfigureAsync()
        {
            // Instead of using a Config.xml we configure everything programmatically.

            //
            // OPC UA Application configuration
            //
            _configuration = new ApplicationConfiguration();

            // Passed in as command line argument
            _configuration.ApplicationName = ApplicationName;
            _configuration.ApplicationUri = ApplicationUri;
            _configuration.ProductUri = ProductUri;
            _configuration.ApplicationType = ApplicationType.Server;

            //
            // Security configuration
            //
            _configuration.SecurityConfiguration = new SecurityConfiguration();

            // Application certificate
            _configuration.SecurityConfiguration.ApplicationCertificate = new CertificateIdentifier();
            _configuration.SecurityConfiguration.ApplicationCertificate.StoreType = _opcOwnCertStoreType;
            _configuration.SecurityConfiguration.ApplicationCertificate.StorePath = _opcOwnCertStorePath;
            _configuration.SecurityConfiguration.ApplicationCertificate.SubjectName = _configuration.ApplicationName;
            Logger.Information($"Application Certificate store type is: {_configuration.SecurityConfiguration.ApplicationCertificate.StoreType}");
            Logger.Information($"Application Certificate store path is: {_configuration.SecurityConfiguration.ApplicationCertificate.StorePath}");
            Logger.Information($"Application Certificate subject name is: {_configuration.SecurityConfiguration.ApplicationCertificate.SubjectName}");

            // Use existing certificate, if it is there.
            X509Certificate2 certificate = await _configuration.SecurityConfiguration.ApplicationCertificate.Find(true);
            if (certificate == null)
            {
                Logger.Information($"No existing Application certificate found. Create a self-signed Application certificate valid from yesterday for {CertificateFactory.defaultLifeTime} months,");
                Logger.Information($"with a {CertificateFactory.defaultKeySize} bit key and {CertificateFactory.defaultHashSize} bit hash.");
                certificate = CertificateFactory.CreateCertificate(
                    _configuration.SecurityConfiguration.ApplicationCertificate.StoreType,
                    _configuration.SecurityConfiguration.ApplicationCertificate.StorePath,
                    null,
                    _configuration.ApplicationUri,
                    _configuration.ApplicationName,
                    _configuration.ApplicationName,
                    null,
                    CertificateFactory.defaultKeySize,
                    DateTime.UtcNow - TimeSpan.FromDays(1),
                    CertificateFactory.defaultLifeTime,
                    CertificateFactory.defaultHashSize,
                    false,
                    null,
                    null
                    );
                _configuration.SecurityConfiguration.ApplicationCertificate.Certificate = certificate ?? throw new Exception("OPC UA application certificate can not be created! Cannot continue without it!");
            }
            else
            {
                Logger.Information("Application certificate found in Application Certificate Store");
            }
            _configuration.ApplicationUri = Utils.GetApplicationUriFromCertificate(certificate);
            Logger.Information($"Application certificate is for Application URI '{_configuration.ApplicationUri}', Application '{_configuration.ApplicationName} and has Subject '{_configuration.ApplicationName}'");

            // TrustedIssuerCertificates
            _configuration.SecurityConfiguration.TrustedIssuerCertificates = new CertificateTrustList();
            _configuration.SecurityConfiguration.TrustedIssuerCertificates.StoreType = _opcIssuerCertStoreType;
            _configuration.SecurityConfiguration.TrustedIssuerCertificates.StorePath = _opcIssuerCertStorePath;
            Logger.Information($"Trusted Issuer store type is: {_configuration.SecurityConfiguration.TrustedIssuerCertificates.StoreType}");
            Logger.Information($"Trusted Issuer Certificate store path is: {_configuration.SecurityConfiguration.TrustedIssuerCertificates.StorePath}");

            // TrustedPeerCertificates
            _configuration.SecurityConfiguration.TrustedPeerCertificates = new CertificateTrustList();
            _configuration.SecurityConfiguration.TrustedPeerCertificates.StoreType = _opcTrustedCertStoreType;
            if (string.IsNullOrEmpty(_opcTrustedCertStorePath))
            {
                // Set default.
                _configuration.SecurityConfiguration.TrustedPeerCertificates.StorePath = _opcTrustedCertStoreType == X509Store ? OpcTrustedCertX509StorePathDefault : OpcTrustedCertDirectoryStorePathDefault;
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("_TPC_SP")))
                {
                    // Use environment variable.
                    _configuration.SecurityConfiguration.TrustedPeerCertificates.StorePath = Environment.GetEnvironmentVariable("_TPC_SP");
                }
            }
            else
            {
                _configuration.SecurityConfiguration.TrustedPeerCertificates.StorePath = _opcTrustedCertStorePath;
            }
            Logger.Information($"Trusted Peer Certificate store type is: {_configuration.SecurityConfiguration.TrustedPeerCertificates.StoreType}");
            Logger.Information($"Trusted Peer Certificate store path is: {_configuration.SecurityConfiguration.TrustedPeerCertificates.StorePath}");

            // RejectedCertificateStore
            _configuration.SecurityConfiguration.RejectedCertificateStore = new CertificateTrustList();
            _configuration.SecurityConfiguration.RejectedCertificateStore.StoreType = _opcRejectedCertStoreType;
            _configuration.SecurityConfiguration.RejectedCertificateStore.StorePath = _opcRejectedCertStorePath;
            Logger.Information($"Rejected certificate store type is: {_configuration.SecurityConfiguration.RejectedCertificateStore.StoreType}");
            Logger.Information($"Rejected Certificate store path is: {_configuration.SecurityConfiguration.RejectedCertificateStore.StorePath}");

            // AutoAcceptUntrustedCertificates
            // This is a security risk and should be set to true only for debugging purposes.
            _configuration.SecurityConfiguration.AutoAcceptUntrustedCertificates = false;

            // RejectSHA1SignedCertificates
            // We allow SHA1 certificates for now as many OPC Servers still use them
            _configuration.SecurityConfiguration.RejectSHA1SignedCertificates = false;
            Logger.Information($"Rejection of SHA1 signed certificates is {(_configuration.SecurityConfiguration.RejectSHA1SignedCertificates ? "enabled" : "disabled")}");

            // MinimunCertificatesKeySize
            // We allow a minimum key size of 1024 bit, as many OPC UA servers still use them
            _configuration.SecurityConfiguration.MinimumCertificateKeySize = 1024;
            Logger.Information($"Minimum certificate key size set to {_configuration.SecurityConfiguration.MinimumCertificateKeySize}");

            // We make the default reference stack behavior configurable to put our own certificate into the trusted peer store.
            if (_trustMyself)
            {
                // Ensure it is trusted
                try
                {
                    ICertificateStore store = _configuration.SecurityConfiguration.TrustedPeerCertificates.OpenStore();
                    if (store == null)
                    {
                        Logger.Warning($"Can not open trusted peer store. StorePath={_configuration.SecurityConfiguration.TrustedPeerCertificates.StorePath}");
                    }
                    else
                    {
                        try
                        {
                            Logger.Information($"Adding server certificate to trusted peer store. StorePath={_configuration.SecurityConfiguration.TrustedPeerCertificates.StorePath}");
                            X509Certificate2 publicKey = new X509Certificate2(certificate.RawData);
                            await store.Add(publicKey);
                        }
                        finally
                        {
                            store.Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Fatal(e, $"Can not add server certificate to trusted peer store. StorePath={_configuration.SecurityConfiguration.TrustedPeerCertificates.StorePath})");
                }
            }
            else
            {
                Logger.Warning("Server certificate is not added to trusted peer store.");
            }

            //
            // TransportConfigurations
            //
            _configuration.TransportQuotas = new TransportQuotas();

            //
            // ServerConfiguration
            //
            _configuration.ServerConfiguration = new ServerConfiguration();

            // BaseAddresses
            if (_configuration.ServerConfiguration.BaseAddresses.Count == 0)
            {
                // We do not use the localhost replacement mechanism of the configuration loading, to immediately show the base address here
                _configuration.ServerConfiguration.BaseAddresses.Add($"opc.tcp://{StationHostname}:{_serverPort}{_serverPath}");
            }
            foreach (var endpoint in _configuration.ServerConfiguration.BaseAddresses)
            {
                Logger.Information($"OPC UA server base address: {endpoint}");
            }

            // SecurityPolicies
            // We do not allow security policy SecurityPolicies.None, but always high security
            ServerSecurityPolicy newPolicy = new ServerSecurityPolicy()
            {
                SecurityMode = MessageSecurityMode.SignAndEncrypt,
                SecurityPolicyUri = SecurityPolicies.Basic256Sha256
            };
            _configuration.ServerConfiguration.SecurityPolicies.Add(newPolicy);
            Logger.Information($"Security policy {newPolicy.SecurityPolicyUri} with mode {newPolicy.SecurityMode} added");

            // MaxRegistrationInterval
            _configuration.ServerConfiguration.MaxRegistrationInterval = _ldsRegistrationInterval;
            Logger.Information($"LDS(-ME) registration intervall set to {_ldsRegistrationInterval} ms (0 means no registration)");

            //
            // TraceConfiguration
            //
            _configuration.TraceConfiguration = new TraceConfiguration();
            // Due to a bug in a stack we need to do console output ourselve.
            Utils.SetTraceOutput(Utils.TraceOutput.FileOnly);

            // OutputFilePath
            if (string.IsNullOrEmpty(_logFileName))
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("_GW_LOGP")))
                {
                    _configuration.TraceConfiguration.OutputFilePath = Environment.GetEnvironmentVariable("_GW_LOGP");
                }
                else
                {
                    _configuration.TraceConfiguration.OutputFilePath = "./Logs/" + _configuration.ApplicationName + ".log.txt";
                }
            }
            else
            {
                _configuration.TraceConfiguration.OutputFilePath = _logFileName;
            }

            // DeleteOnLoad
            _configuration.TraceConfiguration.DeleteOnLoad = false;

            // TraceMasks
            _configuration.TraceConfiguration.TraceMasks = _opcStackTraceMask;

            // Apply the settings
            _configuration.TraceConfiguration.ApplySettings();
            Logger.Information($"Current directory is: {System.IO.Directory.GetCurrentDirectory()}");
            Logger.Information($"Log file is: {Utils.GetAbsoluteFilePath(_configuration.TraceConfiguration.OutputFilePath, true, false, false, true)}");
            Logger.Information($"opcstacktracemask set to: 0x{_opcStackTraceMask:X} ({_opcStackTraceMask})");

            // validate the configuration now
            await _configuration.Validate(_configuration.ApplicationType);
        }

        private static string _stationHostname = $"{Utils.GetHostName()}";
        private static string _logFileName;
        private static ushort _serverPort = 51210;
        private static string _serverPath = string.Empty;
        private static bool _trustMyself = true;
        private static int _opcStackTraceMask = Utils.TraceMasks.Error | Utils.TraceMasks.Security | Utils.TraceMasks.StackTrace | Utils.TraceMasks.StartStop;

        private static string _serverSecurityPolicy = SecurityPolicies.Basic128Rsa15;
        private static string _opcOwnCertStoreType = X509Store;
        private static string _opcOwnCertStorePath = OpcOwnCertX509StorePathDefault;
        private static string _opcTrustedCertStoreType = Directory;
        private static string _opcTrustedCertStorePath = null;
        private static string _opcRejectedCertStoreType = Directory;
        private static string _opcRejectedCertStorePath = OpcRejectedCertDirectoryStorePathDefault;
        private static string _opcIssuerCertStoreType = Directory;
        private static string _opcIssuerCertStorePath = OpcIssuerCertDirectoryStorePathDefault;
        private static int _ldsRegistrationInterval = 0;
        private static ApplicationConfiguration _configuration;
    }
}
