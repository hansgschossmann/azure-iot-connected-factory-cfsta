
using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CfStation
{
    public partial class StationServer : StandardServer
    {
        /// <summary>
        /// Creates the node managers for the server.
        /// </summary>
        /// <remarks>
        /// This method allows the sub-class create any additional node managers which it uses. The SDK
        /// always creates a CoreNodeManager which handles the built-in nodes defined by the specification.
        /// Any additional NodeManagers are expected to handle application specific nodes.
        /// </remarks>
        protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            List<INodeManager> nodeManagers = new List<INodeManager>
            {
                new StationNodeManager(server, configuration)
            };

            return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
        }

        /// <summary>
        /// Loads the non-configurable properties for the application.
        /// </summary>
        /// <remarks>
        /// These properties are exposed by the server but cannot be changed by administrators.
        /// </remarks>
        protected override ServerProperties LoadServerProperties()
        {
            ServerProperties properties = new ServerProperties
            {
                ManufacturerName = "Microsoft",
                ProductName = "IoT Edge OPC Connectedfactory Station",
                ProductUri = "https://github.com/Azure/azure-iot-connected-factory-cfstation.git",
                SoftwareVersion = Utils.GetAssemblySoftwareVersion(),
                BuildNumber = Utils.GetAssemblyBuildNumber(),
                BuildDate = Utils.GetAssemblyTimestamp()
            };
            return properties; 
        }

        /// <summary>
        /// Creates the resource manager for the server.
        /// </summary>
        protected override ResourceManager CreateResourceManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            ResourceManager resourceManager = new ResourceManager(server, configuration);

            System.Reflection.FieldInfo[] fields = typeof(StatusCodes).GetFields(BindingFlags.Public | System.Reflection.BindingFlags.Static);

            foreach (System.Reflection.FieldInfo field in fields)
            {
                uint? id = field.GetValue(typeof(StatusCodes)) as uint?;

                if (id != null)
                {
                    resourceManager.Add(id.Value, "en-US", field.Name);
                }
            }

            return resourceManager;
        }

        /// <summary>
        /// Called after the server has been started.
        /// </summary>
        protected override void OnServerStarted(IServerInternal server)
        {
            base.OnServerStarted(server);

            // request notifications when the user identity is changed. all valid users are accepted by default.
            server.SessionManager.ImpersonateUser += new ImpersonateEventHandler(SessionManager_ImpersonateUser);
        }

        /// <summary>
        /// Called when a client tries to change its user identity.
        /// </summary>
        private void SessionManager_ImpersonateUser(Session session, ImpersonateEventArgs args)
        {
            // check for a user name token.
            UserNameIdentityToken userNameToken = args.NewIdentity as UserNameIdentityToken;
            if (userNameToken != null)
            {
                args.Identity = VerifyPassword(userNameToken);
                return;
            }
        }

        /// <summary>
        /// Validates the password for a username token.
        /// </summary>
        private IUserIdentity VerifyPassword(UserNameIdentityToken userNameToken)
        {
            var userName = userNameToken.UserName;
            var password = userNameToken.DecryptedPassword;
            if (String.IsNullOrEmpty(userName))
            {
                // an empty username is not accepted.
                throw ServiceResultException.Create(StatusCodes.BadIdentityTokenInvalid,
                    "Security token is not a valid username token. An empty username is not accepted.");
            }

            if (String.IsNullOrEmpty(password))
            {
                // an empty password is not accepted.
                throw ServiceResultException.Create(StatusCodes.BadIdentityTokenRejected,
                    "Security token is not a valid username token. An empty password is not accepted.");
            }

            // User with permission to configure server
            if (userName == "sysadmin" && password == "demo")
            {
                return new SystemConfigurationIdentity(new UserIdentity(userNameToken));
            }

            // construct translation object with default text.
            TranslationInfo info = new TranslationInfo(
                "InvalidPassword",
                "en-US",
                "Invalid username or password.",
                userName);

            // create an exception with a vendor defined sub-code.
            throw new ServiceResultException(new ServiceResult(
                StatusCodes.BadUserAccessDenied,
                "InvalidPassword",
                LoadServerProperties().ProductUri,
                new LocalizedText(info)));
        }
    }
}
