using System;
using System.Threading;

namespace Unity.VersionControl.Git
{
    using NiceIO;

    public class GitProcessTask : ProcessTask<string>
    {
        public GitProcessTask(string arguments,
            IOutputProcessor<string> processor = null,
            CancellationToken? token = null,
            IProcessEnvironment gitProcessEnvironment = null
        ) : base(token ?? ProcessManager.Instance.CancellationToken,
            null, arguments, processor,
            (gitProcessEnvironment ?? GitProcessEnvironment.Instance))
        {
            if (ProcessEnvironment == null)
                throw new InvalidOperationException("You need to initialize a GitProcessEnvironment instance");
            base.ProcessName = ((IGitEnvironment)ProcessEnvironment.Environment).GitExecutablePath;
        }
    }

    public class GitProcessTask<T> : ProcessTask<T>
	{
		public GitProcessTask(string arguments = null,
			IOutputProcessor<T> processor = null,
            CancellationToken? token = null,
            IProcessEnvironment gitProcessEnvironment = null)
			: base(token ?? ProcessManager.Instance.CancellationToken, null, arguments,
                processor, (gitProcessEnvironment ?? GitProcessEnvironment.Instance))
		{
			if (ProcessEnvironment == null)
				throw new InvalidOperationException("You need to initialize a GitProcessEnvironment instance");
            base.ProcessName = ((IGitEnvironment)ProcessEnvironment.Environment).GitExecutablePath;
		}

        public GitProcessTask(string arguments,
            Func<string, T> processor,
            CancellationToken? token = null,
            IProcessEnvironment gitProcessEnvironment = null)
            : base(token ?? ProcessManager.Instance.CancellationToken, null, arguments,
                new BaseOutputProcessor<T>((string line, out T result) => Parse(line, out result, processor)),
                (gitProcessEnvironment ?? GitProcessEnvironment.Instance))
        {
            if (ProcessEnvironment == null)
                throw new InvalidOperationException("You need to initialize a GitProcessEnvironment instance");
            base.ProcessName = ((IGitEnvironment)ProcessEnvironment.Environment).GitExecutablePath;
        }

        private static bool Parse(string line, out T result, Func<string, T> processor)
        {
            result = default(T);
            if (line == null) return false;
            result = processor(line);
            return true;
        }

    }

    /// <summary>
    /// Run a simple git command. If you pass in a processManager instance, you don't have to call Configure() on this task
    /// </summary>
    public class SimpleGitProcessTask : GitProcessTask<string>
	{
		public SimpleGitProcessTask(
			string arguments,
			IProcessManager processManager = null,
            IProcessEnvironment gitProcessEnvironment = null
		) : base(arguments)
		{
            processManager?.Configure(this, (gitProcessEnvironment ?? GitProcessEnvironment.Instance).DefaultWorkingDirectory);
        }
    }

    /// <summary>
    /// Run a simple git command, returning the first content line. If you pass in a processManager instance, you don't have to call Configure() on this task
    /// </summary>
    public class FirstNonNullLineGitProcessTask : GitProcessTask<string>
	{
		public FirstNonNullLineGitProcessTask(
			string arguments,
			CancellationToken? token = null,
			IProcessManager processManager = null,
            IProcessEnvironment gitProcessEnvironment = null
		)
			: base(arguments, new FirstNonNullLineOutputProcessor<string>(), token, gitProcessEnvironment)
		{
            processManager?.Configure(this, (gitProcessEnvironment ?? GitProcessEnvironment.Instance).DefaultWorkingDirectory);
		}
	}

}

