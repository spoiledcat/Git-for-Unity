using System.Threading.Tasks;

namespace Unity.VersionControl.Git
{
    public interface IPlatform
    {
        IPlatform Initialize(IProcessManager processManager, ITaskManager taskManager);
        IProcessEnvironment GitEnvironment { get; }
        ICredentialManager CredentialManager { get; }
        IEnvironment Environment { get; }
        IProcessManager ProcessManager { get; }
        IKeychain Keychain { get; }
    }

    public class Platform : IPlatform
    {
        public Platform(IEnvironment environment)
        {
            Environment = environment;
            GitEnvironment = new ProcessEnvironment(environment);
            Instance = this;
        }

        public IPlatform Initialize(IProcessManager processManager, ITaskManager taskManager)
        {
            ProcessManager = processManager;

            if (CredentialManager == null)
            {
                CredentialManager = new GitCredentialManager(processManager, taskManager);
                Keychain = new Keychain(Environment, CredentialManager);
                Keychain.Initialize();
            }

            return this;
        }

        public static IPlatform Instance { get; private set; }
        public IEnvironment Environment { get; private set; }
        public IProcessEnvironment GitEnvironment { get; private set; }
        public ICredentialManager CredentialManager { get; private set; }
        public IProcessManager ProcessManager { get; private set; }
        public IKeychain Keychain { get; private set; }
    }
}
