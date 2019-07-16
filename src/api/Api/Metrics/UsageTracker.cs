﻿using System;
using System.Linq;
using System.Text;
using System.Threading;
using Unity.VersionControl.Git;
using Unity.VersionControl.Git.Json;
using Unity.VersionControl.Git.Logging;
using Unity.VersionControl.Git.NiceIO;

namespace Unity.VersionControl.Git
{
    class UsageTrackerSync : IUsageTracker
    {

#if DEVELOPER_BUILD
        protected internal const int MetrisReportTimeout = 30;
#else
        protected internal const int MetrisReportTimeout = 3 * 60;
#endif

        private static ILogging Logger { get; } = LogHelper.GetLogger<UsageTracker>();

        private static object _lock = new object();

        private readonly ISettings userSettings;
        private readonly IUsageLoader usageLoader;
        private readonly string userId;
        private readonly string appVersion;
        private readonly string unityVersion;
        private readonly string instanceId;
        private Timer timer;

        public IMetricsService MetricsService { get; set; }

        public UsageTrackerSync(ISettings userSettings, IUsageLoader usageLoader,
            string unityVersion, string instanceId)
        {
            this.userSettings = userSettings;
            this.usageLoader = usageLoader;
            this.appVersion = ApplicationInfo.Version;
            this.unityVersion = unityVersion;
            this.instanceId = instanceId;

            if (userSettings.Exists(Constants.GuidKey))
            {
                userId = userSettings.Get(Constants.GuidKey);
            }

            if (String.IsNullOrEmpty(userId))
            {
                userId = Guid.NewGuid().ToString();
                userSettings.Set(Constants.GuidKey, userId);
            }

            Logger.Trace("userId:{0} instanceId:{1}", userId, instanceId);
            if (Enabled)
                RunTimer(MetrisReportTimeout);
        }

        private void RunTimer(int seconds)
        {
            timer = new System.Threading.Timer(_ =>
            {
                try
                {
                    timer.Dispose();
                    SendUsage();
                }
                catch { }
            }, null, seconds * 1000, Timeout.Infinite);
        }

        private void SendUsage()
        {
            if (MetricsService == null)
            {
                Logger.Warning("Metrics disabled: no service");
                return;
            }

            if (!Enabled)
            {
                Logger.Trace("Metrics disabled");
                return;
            }

            UsageStore usageStore = null;
            lock (_lock)
            {
                usageStore = usageLoader.Load(userId);
            }

            var currentTimeOffset = DateTimeOffset.UtcNow;
            if (usageStore.LastSubmissionDate.Date == currentTimeOffset.Date)
            {
                Logger.Trace("Already sent today");
                return;
            }

            var extractReports = usageStore.Model.SelectReports(currentTimeOffset.Date);
            if (!extractReports.Any())
            {
                Logger.Trace("No items to send");
                return;
            }

            var username = GetUsername();
            if (!String.IsNullOrEmpty(username)) {
                extractReports.ForEach(x => x.Dimensions.GitHubUser = username);
            }

            try
            {
                MetricsService.PostUsage(extractReports);
            }
            catch (Exception ex)
            {
                Logger.Warning(@"Error sending usage:""{0}"" Message:""{1}""", ex.GetType(), ex.GetExceptionMessageShort());
                return;
            }

            // if we're here, success!
            lock (_lock)
            {
                usageStore = usageLoader.Load(userId);
                usageStore.LastSubmissionDate = currentTimeOffset;
                usageStore.Model.RemoveReports(currentTimeOffset.Date);
                usageLoader.Save(usageStore);
            }

            // update the repo size for the current report, while we're at it
            CaptureRepoSize();
        }

        protected virtual void CaptureRepoSize()
        {}

