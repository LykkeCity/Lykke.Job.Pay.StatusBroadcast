using System.Threading.Tasks;
using Common.Log;
using Lykke.Job.Pay.StatusBroadcast.Core;
using Lykke.Job.Pay.StatusBroadcast.Core.Services;

namespace Lykke.Job.Pay.StatusBroadcast.Services
{
    // NOTE: This is job service class example
    public class StatusProcessor : IStatusProcessor
    {
        public static readonly string ComponentName = "Lykke.Job.Pay.StatusBroadcast";
        private readonly ILog _log;
        private readonly AppSettings.StatusBroadcastSettings _settings;
        


        public StatusProcessor(AppSettings.StatusBroadcastSettings settings, ILog log)
        {
            _log = log;
            _settings = settings;
            
        }
        public async Task ProcessAsync()
        {

          

        }

        
        
    }
}