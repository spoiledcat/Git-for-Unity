using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VersionControl.Git;
using Unity.VersionControl.Git.Json;
using Unity.VersionControl.Git.Logging;
using Unity.VersionControl.Git.NiceIO;

namespace Unity.VersionControl.Git
{
    [Serializable]
    public class Connection
    {
        public string Host { get; set; }
        public string Username { get; set; }
        [NonSerialized] internal GitHubUser User;

        // for json serialization
        public Connection()
        {
        }

        public Connection(string host, string username)
        {
            Host = host;
            Username = username;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + (Host?.GetHashCode() ?? 0);
            hash = hash * 23 + (Username?.GetHashCode() ?? 0);
            return hash;
        }

        public override bool Equals(object other)
        {
            if (other is Connection)
                return Equals((Connection)other);
            return false;
        }

        public bool Equals(Connection other)
        {
            return
                object.Equals(Host, other.Host) &&
                String.Equals(Username, other.Username)
                ;
        }

        public static bool operator ==(Connection lhs, Connection rhs)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(lhs, rhs))
                return true;

            // If one is null, but not both, return false.
            if (((object)lhs == null) || ((object)rhs == null))
                return false;

            // Return true if the fields match:
            return lhs.Equals(rhs);
        }

        public static bool operator !=(Connection lhs, Connection rhs)
        {
            return !(lhs == rhs);
        }
    }

    public class Keychain : IKeychain
    {
        const string ConnectionFile = "connections.json";

        private readonly ILogging logger = LogHelper.GetLogger<Keychain>();
        private readonly ICredentialManager credentialManager;
        private readonly NPath cachePath;
        // cached credentials loaded from git to pass to GitHub/ApiClient
        private readonly Dictionary<UriString, KeychainAdapter> keychainAdapters = new Dictionary<UriString, KeychainAdapter>();

        // loaded at the start of application from cached/serialized data
        private readonly Dictionary<UriString, Connection> connections = new Dictionary<UriString, Connection>();

        public event Action ConnectionsChanged;

        public Keychain(IEnvironment environment, ICredentialManager credentialManager)
        {
            Guard.ArgumentNotNull(environment, nameof(environment));
            Guard.ArgumentNotNull(credentialManager, nameof(credentialManager));
            Guard.NotNull(environment, environment.UserCachePath, nameof(environment.UserCachePath));

            cachePath = environment.UserCachePath.Combine(ConnectionFile);
            this.credentialManager = credentialManager;
        }

        public IKeychainAdapter Connect(UriString host)
        {
            Guard.ArgumentNotNull(host, nameof(host));
            return FindOrCreateAdapter(host);
        }

        public IKeychainAdapter LoadFromSystem(UriString host)
        {
            Guard.ArgumentNotNull(host, nameof(host));

            var keychainAdapter = Connect(host) as KeychainAdapter;
            var credential = credentialManager.Load(host);
            if (credential == null)
            {
                logger.Warning("Cannot load host from Credential Manager; removing from cache");
                Clear(host, false);
                keychainAdapter = null;
            }
            else
            {
                keychainAdapter.Set(credential);
                var connection = GetConnection(host);
                if (connection.Username == null)
                {
                    connection.Username = credential.Username;
                    SaveConnectionsToDisk();
                }

                if (credential.Username != connection.Username)
                {
                    logger.Warning("Keychain Username:\"{0}\" does not match cached Username:\"{1}\"; Hopefully it works", credential.Username, connection.Username);
                }
            }
            return keychainAdapter;
        }

        private KeychainAdapter FindOrCreateAdapter(UriString host)
        {
            KeychainAdapter value;
            if (!keychainAdapters.TryGetValue(host, out value))
            {
                value = new KeychainAdapter();
                keychainAdapters.Add(host, value);
            }
            return value;
        }

        public void Initialize()
        {
            LoadConnectionsFromDisk();
        }

        public void Clear(UriString host, bool deleteFromCredentialManager)
        {
            Guard.ArgumentNotNull(host, nameof(host));

            RemoveConnection(host);

            //clear octokit credentials
            RemoveCredential(host, deleteFromCredentialManager);
        }

        public void SaveToSystem(UriString host)
        {
            Guard.ArgumentNotNull(host, nameof(host));

            var keychainAdapter = AddCredential(host);
            AddConnection(new Connection(host, keychainAdapter.Credential.Username));
        }

        private void LoadConnectionsFromDisk()
        {
            if (cachePath.FileExists())
            {
                var json = cachePath.ReadAllText();
                try
                {
                    var conns = json.FromJson<Connection[]>();
                    UpdateConnections(conns);
                }
                catch (IOException ex)
                {
                    logger.Error(ex, "Error reading connection cache: {0}", cachePath);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error deserializing connection cache: {0}", cachePath);
                    // try to fix the corrupted file with the data we have
                    SaveConnectionsToDisk(raiseChangedEvent: false);
                }
            }
        }

        private void SaveConnectionsToDisk(bool raiseChangedEvent = true)
        {
            try
            {
                var json = connections.Values.ToJson();
                cachePath.WriteAllText(json);
            }
            catch (IOException ex)
            {
                logger.Error(ex, "Error writing connection cache: {0}", cachePath);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error serializing connection cache: {0}", cachePath);
            }

            if (raiseChangedEvent)
                ConnectionsChanged?.Invoke();
        }

        private KeychainAdapter GetKeychainAdapter(UriString host)
        {
            KeychainAdapter credentialAdapter;
            if (!keychainAdapters.TryGetValue(host, out credentialAdapter))
            {
                throw new ArgumentException($"{host} is not found", nameof(host));
            }
            return credentialAdapter;
        }

        private KeychainAdapter AddCredential(UriString host)
        {
            var keychainAdapter = GetKeychainAdapter(host);
            if (string.IsNullOrEmpty(keychainAdapter.Credential.Token))
            {
                throw new InvalidOperationException("Anonymous credentials cannot be stored");
            }

            // saves credential in git credential manager (host, username, token)
            credentialManager.Delete(host);
            credentialManager.Save(keychainAdapter.Credential);
            return keychainAdapter;
        }

        private void RemoveCredential(UriString host, bool deleteFromCredentialManager)
        {
            KeychainAdapter k;
            if (keychainAdapters.TryGetValue(host, out k))
            {
                k.Clear();
                keychainAdapters.Remove(host);
            }

            if (deleteFromCredentialManager)
            {
                credentialManager.Delete(host);
            }
        }

        private Connection GetConnection(UriString host)
        {
            if (!connections.ContainsKey(host))
                return AddConnection(new Connection(host, null));
            return connections[host];
        }

        private Connection AddConnection(Connection connection)
        {
            // create new connection in the connection cache for this host
            if (connections.ContainsKey(connection.Host))
                connections[connection.Host] = connection;
            else
                connections.Add(connection.Host, connection);
            SaveConnectionsToDisk();
            return connection;
        }

        private void RemoveConnection(UriString host)
        {
            // create new connection in the connection cache for this host
            if (connections.ContainsKey(host))
            {
                connections.Remove(host);
                SaveConnectionsToDisk();
            }
        }

        private void UpdateConnections(Connection[] conns)
        {
            var updated = false;
            // remove all the connections we have in memory that are no longer in the connection cache file
            foreach (var host in connections.Values.Except(conns).Select(x => x.Host).ToArray())
            {
                connections.Remove(host);
                updated = true;
            }

            // update existing connections and add new ones from the cache file
            foreach (var connection in conns)
            {
                if (connections.ContainsKey(connection.Host))
                    connections[connection.Host] = connection;
                else
                    connections.Add(connection.Host, connection);
                updated = true;
            }
            if (updated)
                ConnectionsChanged?.Invoke();
        }

        public Connection[] Connections => connections.Values.ToArray();
        public IList<UriString> Hosts => connections.Keys.ToArray();
        public bool HasKeys => connections.Any();
    }
}
