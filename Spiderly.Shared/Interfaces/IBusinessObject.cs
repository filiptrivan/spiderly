using System.ComponentModel.DataAnnotations;

namespace Spiderly.Shared.Interfaces
{
    /// <typeparam name="T">Entity's Id type — must be <c>int</c>, <c>long</c>, or <c>byte</c> (enforced by SPIDERLY018 at compile time).</typeparam>
    public interface IBusinessObject<T> where T : struct
    {
        /// <summary>
        /// WARNING: Automatically managed by the framework. Do not set manually unless you need to override in specific scenarios.
        /// </summary>
        public T Id { get; set; }

        /// <summary>
        /// WARNING: Automatically managed by the framework for concurrency control. Do not set manually unless you need to override in specific scenarios.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// WARNING: Automatically set by the framework on entity creation. Do not set manually unless you need to override in specific scenarios.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// WARNING: Automatically updated by the framework on entity modification. Do not set manually unless you need to override in specific scenarios.
        /// </summary>
        public DateTime ModifiedAt { get; set; }
    }
}
