using System.Threading;

namespace Unity.VersionControl.Git
{
	using NiceIO;

	public class FindExecTask : ProcessTask<NPath>
	{
		private readonly string arguments;

		public FindExecTask(string executable, IBaseEnvironment environment, CancellationToken token)
			 : base(token, outputProcessor: new FirstNonNullLineOutputProcessor<NPath>(line => new NPath(line)))
		{
			Name = environment.IsWindows ? "where" : "which";
			arguments = executable;
		}

		public override string ProcessName => Name;
		public override string ProcessArguments => arguments;
		public override TaskAffinity Affinity => TaskAffinity.Concurrent;
	}
}
