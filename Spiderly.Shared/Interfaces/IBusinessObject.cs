using System.ComponentModel.DataAnnotations;

namespace Spiderly.Shared.Interfaces
{
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
