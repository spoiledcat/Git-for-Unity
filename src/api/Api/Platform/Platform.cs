using System;
using System.Threading.Tasks;
using Unity.VersionControl.Git.NiceIO;

namespace Unity.VersionControl.Git
{
    public interface IPlatform : IDisposable
    {
        IPlatform Initialize();
        IEnvironment Environment { get; }
        IProcessEnvironment GitProcessEnvironment { get; }
        IProcessEnvironment ProcessEnvironment { get; }
        IProcessManager ProcessManager { get; }
        ITaskManager TaskManager { get; }
        ICredentialManager CredentialManager { get; }
        IKeychain Keychain { get; }
        IGitClient GitClient { get; }
    }

    public class Platform : IPlatform
    {
        public Platform(NPath workingDirectory, IEnvironment environment, ITaskManager taskManager)
        {
            TaskManager = taskManager;
            Environment = environment;
            ProcessManager = new ProcessManager(Environment, workingDirectory, TaskManager.Token);
            ProcessEnvironment = ProcessManager.DefaultProcessEnvironment;
            GitProcessEnvironment = new ProcessEnvironment(environment, workingDirectory);
            CredentialManager = new GitCredentialManager(ProcessManager, TaskManager);
            GitClient = new GitClient(Environment, ProcessManager, TaskManager.Token);
        }

        public IPlatform Initialize()
        {
            Keychain = new Keychain(Environment, CredentialManager);
            Keychain.Initialize();

            return this;
        }

        private bool disposed = false;
        private void Dispose(bool disposing)
        {
            if (disposed) return;
            disposed = true;
            if (disposing)
            {
                ProcessManager.Stop();
                TaskManager.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public IEnvironment Environment { get; }
        public IProcessEnvironment GitProcessEnvironment { get; }
        public IProcessEnvironment ProcessEnvironment { get; }
        public ICredentialManager CredentialManager { get; }
        public ITaskManager TaskManager { get; }
        public IProcessManager ProcessManager { get; }
        public IKeychain Keychain { get; private set; }
        public IGitClient GitClient { get; }
    }
}
