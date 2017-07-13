using System;

namespace Lykke.Job.Pay.StatusBroadcast.Core.Services
{
    public interface IHealthService
    {
        // NOTE: These are example properties
        DateTime LastSpServiceStartedMoment { get; }
        TimeSpan LastSpServiceDuration { get; }
        TimeSpan MaxHealthySpServiceDuration { get; }

        // NOTE: This method probably would stay in the real job, but will be modified
        string GetHealthViolationMessage();

        // NOTE: These are example methods
        void TraceSpServiceStarted();
        void TraceSpServiceCompleted();
        void TraceSpServiceFailed();
       
    }
}