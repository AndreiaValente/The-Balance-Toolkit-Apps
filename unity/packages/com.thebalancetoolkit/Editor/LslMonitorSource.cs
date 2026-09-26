using System;

namespace TheBalanceToolkit.Editor
{
    /// <summary>
    /// Feeds LSL samples to the monitor window (Balance Toolkit Demo > Monitor).
    /// The core package has no LSL dependency; the LSL Integration sample registers an implementation.
    /// </summary>
    public interface ILslMonitorSource
    {
        /// <summary>
        /// Connects to the app's <c>&lt;name&gt;_basic</c> and <c>&lt;name&gt;_complex</c> streams, or to the one
        /// stream named when <paramref name="streamName"/> already ends in <c>_basic</c> or <c>_complex</c>.
        /// Throws when no stream is found.
        /// </summary>
        void Connect(string streamName);

        void Disconnect();

        /// <summary>Pulls pending samples without blocking; called from the editor update loop.</summary>
        void Poll(Action<float[]> basicSample, Action<float[]> complexSample);
    }

    public static class LslMonitor
    {
        /// <summary>Null until the LSL Integration sample is imported and LSL4Unity is installed.</summary>
        public static ILslMonitorSource Source { get; set; }
    }
}
