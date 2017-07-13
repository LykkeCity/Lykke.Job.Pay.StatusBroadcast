using System;
using Lykke.Job.Pay.StatusBroadcast.Core.Services;

namespace Lykke.Job.Pay.StatusBroadcast.Services
{
    public class HealthService : IHealthService
    {
        // NOTE: These are example properties
        public DateTime LastSpServiceStartedMoment { get; private set; }
        public TimeSpan LastSpServiceDuration { get; private set; }
        public TimeSpan MaxHealthySpServiceDuration { get; }

        // NOTE: These are example properties
        private bool WasLastSpServiceFailed { get; set; }
        private bool WasLastSpServiceCompleted { get; set; }
        private bool WasClientsSpServiceEverStarted { get; set; }

        // NOTE: When you change parameters, don't forget to look in to JobModule

        public HealthService(TimeSpan maxHealthySpServiceDuration)
        {
            MaxHealthySpServiceDuration = maxHealthySpServiceDuration;
        }

        // NOTE: This method probably would stay in the real job, but will be modified
        public string GetHealthViolationMessage()
        {
            if (WasLastSpServiceFailed)
            {
                return "Last SpService was failed";
            }

            if (!WasLastSpServiceCompleted && !WasLastSpServiceFailed && !WasClientsSpServiceEverStarted)
            {
                return "Waiting for first SpService execution started";
            }

            if (!WasLastSpServiceCompleted && !WasLastSpServiceFailed && WasClientsSpServiceEverStarted)
            {
                return $"Waiting {DateTime.UtcNow - LastSpServiceStartedMoment} for first SpService execution completed";
            }

            if (LastSpServiceDuration > MaxHealthySpServiceDuration)
            {
                return $"Last SpService was lasted for {LastSpServiceDuration}, which is too long";
            }
            return null;
        }

        // NOTE: These are example methods
        public void TraceSpServiceStarted()
        {
            LastSpServiceStartedMoment = DateTime.UtcNow;
            WasClientsSpServiceEverStarted = true;
        }

        public void TraceSpServiceCompleted()
        {
            LastSpServiceDuration = DateTime.UtcNow - LastSpServiceStartedMoment;
            WasLastSpServiceCompleted = true;
            WasLastSpServiceFailed = false;
        }

        public void TraceSpServiceFailed()
        {
            WasLastSpServiceCompleted = false;
            WasLastSpServiceFailed = true;
        }

        public void TraceBooStarted()
        {
            // TODO: See PrService
        }

        public void TraceBooCompleted()
        {
            // TODO: See PrService
        }

        public void TraceBooFailed()
        {
            // TODO: See PrService
        }
    }
}