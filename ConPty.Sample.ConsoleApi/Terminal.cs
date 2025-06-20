using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ConPty.Sample.ConsoleApi.Interop;
using Microsoft.Win32.SafeHandles;

namespace ConPty.Sample.ConsoleApi
{
    public class Terminal : IDisposable
    {
        private Pipe input;
        private Pipe output;
        private PseudoConsole console;
        private Process process;
        private bool disposed;

        public Terminal()
        {
            ConPtyFeature.ThrowIfVirtualTerminalIsNotEnabled();

            if (Interop.ConsoleApi.GetConsoleWindow() != IntPtr.Zero)
            {
                ConPtyFeature.TryEnableVirtualTerminalConsoleSequenceProcessing();
            }
        }

        ~Terminal()
        {
            Dispose(false);
        }

        public FileStream Input { get; private set; }

        public FileStream Output { get; private set; }

        /// <summary>
        /// Launches a new process using the Windows CreateProcess API.
        /// </summary>
        /// <param name="applicationName">
        /// Optional full path to the executable. If provided, must be a valid path; 
        /// the system will not search the PATH environment variable.
        /// </param>
        /// <param name="commandLine">
        /// Required command line string. Must begin with the executable name (argv[0]), 
        /// regardless of whether <paramref name="applicationName"/> is specified.
        /// </param>
        /// <remarks>
        /// For parameter behavior and security considerations, see:
        /// https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessa#parameters
        /// </remarks>
        public void Start(string applicationName, string commandLine, string workingDirectory, IEnumerable<KeyValuePair<string, string>> additionalEnvironmentVariables, short consoleWidth, short consoleHeight)
        {
            input = new Pipe();
            output = new Pipe();

            console = PseudoConsole.Create(input.Read, output.Write, consoleWidth, consoleHeight);

            if (additionalEnvironmentVariables == null || !additionalEnvironmentVariables.Any())
            {
                process = ProcessFactory.Start(applicationName, commandLine, workingDirectory, null, PseudoConsole.PseudoConsoleThreadAttribute, console.Handle);
            }
            else
            {
                var environmentVariables = ProcessFactory.MergeAdditionalEnvironmentVariables(additionalEnvironmentVariables);
                process = ProcessFactory.Start(applicationName, commandLine, workingDirectory, environmentVariables, PseudoConsole.PseudoConsoleThreadAttribute, console.Handle);
            }

            Input = new FileStream(input.Write, FileAccess.Write);
            Output = new FileStream(output.Read, FileAccess.Read);
        }

        public void KillConsole()
        {
            console?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public WaitHandle BuildWaitHandler()
        {
            return new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(process.ProcessInfo.hProcess, ownsHandle: false)
            };
        }

        public void WaitToExit()
        {
            BuildWaitHandler().WaitOne(Timeout.Infinite);
        }

        public uint GetExitCode()
        {
            if (process == null)
            {
                throw new InvalidOperationException("Process has not been started.");
            }

            if (!ProcessApi.GetExitCodeProcess(process.ProcessInfo.hProcess, out uint exitCode))
            {
                throw InteropException.CreateWithInnerHResultException("Could not get exit code of the process.");
            }

            return exitCode;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            process?.Dispose();
            console?.Dispose();

            if (disposing)
            {
                Input?.Dispose();
                Output?.Dispose();
            }

            input?.Dispose();
            output?.Dispose();
        }
    }
}
