namespace MinCms.Test.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Touchstone.Core;

    public static partial class MinCmsSuites
    {
        /// <summary>Create a console-silent logging module for tests.</summary>
        private static LoggingModule QuietLogging()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            return logging;
        }

        /// <summary>Create a deterministic byte payload of the requested length.</summary>
        private static byte[] CreatePayload(int length)
        {
            byte[] data = new byte[length];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 251);
            }
            return data;
        }

        /// <summary>Build an asynchronous test case.</summary>
        private static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor(suiteId, caseId, displayName, body);
        }

        /// <summary>Build a synchronous test case.</summary>
        private static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Action body)
        {
            return new TestCaseDescriptor(suiteId, caseId, displayName, ct =>
            {
                body();
                return Task.CompletedTask;
            });
        }

        /// <summary>Build a synchronous test case that returns a Task from an async lambda.</summary>
        private static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Func<Task> body)
        {
            return new TestCaseDescriptor(suiteId, caseId, displayName, ct => body());
        }
    }
}
