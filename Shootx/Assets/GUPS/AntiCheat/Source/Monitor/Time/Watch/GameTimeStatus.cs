// GUPS - AntiCheat - Core
using GUPS.AntiCheat.Core.Watch;

namespace GUPS.AntiCheat.Monitor.Time
{
    /// <summary>
    /// Represents a structure for monitoring and conveying the status of game time deviation, implementing the <see cref="IWatchedSubject"/> interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="GameTimeStatus"/> struct encapsulates information about the deviation of game time, allowing observers to stay informed about any time-related manipulations.
    /// </para>
    /// </remarks>
    public struct GameTimeStatus : IWatchedSubject
    {
        /// <summary>
        /// Gets the deviation of game time, indicating any time-related manipulations.
        /// </summary>
        public ETimeDeviation DeltaDeviation { get; private set; }

        /// <summary>
        /// Gets the fixed delta time (physics) deviation, indicating any fixed time-related manipulations.
        /// </summary>
        public ETimeDeviation FixedDeltaDeviation { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GameTimeStatus"/> struct with the specified time deviation.
        /// </summary>
        /// <param name="_DeltaDeviation">The deviation of game time.</param>
        /// <param name="_FixedDeltaDeviation">The fixed delta time (physics) deviation.</param>
        public GameTimeStatus(ETimeDeviation _DeltaDeviation, ETimeDeviation _FixedDeltaDeviation)
        {
            this.DeltaDeviation = _DeltaDeviation;
            this.FixedDeltaDeviation = _FixedDeltaDeviation;
        }
    }
}
