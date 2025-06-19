using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ConPty.Sample.ConsoleApi;

namespace ConPty.Sample.Terminal.Forms
{
    public partial class TerminalForm : Form
    {
        private ConsoleApi.Terminal terminal;

        private CancellationTokenSource copyConsoleCts;
        private Task copyConsoleTask;

        private WaitHandle terminalWaitHandle;
        private Task terminalWaitTask;

        public TerminalForm()
        {
            InitializeComponent();
            InitialiseTerminal();
        }

        private void InitialiseTerminal()
        {
            //console = new NativeConsole(false);
            terminal = new ConsoleApi.Terminal();
            terminal.Start(@"ping localhost", 126, 32);
            terminalWaitHandle = terminal.BuildWaitHandler();

            copyConsoleCts = new CancellationTokenSource();
            copyConsoleTask = Task.Run(() => CopyConsoleToWindow(copyConsoleCts.Token), copyConsoleCts.Token);

            // Start the terminal wait task
            terminalWaitTask = Task.Run(() => WaitForTerminalExit(), CancellationToken.None);
        }

        private void WaitForTerminalExit()
        {
            // Wait for the terminal process to exit
            terminalWaitHandle.WaitOne();

            Task.Delay(1000).Wait();

            // Cancel the CopyConsoleToWindow task
            copyConsoleCts?.Cancel();

            // Display a message in the output window
            string exitMessage = Environment.NewLine + "Terminal process exited." + Environment.NewLine;
            OutputCharacters(exitMessage, exitMessage.Length);
        }

        private void CopyConsoleToWindow(CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<char>.Shared.Rent(1024);

            try
            {
                using (StreamReader reader = new StreamReader(terminal.Output))
                using (StreamWriter fileWriter = new StreamWriter(File.Open("output.txt", FileMode.Create, FileAccess.Write)) { AutoFlush = true })
                {
                    while (true)
                    {
                        // Check for cancellation
                        if (cancellationToken.IsCancellationRequested)
                        {
                            terminal.KillConsole();
                            return;
                        }

                        int readed = reader.Read(buffer, 0, buffer.Length);

                        if (readed == 0)
                        {
                            // No more data to read, wait for a while before checking again
                            Task.Delay(100, cancellationToken).Wait(cancellationToken);
                            continue;
                        }
                        else
                        {
                            fileWriter.Write(buffer, 0, readed);
                            OutputCharacters(buffer, readed, cancellationToken);
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                /* Pseudo terminal is terminated. */
            }
            catch (Exception exception)
            {
                string message = Environment.NewLine + "Error: " + exception.Message;
                OutputCharacters(message.ToCharArray(), message.Length, cancellationToken);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Signal cancellation and wait for the tasks to finish
            if (copyConsoleCts != null)
            {
                copyConsoleCts.Cancel();
                try
                {
                    copyConsoleTask?.Wait(1000); // Wait up to 1 second for task to finish
                    terminalWaitTask?.Wait(1000);
                }
                catch (AggregateException) { /* Ignore cancellation exceptions */ }
                copyConsoleCts.Dispose();
            }

            terminal?.Dispose();
            //console?.Dispose();
        }

        private void OutputCharacters(char[] buffer, int length, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            string text = new(buffer.Take(length).ToArray());
            text = Ecma48Stripper.Strip(text);

            if (InvokeRequired)
            {
                Invoke(new Action(() => { tbOutput.Text += text; }));
            }
            else
            {
                tbOutput.Text += text;
            }
        }

        // Overload OutputCharacters for string input for convenience
        private void OutputCharacters(string text, int length)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => { tbOutput.Text += text.Substring(0, length); }));
            }
            else
            {
                tbOutput.Text += text.Substring(0, length);
            }
        }
    }
}