        public virtual void IncrementNumberOfStartups()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .NumberOfStartups++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementProjectsInitialized()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .ProjectsInitialized++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementChangesViewButtonCommit()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .ChangesViewButtonCommit++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementHistoryViewToolbarFetch()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .HistoryViewToolbarFetch++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementHistoryViewToolbarPush()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .HistoryViewToolbarPush++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementHistoryViewToolbarPull()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .HistoryViewToolbarPull++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementBranchesViewButtonCreateBranch()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .BranchesViewButtonCreateBranch++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementBranchesViewButtonDeleteBranch()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .BranchesViewButtonDeleteBranch++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementBranchesViewButtonCheckoutLocalBranch()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .BranchesViewButtonCheckoutLocalBranch++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementBranchesViewButtonCheckoutRemoteBranch()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .BranchesViewButtonCheckoutRemoteBranch++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementSettingsViewButtonLfsUnlock()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .SettingsViewButtonLfsUnlock++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementAuthenticationViewButtonAuthentication()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .AuthenticationViewButtonAuthentication++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementUnityProjectViewContextLfsLock()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .UnityProjectViewContextLfsLock++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementUnityProjectViewContextLfsUnlock()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                    .UnityProjectViewContextLfsUnlock++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementPublishViewButtonPublish()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                     .PublishViewButtonPublish++;
                usageLoader.Save(usage);
            }
        }

        public virtual void IncrementApplicationMenuMenuItemCommandLine()
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId)
                     .ApplicationMenuMenuItemCommandLine++;
                usageLoader.Save(usage);
            }
        }

        public virtual void UpdateRepoSize(int kilobytes)
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId).GitRepoSize = kilobytes;
                usageLoader.Save(usage);
            }
        }

        public virtual void UpdateLfsDiskUsage(int kilobytes)
        {
            lock (_lock)
            {
                var usage = usageLoader.Load(userId);
                usage.GetCurrentMeasures(appVersion, unityVersion, instanceId).LfsDiskUsage = kilobytes;
                usageLoader.Save(usage);
            }
        }

        protected virtual string GetUsername()
        {
            return "";
        }

        public bool Enabled
        {
            get
            {
                return userSettings.Get(Constants.MetricsKey, true);
            }
            set
            {
                if (value == Enabled)
                    return;
                userSettings.Set(Constants.MetricsKey, value);
                if (value)
                {
                    RunTimer(5);
                }
                else
                {
                    timer.Dispose();
                    timer = null;
                }
            }
        }
    }

    class UsageTracker : UsageTrackerSync
    {
        public UsageTracker(ITaskManager taskManager, IGitClient gitClient, IProcessManager processManager,
            ISettings userSettings,
            IEnvironment environment,
            IKeychain keychain,
            string instanceId)
            : base(userSettings,
                   new UsageLoader(environment.UserCachePath.Combine(Constants.UsageFile)),
                   environment.UnityVersion, instanceId)
        {
            TaskManager = taskManager;
            Environment = environment;
            GitClient = gitClient;
            ProcessManager = processManager;
            Keychain = keychain;
        }

        protected override void CaptureRepoSize()
        {
            try
            {
                var gitSize = GitClient.CountObjects()
                    .Catch(_ => true)
                    .RunSynchronously();
                base.UpdateRepoSize(gitSize);

                var gitLfsDataPath = Environment.RepositoryPath.Combine(".git", "lfs");
                if (gitLfsDataPath.Exists())
                {
                    var lfsSize = new LinuxDiskUsageTask(gitLfsDataPath, TaskManager.Token)
                        .Configure(ProcessManager)
                        .Catch(_ => true)
                        .RunSynchronously();
                    base.UpdateLfsDiskUsage(lfsSize);
                }
            }
            catch {}
        }

        protected override string GetUsername()
        {
            string username = "";
            try {
                var apiClient = new ApiClient(Keychain, ProcessManager, TaskManager, Environment);
                var user = apiClient.GetCurrentUser();
                username = user.Login;
            } catch {
            }
            return username;
        }

        public override void IncrementApplicationMenuMenuItemCommandLine() => TaskManager.Run(base.IncrementApplicationMenuMenuItemCommandLine);
        public override void IncrementAuthenticationViewButtonAuthentication() => TaskManager.Run(base.IncrementAuthenticationViewButtonAuthentication);
        public override void IncrementBranchesViewButtonCheckoutLocalBranch() => TaskManager.Run(base.IncrementBranchesViewButtonCheckoutLocalBranch);
        public override void IncrementBranchesViewButtonCheckoutRemoteBranch() => TaskManager.Run(base.IncrementBranchesViewButtonCheckoutRemoteBranch);
        public override void IncrementBranchesViewButtonCreateBranch() => TaskManager.Run(base.IncrementBranchesViewButtonCreateBranch);
        public override void IncrementBranchesViewButtonDeleteBranch() => TaskManager.Run(base.IncrementBranchesViewButtonDeleteBranch);
        public override void IncrementChangesViewButtonCommit() => TaskManager.Run(base.IncrementChangesViewButtonCommit);
        public override void IncrementHistoryViewToolbarFetch() => TaskManager.Run(base.IncrementHistoryViewToolbarFetch);
        public override void IncrementHistoryViewToolbarPull() => TaskManager.Run(base.IncrementHistoryViewToolbarPull);
        public override void IncrementHistoryViewToolbarPush() => TaskManager.Run(base.IncrementHistoryViewToolbarPush);
        public override void IncrementNumberOfStartups() => TaskManager.Run(base.IncrementNumberOfStartups);
        public override void IncrementProjectsInitialized() => TaskManager.Run(base.IncrementProjectsInitialized);
        public override void IncrementPublishViewButtonPublish() => TaskManager.Run(base.IncrementPublishViewButtonPublish);
        public override void IncrementSettingsViewButtonLfsUnlock() => TaskManager.Run(base.IncrementSettingsViewButtonLfsUnlock);
        public override void IncrementUnityProjectViewContextLfsLock() => TaskManager.Run(base.IncrementUnityProjectViewContextLfsLock);
        public override void IncrementUnityProjectViewContextLfsUnlock() => TaskManager.Run(base.IncrementUnityProjectViewContextLfsUnlock);
        public override void UpdateLfsDiskUsage(int kilobytes) => TaskManager.Run(() => base.UpdateLfsDiskUsage(kilobytes));
        public override void UpdateRepoSize(int kilobytes) => TaskManager.Run(() => base.UpdateRepoSize(kilobytes));

        protected ITaskManager TaskManager { get; }
        protected IEnvironment Environment { get; }
        protected IGitClient GitClient { get; }
        public IProcessManager ProcessManager { get; }
        protected IKeychain Keychain { get; }
    }

    interface IUsageLoader
    {
        UsageStore Load(string userId);
        void Save(UsageStore store);
    }

    class UsageLoader : IUsageLoader
    {
        private readonly NPath path;

        public UsageLoader(NPath path)
        {
            this.path = path;
        }

        public UsageStore Load(string userId)
        {
            UsageStore result = null;
            string json = null;
            if (path.FileExists())
            {
                try
                {
                    json = path.ReadAllText(Encoding.UTF8);
                    result = json?.FromJson<UsageStore>(lowerCase: true);
                }
                catch (Exception ex)
                {
                    LogHelper.Instance.Warning(ex, "Error Loading Usage: {0}; Deleting File", path);
                    try
                    {
                        path.DeleteIfExists();
                    }
                    catch { }
                }
            }

            if (result == null)
                result = new UsageStore();

            if (String.IsNullOrEmpty(result.Model.Guid))
                result.Model.Guid = userId;

            return result;
        }

        public void Save(UsageStore store)
        {
            try
            {
                var json = store.ToJson(lowerCase: true);
                path.WriteAllText(json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Error(ex, "SaveUsage Error: \"{0}\"", path);
            }
        }
    }
}
