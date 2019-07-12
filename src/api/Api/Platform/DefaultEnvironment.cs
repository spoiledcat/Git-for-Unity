using System.Linq;
using Unity.VersionControl.Git.NiceIO;

namespace Unity.VersionControl.Git
{
    public interface IEnvironment : IGitEnvironment
    {
        ICacheContainer CacheContainer { get; }
        NPath OctorunScriptPath { get; }
    }

    public class DefaultEnvironment : GitEnvironment, IEnvironment
    {
        private const string logFile = "github-unity.log";

        private NPath nodeJsExecutablePath;
        private NPath octorunScriptPath;

        public DefaultEnvironment(ICacheContainer cacheContainer) : base(ApplicationInfo.ApplicationName, logFile)
        {
            this.CacheContainer = cacheContainer;
        }

        public void InitializeRepository(NPath? repositoryPath = null)
        {
            Guard.NotNull(this, FileSystem, nameof(FileSystem));

            NPath expectedRepositoryPath;
            if (!RepositoryPath.IsInitialized || (repositoryPath != null && RepositoryPath != repositoryPath.Value))
            {
                Guard.NotNull(this, UnityProjectPath, nameof(UnityProjectPath));

                expectedRepositoryPath = repositoryPath ?? UnityProjectPath;

                if (!expectedRepositoryPath.Exists(".git"))
                {
                    NPath reporoot = UnityProjectPath.RecursiveParents.FirstOrDefault(d => d.Exists(".git"));
                    if (reporoot.IsInitialized)
                        expectedRepositoryPath = reporoot;
                }
            }
            else
            {
                expectedRepositoryPath = RepositoryPath;
            }

            FileSystem.CurrentDirectory = expectedRepositoryPath;
            if (expectedRepositoryPath.Exists(".git"))
            {
                RepositoryPath = expectedRepositoryPath;
                Repository = new Repository(RepositoryPath, CacheContainer);
            }
        }


        public NPath OctorunScriptPath
        {
            get
            {
                if (!octorunScriptPath.IsInitialized)
                    octorunScriptPath = UserCachePath.Combine("octorun", "src", "bin", "app.js");
                return octorunScriptPath;
            }
            set
            {
                octorunScriptPath = value;
            }
        }


        public NPath NodeJsExecutablePath
        {
            get
            {
                if (!nodeJsExecutablePath.IsInitialized)
                {
                    nodeJsExecutablePath = IsWindows ?
                        UnityApplicationContents.Combine("Tools", "nodejs", "node" + ExecutableExtension) :
                        UnityApplicationContents.Combine("Tools", "nodejs", "bin", "node" + ExecutableExtension);
                }
                return nodeJsExecutablePath;
            }
        }
        public ICacheContainer CacheContainer { get; private set; }
    }
}
