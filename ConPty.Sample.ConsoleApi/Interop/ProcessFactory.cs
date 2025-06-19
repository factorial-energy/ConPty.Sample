using System;
using System.Runtime.InteropServices;
using System.Text;
using ConPty.Sample.ConsoleApi.Interop.Definitions;

namespace ConPty.Sample.ConsoleApi.Interop
{
    internal static class ProcessFactory
    {
        public static Process Start(string commandLine, string workingDirectory, IntPtr attributes, IntPtr hPC)
        {
            var startupInfo = ConfigureProcessThread(hPC, attributes);
            var processInfo = RunProcess(ref startupInfo, commandLine, workingDirectory, IntPtr.Zero);
            return new Process(startupInfo, processInfo);
        }

        private static StartInfoExtended ConfigureProcessThread(IntPtr hPC, IntPtr attributes = PseudoConsole.PseudoConsoleThreadAttribute)
        {
            // this method implements the behavior described in https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session#preparing-for-creation-of-the-child-process

            var lpSize = IntPtr.Zero;
            var success = ProcessApi.InitializeProcThreadAttributeList(
                lpAttributeList: IntPtr.Zero,
                dwAttributeCount: 1,
                dwFlags: 0,
                lpSize: ref lpSize
            );

            if (success || lpSize == IntPtr.Zero) // we're not expecting `success` here, we just want to get the calculated lpSize
            {
                throw InteropException.CreateWithInnerHResultException("Could not calculate the number of bytes for the attribute list.");
            }

            var startupInfo = new StartInfoExtended();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<StartInfoExtended>();
            startupInfo.lpAttributeList = Marshal.AllocHGlobal(lpSize);

            success = ProcessApi.InitializeProcThreadAttributeList(
                lpAttributeList: startupInfo.lpAttributeList,
                dwAttributeCount: 1,
                dwFlags: 0,
                lpSize: ref lpSize
            );

            if (!success)
            {
                throw InteropException.CreateWithInnerHResultException("Could not set up attribute list.");
            }

            success = ProcessApi.UpdateProcThreadAttribute(
                lpAttributeList: startupInfo.lpAttributeList,
                dwFlags: 0,
                attribute: attributes,
                lpValue: hPC,
                cbSize: (IntPtr)IntPtr.Size,
                lpPreviousValue: IntPtr.Zero,
                lpReturnSize: IntPtr.Zero
            );

            if (!success)
            {
                throw InteropException.CreateWithInnerHResultException("Could not set pseudoconsole thread attribute.");
            }

            return startupInfo;
        }

        private static ProcessInfo RunProcess(ref StartInfoExtended sInfoEx, string commandLine, string workingDirectory, IntPtr environmentPtr)
        {
            int securityAttributeSize = Marshal.SizeOf<SecurityAttributes>();
            var pSec = new SecurityAttributes { nLength = securityAttributeSize };
            var tSec = new SecurityAttributes { nLength = securityAttributeSize };
            var success = ProcessApi.CreateProcess(
                lpApplicationName: null,
                lpCommandLine: commandLine,
                lpProcessAttributes: ref pSec,
                lpThreadAttributes: ref tSec,
                bInheritHandles: false,
                dwCreationFlags: Constants.EXTENDED_STARTUPINFO_PRESENT,
                lpEnvironment: environmentPtr,
                lpCurrentDirectory: string.IsNullOrWhiteSpace(workingDirectory) ? null : workingDirectory,
                lpStartupInfo: ref sInfoEx,
                lpProcessInformation: out ProcessInfo pInfo
            );

            if (!success)
            {
                throw InteropException.CreateWithInnerHResultException("Could not create process.");
            }

            return pInfo;
        }

    }

    // Helper for environment block creation
    internal static class EnvironmentBlockHelper
    {
        public static IntPtr CreateEnvironmentBlock(System.Collections.Specialized.StringDictionary envVars)
        {
            var builder = new StringBuilder();
            foreach (System.Collections.DictionaryEntry entry in envVars)
            {
                builder.Append($"{entry.Key}={entry.Value}\0");
            }
            builder.Append('\0');
            var bytes = Encoding.Unicode.GetBytes(builder.ToString() + 2); // double null-terminated
            IntPtr envBlock = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, envBlock, bytes.Length);
            return envBlock;
        }
    }
}
