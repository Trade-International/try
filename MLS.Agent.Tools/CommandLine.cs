using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Clockwise;
using Pocket;
using Recipes;
using static Pocket.Logger<MLS.Agent.Tools.CommandLine>;

namespace MLS.Agent.Tools
{
    public static class CommandLine
    {
        public static Task<CommandLineResult> Execute(
            FileInfo exePath,
            string args,
            DirectoryInfo workingDir = null,
            TimeBudget budget = null) =>
            Execute(exePath.FullName,
                    args,
                    workingDir,
                    budget);

        public static async Task<CommandLineResult> Execute(
            string command,
            string args,
            DirectoryInfo workingDir = null,
            TimeBudget budget = null)
        {
            args = args ?? "";
            budget = budget ?? TimeBudget.Unlimited();

            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            using (var operation = CheckBudgetAndStartConfirmationLogger(command, args, budget))
            using (var process = StartProcess(
                command,
                args,
                workingDir,
                output: data =>
                {
                    stdOut.AppendLine(data);
                    operation.Info("{data}", data);
                },
                error: data =>
                {
                    stdErr.AppendLine(data);
                    operation.Error("{data}", args: data);
                }))
            {
                (int exitCode, Exception exception) =
                    await Task.Run(() =>
                              {
                                  process.WaitForExit();

                                  operation.Succeed(
                                      "{command} {args} exited with {code}",
                                      command,
                                      args,
                                      process.ExitCode);

                                  return (process.ExitCode, (Exception) null);
                              })
                              .CancelIfExceeds(
                                  budget,
                                  ifCancelled: () =>
                                  {
                                      var ex = new TimeBudgetExceededException(budget);

                                      //FIX: (Execute)
                                      //Task.Run(() =>
                                      //{
                                      //    if (!process.HasExited)
                                      //    {
                                      //        process.Kill();
                                      //    }
                                      //}).DontAwait();

                                      if (!process.HasExited)
                                      {
                                          operation.Fail(ex);

                                          return (124, ex); // like the Linux timeout command 
                                      }
                                      return (process.ExitCode, (Exception)null);
                                  });

                return new CommandLineResult(
                    exitCode: exitCode,
                    output: stdOut.Replace("\r\n", "\n").ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries),
                    error: stdErr.Replace("\r\n", "\n").ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        public static Process StartProcess(
            string command,
            string args,
            DirectoryInfo workingDir,
            Action<string> output = null,
            Action<string> error = null)
        {
            args = args ?? "";

            var process = new Process
            {
                StartInfo =
                {
                    Arguments = args,
                    FileName = command,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    WorkingDirectory = workingDir?.FullName
                }
            };

            if (output != null)
            {
                process.OutputDataReceived += (sender, eventArgs) =>
                {
                    if (eventArgs.Data != null)
                    {
                        output(eventArgs.Data);
                    }
                };
            }

            if (error != null)
            {
                process.ErrorDataReceived += (sender, eventArgs) =>
                {
                    if (eventArgs.Data != null)
                    {
                        error(eventArgs.Data);
                    }
                };
            }

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
        }

        internal static string AppendArgs(this string initial, string append = null) =>
            string.IsNullOrWhiteSpace(append)
                ? initial
                : $"{initial} {append}";

        private static ConfirmationLogger CheckBudgetAndStartConfirmationLogger(
            object command,
            string args,
            TimeBudget budget,
            [CallerMemberName] string operationName = null)
        {
            budget.RecordEntryAndThrowIfBudgetExceeded($"Execute ({command} {args})");

            return new ConfirmationLogger(
                operationName: operationName,
                category: Log.Category,
                message: "Invoking {command} {args}",
                args: new[] { command, args },
                logOnStart: true,
                exitArgs: () => new[] { ( "budget", (object) budget ) });
        }
    }
}
