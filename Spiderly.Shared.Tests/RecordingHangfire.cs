using Hangfire;
using Hangfire.Common;
using Hangfire.States;

namespace Spiderly.Shared.Tests
{
    // Hangfire's two scheduling seams, recording what they are handed instead of writing to storage, for tests whose
    // claim is which job, method and schedule the code under test hands Hangfire.
    internal sealed class RecordingJobClient : IBackgroundJobClient
    {
        public List<(Job Job, IState State)> Created { get; } = new();

        public string Create(Job job, IState state)
        {
            Created.Add((job, state));
            return Created.Count.ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    internal sealed class RecordingRecurringJobManager : IRecurringJobManager
    {
        public List<(string Id, Job Job, string Cron, RecurringJobOptions Options)> Added { get; } = new();

        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options)
            => Added.Add((recurringJobId, job, cronExpression, options));

        public void Trigger(string recurringJobId) { }

        public void RemoveIfExists(string recurringJobId) { }
    }
}
