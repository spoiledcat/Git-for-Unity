using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Unity.VersionControl.Git
{
	using Logging;
	using NiceIO;

	public static class ProcessTaskExtensions
	{
		public static T Configure<T>(this T task, IProcessManager processManager,
			 NPath? workingDirectory = null,
			 bool withInput = false)
			 where T : IProcessTask
		{
			return processManager.Configure(task, workingDirectory, withInput);
		}

		public static void Configure(this ProcessStartInfo psi, IProcessEnvironment processEnvironment, NPath? workingDirectory = null)
		{
			processEnvironment.Configure(psi, workingDirectory);
		}
	}

	public interface IProcessTask : IProcess
	{
		IProcessEnvironment ProcessEnvironment { get; }
	}

	public interface IProcessTask<T> : ITask<T>, IProcessTask
	{
		void Configure(ProcessStartInfo psi, IOutputProcessor<T> processor);
	}

	public interface IProcessTask<TData, T> : ITask<TData, T>, IProcessTask
	{
		void Configure(ProcessStartInfo psi, IOutputProcessor<TData, T> processor);
	}

	class ProcessWrapper
	{
		private readonly string taskName;
		private readonly IOutputProcessor outputProcessor;
		private readonly Action onStart;
		private readonly Action onEnd;
		private readonly Action<Exception, string> onError;
		private readonly CancellationToken token;
		private readonly List<string> errors = new List<string>();

		public Process Process { get; }
		public StreamWriter Input { get; private set; }

		private ILogging logger;
		protected ILogging Logger { get { return logger = logger ?? LogHelper.GetLogger(GetType()); } }

		public ProcessWrapper(string taskName, Process process, IOutputProcessor outputProcessor,
			 Action onStart, Action onEnd, Action<Exception, string> onError,
			 CancellationToken token)
		{
			this.taskName = taskName;
			this.outputProcessor = outputProcessor;
			this.onStart = onStart;
			this.onEnd = onEnd;
			this.onError = onError;
			this.token = token;
			this.Process = process;
		}

		public void Run()
		{
			DateTimeOffset lastOutput = DateTimeOffset.UtcNow;
			Exception thrownException = null;
			var gotOutput = new AutoResetEvent(false);
			if (Process.StartInfo.RedirectStandardError)
			{
				Process.ErrorDataReceived += (s, e) =>
				{
						 //if (e.Data != null)
						 //{
						 //    Logger.Trace("ErrorData \"" + (e.Data == null ? "'null'" : e.Data) + "\"");
						 //}

						 lastOutput = DateTimeOffset.UtcNow;
					gotOutput.Set();
					if (e.Data != null)
					{
						var line = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(e.Data));
						errors.Add(line.TrimEnd('\r', '\n'));
						Logger.Trace(line);
					}
				};
			}

			if (Process.StartInfo.RedirectStandardOutput)
			{
				Process.OutputDataReceived += (s, e) =>
				{
					try
					{
						lastOutput = DateTimeOffset.UtcNow;
						gotOutput.Set();
						if (e.Data != null)
						{
							var line = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(e.Data));
							outputProcessor.Process(line.TrimEnd('\r', '\n'));
						}
						else
							outputProcessor.Process(null);
					}
					catch (Exception ex)
					{
						Logger.Error(ex);
					}
				};
			}

			try
			{
				Logger.Trace($"Running '{Process.StartInfo.FileName} {Process.StartInfo.Arguments}'");

				token.ThrowIfCancellationRequested();
				Process.Start();

				if (Process.StartInfo.RedirectStandardInput)
					Input = new StreamWriter(Process.StandardInput.BaseStream, new UTF8Encoding(false));
				if (Process.StartInfo.RedirectStandardError)
					Process.BeginErrorReadLine();
				if (Process.StartInfo.RedirectStandardOutput)
					Process.BeginOutputReadLine();

				onStart?.Invoke();

				if (Process.StartInfo.CreateNoWindow)
				{
					bool done = false;
					while (!done)
					{
						var exited = WaitForExit(500);
						if (exited)
						{
							// process is done and we haven't seen output, we're done
							done = !gotOutput.WaitOne(100);
						}
						else if (token.IsCancellationRequested /* || (taskName.Contains("git lfs") && lastOutput.AddMilliseconds(ApplicationConfiguration.DefaultGitTimeout) < DateTimeOffset.UtcNow) */)
						// if we're exiting or we haven't had output for a while
						{
							Stop(true);
							token.ThrowIfCancellationRequested();
							throw new ProcessException(-2, "Process timed out");
						}
					}

					if (Process.ExitCode != 0 && errors.Count > 0)
					{
						thrownException = new ProcessException(Process.ExitCode, string.Join(Environment.NewLine, errors.ToArray()));
					}
				}
			}
			catch (Exception ex)
			{
				var errorCode = -42;
				if (ex is Win32Exception)
					errorCode = ((Win32Exception)ex).NativeErrorCode;

				StringBuilder sb = new StringBuilder();
				sb.AppendLine($"Error code {errorCode}");
				sb.AppendLine(ex.Message);
				if (Process.StartInfo.Arguments.Contains("-credential"))
					sb.AppendLine($"'{Process.StartInfo.FileName} {taskName}'");
				else
					sb.AppendLine($"'{Process.StartInfo.FileName} {Process.StartInfo.Arguments}'");
				if (errorCode == 2)
					sb.AppendLine("The system cannot find the file specified.");
				sb.AppendLine($"Working directory: {Process.StartInfo.WorkingDirectory}");
				foreach (string env in Process.StartInfo.EnvironmentVariables.Keys)
				{
					sb.AppendFormat("{0}:{1}", env, Process.StartInfo.EnvironmentVariables[env]);
					sb.AppendLine();
				}
				thrownException = new ProcessException(errorCode, sb.ToString(), ex);
			}

			if (thrownException != null || errors.Count > 0)
				onError?.Invoke(thrownException, string.Join(Environment.NewLine, errors.ToArray()));
			onEnd?.Invoke();
		}

		public void Stop(bool dontWait = false)
		{
			try
			{
				if (Process.StartInfo.RedirectStandardError)
					Process.CancelErrorRead();
				if (Process.StartInfo.RedirectStandardOutput)
					Process.CancelOutputRead();
				if (!Process.HasExited && Process.StartInfo.RedirectStandardInput)
					Input.WriteLine("\x3");
			}
			catch
			{ }

			try
			{

				if (!Process.HasExited)
				{
					Process.Kill();
				}

				if (!dontWait)
				{
					bool waitSucceeded = Process.WaitForExit(500);
					if (waitSucceeded)
					{
						Process.Close();
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Trace(ex);
			}
		}

		private bool WaitForExit(int milliseconds)
		{
			//Logger.Debug("WaitForExit - time: {0}ms", milliseconds);

			// Workaround for a bug in which some data may still be processed AFTER this method returns true, thus losing the data.
			// http://connect.microsoft.com/VisualStudio/feedback/details/272125/waitforexit-and-waitforexit-int32-provide-different-and-undocumented-implementations
			bool waitSucceeded = Process.WaitForExit(milliseconds);
			if (waitSucceeded)
			{
				Process.WaitForExit();
			}
			return waitSucceeded;
		}
	}

	/// <summary>
	///
	/// </summary>
	/// <typeparam name="T">The type of the results. If it's a List<> or similar, then specify the full List<> type here and the inner type of the List in <typeparam name="TData"/>
	/// <typeparam name="TData">If <typeparam name="TData"/> is a list or similar, then specify its inner type here</typeparam>
	public class ProcessTask<T> : TaskBase<T>, IProcessTask<T>
	{
		private IOutputProcessor<T> outputProcessor;
		private ProcessWrapper wrapper;

		public event Action<string> OnErrorData;
		public event Action<IProcess> OnStartProcess;
		public event Action<IProcess> OnEndProcess;

		private Exception thrownException = null;

        /// <summary>
        /// Runs a Process with the passed arguments
        /// </summary>
        /// <param name="token"></param>
        /// <param name="executable"></param>
        /// <param name="arguments"></param>
        /// <param name="outputProcessor"></param>
        /// <param name="processEnvironment"></param>
        public ProcessTask(CancellationToken token,
			string executable = null,
			string arguments = null,
			IOutputProcessor<T> outputProcessor = null,
			IProcessEnvironment processEnvironment = null)
			 : base(token)
		{
			Guard.ArgumentNotNull(token, nameof(token));

			this.outputProcessor = outputProcessor;
			ProcessEnvironment = processEnvironment ?? ProcessManager.Instance.DefaultProcessEnvironment;
			ProcessArguments = arguments;
			ProcessName = executable;
		}

		public virtual void Configure(ProcessStartInfo psi)
		{
			Guard.ArgumentNotNull(psi, "psi");

			ConfigureOutputProcessor();

			Guard.NotNull(this, outputProcessor, nameof(outputProcessor));
			Process = new Process { StartInfo = psi, EnableRaisingEvents = true };
			ProcessName = psi.FileName;
			Name = ProcessArguments;
		}

		public virtual void Configure(ProcessStartInfo psi, IOutputProcessor<T> processor)
		{
			outputProcessor = processor ?? outputProcessor;
			ConfigureOutputProcessor();

			Guard.NotNull(this, outputProcessor, nameof(outputProcessor));

			Process = new Process { StartInfo = psi, EnableRaisingEvents = true };
			ProcessName = psi.FileName;
			Name = ProcessArguments;
		}

		public void Configure(Process existingProcess)
		{
			Guard.ArgumentNotNull(existingProcess, "existingProcess");

			ConfigureOutputProcessor();

			Guard.NotNull(this, outputProcessor, nameof(outputProcessor));

			Process = existingProcess;
			ProcessName = existingProcess.StartInfo.FileName;
			Name = ProcessArguments;
		}

		public void Stop()
		{
			wrapper?.Stop();
		}

		protected override void RaiseOnEnd()
		{
			base.RaiseOnEnd();
			OnEndProcess?.Invoke(this);
		}

		protected virtual void ConfigureOutputProcessor()
		{
		}

		protected override T RunWithReturn(bool success)
		{
			var result = base.RunWithReturn(success);

			wrapper = new ProcessWrapper(Name, Process, outputProcessor,
				 () => OnStartProcess?.Invoke(this),
				 () =>
				 {
					 try
					 {
						 if (outputProcessor != null)
							 result = outputProcessor.Result;

						 if (typeof(T) == typeof(string) && result == null && !Process.StartInfo.CreateNoWindow)
							 result = (T)(object)"Process running";

						 if (!String.IsNullOrEmpty(Errors))
							 OnErrorData?.Invoke(Errors);
					 }
					 catch (Exception ex)
					 {
						 if (thrownException == null)
							 thrownException = new ProcessException(ex.Message, ex);
						 else
							 thrownException = new ProcessException(thrownException.GetExceptionMessage(), ex);
					 }

					 if (thrownException != null && !RaiseFaultHandlers(thrownException))
						 ThrownException.Rethrow();
				 },
				 (ex, error) =>
				 {
					 thrownException = ex;
					 Errors = error;
				 },
				 Token);

			wrapper.Run();

			return result;
		}

		public override string ToString()
		{
			return $"{Task?.Id ?? -1} {Name} {GetType()} {ProcessName} {ProcessArguments}";
		}

		public IProcessEnvironment ProcessEnvironment { get; private set; }
		public Process Process { get; set; }
		public int ProcessId { get { return Process.Id; } }
		public override bool Successful { get { return base.Successful && Process.ExitCode == 0; } }
		public StreamWriter StandardInput { get { return wrapper?.Input; } }
		public virtual string ProcessName { get; protected set; }
		public virtual string ProcessArguments { get; protected set; }
	}

	public class ProcessTaskWithListOutput<T> : DataTaskBase<T, List<T>>, IProcessTask<T, List<T>>
	{
		private IOutputProcessor<T, List<T>> outputProcessor;
		private Exception thrownException = null;
		private ProcessWrapper wrapper;

		public event Action<string> OnErrorData;
		public event Action<IProcess> OnStartProcess;
		public event Action<IProcess> OnEndProcess;

		public ProcessTaskWithListOutput(CancellationToken token,
			string executable = null,
			string arguments = null,
			IOutputProcessor<T, List<T>> outputProcessor = null,
			IProcessEnvironment processEnvironment = null)
			 : base(token)
		{

			Guard.ArgumentNotNull(token, nameof(token));

			this.outputProcessor = outputProcessor;
			ProcessEnvironment = processEnvironment ?? ProcessManager.Instance.DefaultProcessEnvironment;
			ProcessArguments = arguments;
			ProcessName = executable;
		}

		public virtual void Configure(ProcessStartInfo psi)
		{
			Guard.ArgumentNotNull(psi, "psi");

			ConfigureOutputProcessor();

			Guard.NotNull(this, outputProcessor, nameof(outputProcessor));

			Process = new Process { StartInfo = psi, EnableRaisingEvents = true };
			ProcessName = psi.FileName;
		}

		public void Configure(Process existingProcess)
		{
			Guard.ArgumentNotNull(existingProcess, "existingProcess");

			ConfigureOutputProcessor();
			Guard.NotNull(this, outputProcessor, nameof(outputProcessor));
			Process = existingProcess;
			ProcessName = existingProcess.StartInfo.FileName;
		}

		public virtual void Configure(ProcessStartInfo psi, IOutputProcessor<T, List<T>> processor)
		{
			Guard.ArgumentNotNull(psi, "psi");
			Guard.ArgumentNotNull(processor, "processor");

			outputProcessor = processor ?? outputProcessor;
			ConfigureOutputProcessor();
			Process = new Process { StartInfo = psi, EnableRaisingEvents = true };
			ProcessName = psi.FileName;
		}

		public void Stop()
		{
			wrapper?.Stop();
		}

		protected override void RaiseOnEnd()
		{
			base.RaiseOnEnd();
			OnEndProcess?.Invoke(this);
		}

		protected virtual void ConfigureOutputProcessor()
		{
			if (outputProcessor == null && (typeof(T) != typeof(string)))
			{
				throw new InvalidOperationException("ProcessTask without an output processor must be defined as IProcessTask<string>");
			}
			outputProcessor.OnEntry += x => RaiseOnData(x);
		}

		protected override List<T> RunWithReturn(bool success)
		{
			var result = base.RunWithReturn(success);

			wrapper = new ProcessWrapper(Name, Process, outputProcessor,
				 () => OnStartProcess?.Invoke(this),
				 () =>
				 {
					 try
					 {
						 if (outputProcessor != null)
							 result = outputProcessor.Result;
						 if (result == null)
							 result = new List<T>();

						 if (!String.IsNullOrEmpty(Errors))
							 OnErrorData?.Invoke(Errors);
					 }
					 catch (Exception ex)
					 {
						 if (thrownException == null)
							 thrownException = new ProcessException(ex.Message, ex);
						 else
							 thrownException = new ProcessException(thrownException.GetExceptionMessage(), ex);
					 }

					 if (thrownException != null && !RaiseFaultHandlers(thrownException))
						 ThrownException.Rethrow();
				 },
				 (ex, error) =>
				 {
					 thrownException = ex;
					 Errors = error;
				 },
				 Token);
			wrapper.Run();

			return result;
		}

		public override string ToString()
		{
			return $"{Task?.Id ?? -1} {Name} {GetType()} {ProcessName} {ProcessArguments}";
		}

		public IProcessEnvironment ProcessEnvironment { get; private set; }
		public Process Process { get; set; }
		public int ProcessId { get { return Process.Id; } }
		public override bool Successful { get { return base.Successful && Process.ExitCode == 0; } }
		public StreamWriter StandardInput { get { return wrapper?.Input; } }
		public virtual string ProcessName { get; protected set; }
		public virtual string ProcessArguments { get; }
	}

	public class FirstNonNullLineProcessTask : ProcessTask<string>
	{
		public FirstNonNullLineProcessTask(
			string executable, string arguments, NPath? workingDirectory = null,
			IProcessManager processManager = null,
			CancellationToken? token = null
		)
			: base((token ?? TaskManager.Instance.Token), executable, arguments, new FirstNonNullLineOutputProcessor<string>())
		{
			(processManager ?? ProcessManager.Instance).Configure(this, workingDirectory);
		}
	}

	public class SimpleProcessTask : SimpleProcessTask<string>
	{
		public SimpleProcessTask(
			string executable, string arguments,
			IOutputProcessor<string> processor = null,
            NPath? workingDirectory = null,
			IProcessManager processManager = null,
			CancellationToken? token = null
			)
			 : base(executable, arguments, processor, workingDirectory, processManager, token)
		{
		}
    }

	public class SimpleProcessTask<T> : ProcessTask<T>
	{
		public SimpleProcessTask(
			string executable, string arguments,
			Func<string, T> processor = null,
			NPath? workingDirectory = null,
			IProcessManager processManager = null,
			CancellationToken? token = null
		)
			 : base((token ?? TaskManager.Instance.Token),
					executable, arguments,
					new BaseOutputProcessor<T>((string line, out T result) => Parse(line, out result, processor)))
		{
			(processManager ?? ProcessManager.Instance).Configure(this, workingDirectory);
		}

        public SimpleProcessTask(
            string executable, string arguments,
            IOutputProcessor<T> processor = null,
            NPath? workingDirectory = null,
            IProcessManager processManager = null,
            CancellationToken? token = null
        )
            : base((token ?? TaskManager.Instance.Token), executable, arguments, processor)
        {
            (processManager ?? ProcessManager.Instance).Configure(this, workingDirectory);
        }

        private static bool Parse(string line, out T result, Func<string, T> processor)
        {
            result = default(T);
            if (line == null) return false;
            result = processor(line);
            return true;
        }
    }

	public class SimpleListProcessTask : ProcessTaskWithListOutput<string>
	{
		private readonly NPath fullPathToExecutable;
		private readonly string arguments;

		public SimpleListProcessTask(
			string executable, string arguments, NPath? workingDirectory = null,
			IOutputProcessor<string, List<string>> processor = null,
			IProcessManager processManager = null,
			CancellationToken? token = null
		)
			 : base((token ?? TaskManager.Instance.Token), executable, arguments,
					processor ?? new SimpleListOutputProcessor())
		{
			(processManager ?? ProcessManager.Instance).Configure(this, workingDirectory);
		}
	}
}
