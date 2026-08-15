namespace MinCms.Test.Shared.Support
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal assertion helpers for Touchstone test cases.
    /// A failed assertion throws; Touchstone treats any thrown exception as a failing case.
    /// </summary>
    public static class Check
    {
        /// <summary>Assert that a condition is true.</summary>
        public static void True(bool condition, string message = null)
        {
            if (!condition)
                throw new TouchstoneAssertException(message ?? "Expected condition to be true but it was false.");
        }

        /// <summary>Assert that a condition is false.</summary>
        public static void False(bool condition, string message = null)
        {
            if (condition)
                throw new TouchstoneAssertException(message ?? "Expected condition to be false but it was true.");
        }

        /// <summary>Assert equality using the default equality comparer.</summary>
        public static void Equal<T>(T expected, T actual, string message = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new TouchstoneAssertException(
                    (message ?? "Values are not equal.")
                    + " Expected=[" + Format(expected) + "] Actual=[" + Format(actual) + "]");
        }

        /// <summary>Assert inequality using the default equality comparer.</summary>
        public static void NotEqual<T>(T notExpected, T actual, string message = null)
        {
            if (EqualityComparer<T>.Default.Equals(notExpected, actual))
                throw new TouchstoneAssertException(
                    (message ?? "Values should not be equal.") + " Value=[" + Format(actual) + "]");
        }

        /// <summary>Assert that a reference is not null.</summary>
        public static T NotNull<T>(T value, string message = null) where T : class
        {
            if (value == null)
                throw new TouchstoneAssertException(message ?? "Expected value to be non-null.");
            return value;
        }

        /// <summary>Assert that a reference is null.</summary>
        public static void Null(object value, string message = null)
        {
            if (value != null)
                throw new TouchstoneAssertException(message ?? "Expected value to be null but it was [" + Format(value) + "].");
        }

        /// <summary>Assert that a string contains an expected substring.</summary>
        public static void Contains(string expectedSubstring, string actual, StringComparison comparison = StringComparison.Ordinal, string message = null)
        {
            if (actual == null || actual.IndexOf(expectedSubstring, comparison) < 0)
                throw new TouchstoneAssertException(
                    (message ?? "Expected substring not found.")
                    + " Expected to contain=[" + expectedSubstring + "] Actual=[" + Truncate(actual) + "]");
        }

        /// <summary>Assert that a string does not contain a substring.</summary>
        public static void DoesNotContain(string unexpectedSubstring, string actual, StringComparison comparison = StringComparison.Ordinal, string message = null)
        {
            if (actual != null && actual.IndexOf(unexpectedSubstring, comparison) >= 0)
                throw new TouchstoneAssertException(
                    (message ?? "Unexpected substring found.") + " Unexpected=[" + unexpectedSubstring + "]");
        }

        /// <summary>Assert that two byte sequences are identical.</summary>
        public static void BytesEqual(byte[] expected, byte[] actual, string message = null)
        {
            if (expected == null || actual == null || !expected.AsSpan().SequenceEqual(actual))
                throw new TouchstoneAssertException(
                    (message ?? "Byte sequences differ.")
                    + " ExpectedLength=[" + (expected?.Length.ToString() ?? "null") + "] ActualLength=[" + (actual?.Length.ToString() ?? "null") + "]");
        }

        /// <summary>Assert that a delegate throws an exception assignable to <typeparamref name="TException"/>.</summary>
        public static TException Throws<TException>(Action action, string message = null) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new TouchstoneAssertException(
                    (message ?? "Wrong exception type thrown.")
                    + " Expected=[" + typeof(TException).Name + "] Actual=[" + ex.GetType().Name + ": " + ex.Message + "]");
            }

            throw new TouchstoneAssertException(
                (message ?? "Expected an exception but none was thrown.") + " Expected=[" + typeof(TException).Name + "]");
        }

        /// <summary>Assert that an async delegate throws an exception assignable to <typeparamref name="TException"/>.</summary>
        public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string message = null) where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new TouchstoneAssertException(
                    (message ?? "Wrong exception type thrown.")
                    + " Expected=[" + typeof(TException).Name + "] Actual=[" + ex.GetType().Name + ": " + ex.Message + "]");
            }

            throw new TouchstoneAssertException(
                (message ?? "Expected an exception but none was thrown.") + " Expected=[" + typeof(TException).Name + "]");
        }

        private static string Format(object value)
        {
            if (value == null) return "null";
            return value.ToString();
        }

        private static string Truncate(string value, int max = 256)
        {
            if (String.IsNullOrEmpty(value)) return value ?? "null";
            return value.Length <= max ? value : value.Substring(0, max) + "...";
        }
    }

    /// <summary>
    /// Exception thrown by <see cref="Check"/> when an assertion fails.
    /// </summary>
    public sealed class TouchstoneAssertException : Exception
    {
        /// <summary>Instantiate.</summary>
        /// <param name="message">Assertion failure message.</param>
        public TouchstoneAssertException(string message) : base(message)
        {
        }
    }
}
