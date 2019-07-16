using System.Text;
using System.Threading;
using Unity.VersionControl.Git.NiceIO;

namespace Unity.VersionControl.Git.Tasks
{
    public class GitUnlockTask : ProcessTask<string>
    {
        private const string TaskName = "git lfs unlock";
        private readonly string arguments;

        public GitUnlockTask(NPath path, bool force,
            CancellationToken token, IOutputProcessor<string> processor = null)
            : base(token, processor ?? new SimpleOutputProcessor())
        {
            Guard.ArgumentNotNullOrWhiteSpace(path, "path");

            Name = TaskName;
            var stringBuilder = new StringBuilder("unlock ");

            if (force)
            {
                stringBuilder.Append("--force ");
            }

            stringBuilder.Append("\"");
            stringBuilder.Append(path.ToString(SlashMode.Forward));
            stringBuilder.Append("\"");

            arguments = stringBuilder.ToString();
        }

        public override string ProcessArguments => arguments;
        public override TaskAffinity Affinity { get { return TaskAffinity.Exclusive; } }
        public override string Message { get; set; } = "Unlocking file...";

    }
}
