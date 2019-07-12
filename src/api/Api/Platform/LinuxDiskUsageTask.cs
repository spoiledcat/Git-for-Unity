using System.Threading;
using Unity.VersionControl.Git.NiceIO;

namespace Unity.VersionControl.Git
{
    class LinuxDiskUsageTask : SimpleProcessTask<int>
    {
        public LinuxDiskUsageTask(NPath directory, CancellationToken? token = null)
            : base("du" + UnityEnvironment.ExecutableExtension,
                string.Format("-sH \"{0}\"", directory),
                new LinuxDiskUsageOutputProcessor(),
                token: token)
        {
        }

        public override TaskAffinity Affinity => TaskAffinity.Concurrent;
        public override string Message { get; set; } = "Getting directory size...";
    }
}
