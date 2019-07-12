namespace Unity.VersionControl.Git
{
    using NiceIO;
    using System.Linq;

    public interface IGitEnvironment : IBaseEnvironment
    {
        void Initialize(string unityVersion, NPath extensionInstallPath, NPath unityApplicationPath, NPath unityApplicationContentsPath, NPath assetsPath, NPath repositoryPath);

        NPath GitInstallationPath { get; }
        NPath GitExecutablePath { get; }
        NPath GitLfsInstallationPath { get; }
        NPath GitLfsExecutablePath { get; }
        NPath RepositoryPath { get; set; }
        IRepository Repository { get; set; }
        IUser User { get; set; }
        GitInstaller.GitInstallationState GitInstallationState { get; set; }
    }

    public class GitEnvironment : UnityEnvironment, IGitEnvironment
    {
        public GitEnvironment(string applicationName, string logFile)
            : base(applicationName, logFile)
        { }

        public void Initialize(string unityVersion, NPath extensionInstallPath, NPath EditorApplication_applicationPath,
            NPath EditorApplication_applicationContentsPath, NPath Application_dataPath, NPath repositoryPath)
        {
            base.Initialize(unityVersion, extensionInstallPath, EditorApplication_applicationPath, EditorApplication_applicationContentsPath, Application_dataPath);
            RepositoryPath = FindRepositoryRoot(repositoryPath);
        }

        private static NPath FindRepositoryRoot(NPath path)
        {
            if (path.IsInitialized && path.Exists(".git"))
                return path;
            var ret = path.RecursiveParents.FirstOrDefault(d => d.Exists(".git"));
            if (ret.IsInitialized)
                return ret;
            return path;
        }

        public NPath GitInstallationPath => GitInstallationState.GitInstallationPath;
        public NPath GitExecutablePath => GitInstallationState.GitExecutablePath;
        public NPath GitLfsInstallationPath => GitInstallationState.GitLfsInstallationPath;
        public NPath GitLfsExecutablePath => GitInstallationState.GitLfsExecutablePath;
        public NPath RepositoryPath { get; set; }
        public bool IsCustomGitExecutable => GitInstallationState.IsCustomGitPath;
        public IRepository Repository { get; set; }
        public IUser User { get; set; }

        public GitInstaller.GitInstallationState GitInstallationState
        {
            get
            {
                return SystemSettings.Get(Constants.GitInstallationState, new GitInstaller.GitInstallationState(new GitInstaller.GitInstallDetails(UserCachePath, this)));
            }
            set
            {
                if (value == null)
                    SystemSettings.Unset(Constants.GitInstallationState);
                else
                    SystemSettings.Set(Constants.GitInstallationState, value);
            }
        }

    }
}
