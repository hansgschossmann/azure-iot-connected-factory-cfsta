# azure-iot-connected-factory-cfstation

CfStation is an OPC UA server and implements a station in a production line for the factory simulation in [Connectedfactory](https://github.com/Azure/azure-iot-connected-factory).
The implementation is used for three different stations: Assembly, Test and Packaging. The operation of the stations is similar. Some parameters to control different behaviour like power consumption, cycle time and pressure can be passed in via command line.

CfStation's OPC UA server defines a set of nodes and methods, which are used by the Manufacturing Exection System of the production line, which can be found [here](https://github.com/hansgschossmann/azure-iot-connected-factory-cfmes).

By default CfStation listens on endpoint `opc.tcp://<localhostname>:51210`.
For use in the Connectedfactory simulation the hostname, the server port and the path portion of the endpoint can be configured via command line options.

A docker container of CfStation is available [here](https://hub.docker.com/r/hansgschossmann/azure-iot-connected-factory-cfstation).

The command line usage is:

     Usage: CfStation.exe [<options>]

     OPC UA Connectedfactory station for the factory simulation
     To exit the application, just press ENTER while it is running.

     Options:
           --lf, --logfile=VALUE  the filename of the logfile to use.
                                    Default: './Logs/johanngnb.log.txt'
           --pn, --portnum=VALUE  the server port of the OPC server endpoint.
                                    Default: 51210
           --op, --path=VALUE     the enpoint URL path part of the OPC server
                                    endpoint.
                                    Default: ''
           --sh, --stationhostname=VALUE
                                  the fullqualified hostname of the station.
                                    Default: <localhostname>
           --ga, --generatealerts the station should generate alerts.
                                    Default: False
           --pc, --powerconsumption=VALUE
                                  the stations average power consumption in kW
                                    Default:  150 kW
           --ct, --cycletime=VALUE
                                  the stations cycle time in seconds
                                    Default:  7 sec
           --lr, --ldsreginterval=VALUE
                                  the LDS(-ME) registration interval in ms. If 0,
                                    then the registration is disabled.
                                    Default: 0
           --st, --opcstacktracemask=VALUE
                                  the trace mask for the OPC stack. See github OPC .
                                    NET stack for definitions.
                                    To enable IoTHub telemetry tracing set it to 711.

                                    Default: 285  (645)
           --aa, --autoacceptcerts
                                  all certs are trusted when a connection is
                                    established.
                                    Default: False
           --tm, --trustmyself    the server certificate is put into the trusted
                                    certificate store automatically.
                                    Default: True
           --at, --appcertstoretype=VALUE
                                  the own application cert store type.
                                    (allowed values: Directory, X509Store)
                                    Default: 'X509Store'
           --ap, --appcertstorepath=VALUE
                                  the path where the own application cert should be
                                    stored
                                    Default (depends on store type):
                                    X509Store: 'CurrentUser\UA_MachineDefault'
                                    Directory: 'CertificateStores/own'
           --tt, --trustedcertstoretype=VALUE
                                  the trusted cert store type.
                                    (allowed values: Directory, X509Store)
                                    Default: Directory
           --tp, --trustedcertstorepath=VALUE
                                  the path of the trusted cert store
                                    Default (depends on store type):
                                    X509Store: 'CurrentUser\UA_MachineDefault'
                                    Directory: 'CertificateStores/trusted'
           --rt, --rejectedcertstoretype=VALUE
                                  the rejected cert store type.
                                    (allowed values: Directory, X509Store)
                                    Default: Directory
           --rp, --rejectedcertstorepath=VALUE
                                  the path of the rejected cert store
                                    Default (depends on store type):
                                    X509Store: 'CurrentUser\UA_MachineDefault'
                                    Directory: 'CertificateStores/rejected'
           --it, --issuercertstoretype=VALUE
                                  the trusted issuer cert store type.
                                    (allowed values: Directory, X509Store)
                                    Default: Directory
           --ip, --issuercertstorepath=VALUE
                                  the path of the trusted issuer cert store
                                    Default (depends on store type):
                                    X509Store: 'CurrentUser\UA_MachineDefault'
                                    Directory: 'CertificateStores/issuers'
       -h, --help                 show this message and exit
