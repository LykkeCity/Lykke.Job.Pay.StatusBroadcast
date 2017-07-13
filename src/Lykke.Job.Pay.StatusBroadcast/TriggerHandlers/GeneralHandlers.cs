using System;
using System.Threading.Tasks;
using Lykke.Job.Pay.StatusBroadcast.Core.Services;
using Lykke.JobTriggers.Triggers.Attributes;

namespace Lykke.Job.Pay.StatusBroadcast.TriggerHandlers
{
    // NOTE: This is the trigger handlers class example
    public class GeneralHandlers
    {
        private readonly IStatusProcessor _statusRequest;
        private readonly IHealthService _healthService;

        // NOTE: The object is instantiated using DI container, so registered dependencies are injects well
        public GeneralHandlers(IStatusProcessor statusRequest, IHealthService healthService)
        {
            _statusRequest = statusRequest;
            _healthService = healthService;
        }


        [TimerTrigger("00:00:10")]
        public async Task TimeTriggeredHandler()
        {
            try
            {
                _healthService.TraceSpServiceStarted();

                await _statusRequest.ProcessAsync();

                _healthService.TraceSpServiceCompleted();
            }
            catch(Exception e)
            {
                _healthService.TraceSpServiceFailed();
            }

        }

       
    }
}