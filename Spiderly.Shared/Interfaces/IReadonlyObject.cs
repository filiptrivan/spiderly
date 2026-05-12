namespace Spiderly.Shared.Interfaces
{
    /// <typeparam name="T">Entity's Id type — must be <c>int</c>, <c>long</c>, or <c>byte</c> (enforced by SPIDERLY018 at compile time).</typeparam>
    public interface IReadonlyObject<T> where T : struct
    {
        /// <summary>
        /// WARNING: Automatically managed by the framework. Do not set manually unless you need to override in specific scenarios.
        /// </summary>
        public T Id { get; }
    }
}
