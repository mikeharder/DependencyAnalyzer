using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DependencyAnalyzer
{
    static class Util
    {
        public static void WriteLine(string value) => Console.WriteLine(value);

        private static (Process Process, StringBuilder OutputBuilder, StringBuilder ErrorBuilder) StartProcess(
            string filename, string arguments, string workingDirectory, IDictionary<string, string> environment = null)
        {
            Util.WriteLine($"{filename} {arguments}");

            var process = new Process()
            {
                StartInfo =
                {
                    FileName = filename,
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory,
                },
            };

            if (environment != null)
            {
                foreach (var kvp in environment)
                {
                    process.StartInfo.Environment.Add(kvp);
                }
            }

            var outputBuilder = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                outputBuilder.AppendLine(e.Data);
                Util.WriteLine(e.Data);
            };

            var errorBuilder = new StringBuilder();
            process.ErrorDataReceived += (_, e) =>
            {
                errorBuilder.AppendLine(e.Data);
                Util.WriteLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return (process, outputBuilder, errorBuilder);
        }

        private static string WaitForExit(Process process, StringBuilder outputBuilder, StringBuilder errorBuilder,
            bool throwOnError = true)
        {
            process.WaitForExit();

            if (throwOnError && process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Command {process.StartInfo.FileName} {process.StartInfo.Arguments} returned exit code {process.ExitCode}");
            }

            return outputBuilder.ToString();
        }

        public static string RunProcess(string filename, string arguments, string workingDirectory,
            bool throwOnError = true, IDictionary<string, string> environment = null)
        {
            var p = StartProcess(filename, arguments, workingDirectory, environment: environment);
            return WaitForExit(p.Process, p.OutputBuilder, p.ErrorBuilder, throwOnError: throwOnError);
        }
    }
}
