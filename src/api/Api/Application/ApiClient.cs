using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Unity.VersionControl.Git.Logging;

namespace Unity.VersionControl.Git
{
    public class ApiClient : IApiClient
    {
        private static readonly ILogging logger = LogHelper.GetLogger<ApiClient>();
        private static readonly Regex httpStatusErrorRegex = new Regex("(?<=[a-z])([A-Z])", RegexOptions.Compiled);
        private static readonly Regex accessTokenRegex = new Regex("access_token=(.*?)&", RegexOptions.Compiled);

        public HostAddress HostAddress { get; }

        private readonly IKeychain keychain;
        private readonly IProcessManager processManager;
        private readonly ITaskManager taskManager;
        private readonly IEnvironment environment;
        private IKeychainAdapter keychainAdapter;
        private Connection connection;

        public ApiClient(IKeychain keychain, IProcessManager processManager, ITaskManager taskManager,
            IEnvironment environment, UriString host = null)
        {
            Guard.ArgumentNotNull(keychain, nameof(keychain));

            host = host == null
                ? UriString.ToUriString(HostAddress.GitHubDotComHostAddress.WebUri)
                : new UriString(host.ToRepositoryUri().GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped));

            HostAddress = HostAddress.Create(host);

            this.keychain = keychain;
            this.processManager = processManager;
            this.taskManager = taskManager;
            this.environment = environment;
        }

        private IKeychainAdapter EnsureKeychainAdapter()
        {
            var adapter = KeychainAdapter;
            if (adapter.Credential == null)
            {
                throw new ApiClientException("No Credentials found");
            }

            return adapter;
        }


        private Connection Connection
        {
            get
            {
                if (connection == null)
                {
                    connection = keychain.Connections.FirstOrDefault(x => x.Host.ToUriString().Host == HostAddress.WebUri.Host);
                }

                return connection;
            }
        }

        private IKeychainAdapter KeychainAdapter
        {
            get
            {
                if (keychainAdapter == null)
                {
                    if (Connection == null)
                        throw new KeychainEmptyException();

                    var loadedKeychainAdapter = keychain.LoadFromSystem(Connection.Host);
                    if (loadedKeychainAdapter == null)
                        throw new KeychainEmptyException();

                    if (string.IsNullOrEmpty(loadedKeychainAdapter.Credential?.Username))
                    {
                        logger.Warning("LoadKeychainInternal: Username is empty");
                        throw new TokenUsernameMismatchException(connection.Username);
                    }

                    if (loadedKeychainAdapter.Credential.Username != connection.Username)
                    {
                        logger.Warning("LoadKeychainInternal: Token username does not match");
                    }

                    keychainAdapter = loadedKeychainAdapter;
                }

                return keychainAdapter;
            }
        }

        private GitHubUser GetValidatedGitHubUser()
        {
            try
            {
                var adapter = EnsureKeychainAdapter();

                var command = HostAddress.IsGitHubDotCom() ? "validate" : "validate -h " + HostAddress.ApiUri.Host;
                var octorunTask = new OctorunTask(taskManager.Token, environment, command, adapter.Credential.Token)
                    .Configure(processManager);

                var ret = octorunTask.RunSynchronously();
                if (ret.IsSuccess)
                {
                    var login = ret.Output[1];

                    if (!string.Equals(login, Connection.Username, StringComparison.InvariantCultureIgnoreCase))
                    {
                        logger.Trace("LoadKeychainInternal: Api username does not match");
                        throw new TokenUsernameMismatchException(Connection.Username, login);
                    }

                    return new GitHubUser
                    {
                        Name = ret.Output[0],
                        Login = login
                    };
                }

                throw new ApiClientException(ret.GetApiErrorMessage() ?? "Error validating current user");
            }
            catch (KeychainEmptyException)
            {
                logger.Warning("Keychain is empty");
                throw;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error Getting Current User");
                throw;
            }
        }
    }

    public class GitHubHostMeta
    {
        public bool VerifiablePasswordAuthentication { get; set; }
        public string GithubServicesSha { get; set; }
        public string InstalledVersion { get; set; }
    }

    public class GitHubUser
    {
        public string Name { get; set; }
        public string Login { get; set; }
    }

    public class GitHubRepository
    {
        public string Name { get; set; }
        public string CloneUrl { get; set; }
    }

    [Serializable]
    public class ApiClientException : Exception
    {
        public ApiClientException()
        { }

        public ApiClientException(string message) : base(message)
        { }

        public ApiClientException(string message, Exception innerException) : base(message, innerException)
        { }

        protected ApiClientException(SerializationInfo info, StreamingContext context) : base(info, context)
        { }
    }

    [Serializable]
    public class TokenUsernameMismatchException : ApiClientException
    {
        public string CachedUsername { get; }
        public string CurrentUsername { get; }

        public TokenUsernameMismatchException(string cachedUsername, string currentUsername = null)
        {
            CachedUsername = cachedUsername;
            CurrentUsername = currentUsername;
        }
        protected TokenUsernameMismatchException(SerializationInfo info, StreamingContext context) : base(info, context)
        { }
    }

    [Serializable]
    public class KeychainEmptyException : ApiClientException
    {
        public KeychainEmptyException()
        {
        }

        protected KeychainEmptyException(SerializationInfo info, StreamingContext context) : base(info, context)
        { }
    }
}
