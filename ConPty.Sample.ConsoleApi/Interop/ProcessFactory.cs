using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ConPty.Sample.ConsoleApi.Interop.Definitions;

namespace ConPty.Sample.ConsoleApi.Interop
{
    internal static class ProcessFactory
    {
        public static Process Start(string applicationName, string commandLine, string workingDirectory, IEnumerable<KeyValuePair<string, string>> environmentVariables, IntPtr attributes, IntPtr hPC)
        {
            var startupInfo = ConfigureProcessThread(hPC, attributes);
            var processInfo = RunProcess(ref startupInfo, applicationName, commandLine, workingDirectory, environmentVariables);
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

        public static IEnumerable<KeyValuePair<string, string>> MergeAdditionalEnvironmentVariables(IEnumerable<KeyValuePair<string, string>> additionalEnvironmentVariables)
        {
            if (additionalEnvironmentVariables == null || !additionalEnvironmentVariables.Any())
            {
                return null;
            }

            var environmentVariables = new Dictionary<string, string>();

            var currentEnvVars = System.Environment.GetEnvironmentVariables();

            foreach (DictionaryEntry entry in currentEnvVars)
            {
                environmentVariables[entry.Key.ToString()] = entry.Value.ToString();
            }

            foreach (var pair in additionalEnvironmentVariables)
            {
                environmentVariables[pair.Key] = pair.Value;
            }

            return environmentVariables;
        }

        public static string CreateEnvironmentBlock(IEnumerable<KeyValuePair<string, string>> environmentVariables)
        {
            if (environmentVariables == null || !environmentVariables.Any())
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (var kvp in environmentVariables)
            {
                sb.Append($"{kvp.Key}={kvp.Value}\0");
            }
            sb.Append('\0'); // Double null terminator

            return sb.ToString();
        }

        private static ProcessInfo RunProcess(ref StartInfoExtended sInfoEx, string applicationName, string commandLine, string workingDirectory, System.Collections.Specialized.StringDictionary environmentVariables)
        {
            var envEnum = environmentVariables
                .Cast<System.Collections.DictionaryEntry>()
                .Select(entry => new KeyValuePair<string, string>((string)entry.Key, (string)entry.Value));

            return RunProcess(ref sInfoEx, applicationName, commandLine, workingDirectory, envEnum);
        }

        private static ProcessInfo RunProcess(ref StartInfoExtended sInfoEx, string applicationName, string commandLine, string workingDirectory, IEnumerable<KeyValuePair<string, string>> environmentVariables)
        {
            string environmentStr = CreateEnvironmentBlock(environmentVariables);
            IntPtr environmentPtr = IntPtr.Zero;

            if (!string.IsNullOrWhiteSpace(environmentStr))
            {
                // Convert to unmanaged memory
                environmentPtr = Marshal.StringToHGlobalAnsi(environmentStr);
            }

            var pInfo = RunProcess(ref sInfoEx, applicationName, commandLine, workingDirectory, environmentPtr);

            if (environmentPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(environmentPtr);
            }

            return pInfo;
        }

        private static ProcessInfo RunProcess(ref StartInfoExtended sInfoEx, string applicationName, string commandLine, string workingDirectory, IntPtr environmentPtr)
        {
            int securityAttributeSize = Marshal.SizeOf<SecurityAttributes>();
            var pSec = new SecurityAttributes { nLength = securityAttributeSize };
            var tSec = new SecurityAttributes { nLength = securityAttributeSize };
            var success = ProcessApi.CreateProcess(
                lpApplicationName: string.IsNullOrWhiteSpace(applicationName) ? null : applicationName,
                lpCommandLine: string.IsNullOrWhiteSpace(commandLine) ? null : commandLine,
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

}
