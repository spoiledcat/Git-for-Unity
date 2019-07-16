using System.Threading;
using Unity.VersionControl.Git.NiceIO;

namespace Unity.VersionControl.Git
{
    class WindowsDiskUsageTask : ProcessTask<long>
    {
        private readonly string arguments;

        public WindowsDiskUsageTask(NPath directory, CancellationToken token)
            : base(token, outputProcessor: new WindowsDiskUsageOutputProcessor())
        {
            Name = "cmd";
            arguments = string.Format("/c dir /a/s \"{0}\"", directory);
        }

        public override string ProcessName { get { return Name; } }
        public override string ProcessArguments { get { return arguments; } }
        public override TaskAffinity Affinity { get { return TaskAffinity.Concurrent; } }
        public override string Message { get; set; } = "Getting directory size...";
    }
}
