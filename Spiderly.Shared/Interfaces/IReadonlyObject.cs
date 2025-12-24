namespace Spiderly.Shared.Interfaces
{
    public interface IReadonlyObject<T> where T : struct
    {
        /// <summary>
        /// WARNING: Automatically managed by the framework. Do not set manually unless you need to override in specific scenarios.
        /// </summary>
        public T Id { get; }
    }
}
