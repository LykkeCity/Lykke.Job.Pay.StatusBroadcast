using System;
using System.Net.Http;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Core;
using Lykke.Job.Pay.StatusBroadcast.Core;
using Lykke.Job.Pay.StatusBroadcast.Core.Services;
using Lykke.Pay.Common;
using Newtonsoft.Json;

namespace Lykke.Job.Pay.StatusBroadcast.Services
{
    // NOTE: This is job service class example
    public class StatusProcessor : IStatusProcessor
    {
        public static readonly string ComponentName = "Lykke.Job.Pay.StatusBroadcast";
        private readonly ILog _log;
        private readonly IMerchantPayRequestRepository _merchantRepo;
        private readonly AppSettings.StatusBroadcastSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly IBitcoinAggRepository _bitcoinRepo;

        public StatusProcessor(IMerchantPayRequestRepository merchantRepo, ILog log, AppSettings.StatusBroadcastSettings settings,
            HttpClient httpClient, IBitcoinAggRepository bitcoinRepo)
        {
            _log = log;
            _merchantRepo = merchantRepo;
            _settings = settings;
            _httpClient = httpClient;
            _bitcoinRepo = bitcoinRepo;
        }
        public async Task ProcessAsync()
        {
            var requests = await _merchantRepo.GetAllAsync();

            foreach (var r in requests)
            {
                bool needSave = false;
                if ((r.MerchantPayRequestNotification & MerchantPayRequestNotification.Success) ==
                    MerchantPayRequestNotification.Success &&
                    !string.IsNullOrEmpty(r.SuccessUrl))
                {
                    await PostInfo(r.SuccessUrl, JsonConvert.SerializeObject(new TransferSuccessReturn
                        {
                            TransferResponse = new TransferSuccessResponse
                            {
                                TransactionId = r.TransactionId,
                                Currency = r.AssetId,
                                NumberOfConfirmation = await GetNumberOfConfirmation(r.DestinationAddress, r.TransactionId),
                                TimeStamp = DateTime.UtcNow.Ticks,
                                Url = $"{_settings.LykkePayBaseUrl}/transaction/{r.TransactionId}"
                            }
                        }
                    ));
                    needSave = true;
                    r.MerchantPayRequestNotification &= ~MerchantPayRequestNotification.Success;
                }

                if ((r.MerchantPayRequestNotification & MerchantPayRequestNotification.InProgress) ==
                    MerchantPayRequestNotification.InProgress &&
                    !string.IsNullOrEmpty(r.ProgressUrl))
                {
                    await PostInfo(r.ProgressUrl, JsonConvert.SerializeObject(new TransferInProgressReturn
                    {
                        TransferResponse = new TransferInProgressResponse
                        {
                            Settlement = Settlement.TRANSACTION_DETECTED,
                            TimeStamp = DateTime.UtcNow.Ticks,
                            Currency = r.AssetId,
                            TransactionId = r.TransactionId
                        }
                    }));
                   needSave = true;
                    r.MerchantPayRequestNotification &= ~MerchantPayRequestNotification.InProgress;
                }

                if ((r.MerchantPayRequestNotification & MerchantPayRequestNotification.Error) ==
                    MerchantPayRequestNotification.Error &&
                    !string.IsNullOrEmpty(r.ErrorUrl))
                {
                    await PostInfo(r.ErrorUrl, JsonConvert.SerializeObject(
                        new TransferErrorReturn
                        {
                            TransferResponse = new TransferErrorResponse
                            {
                                TransferError = TransferError.INTERNAL_ERROR,
                                TimeStamp = DateTime.UtcNow.Ticks
                            }
                        }));
                    needSave = true;
                    r.MerchantPayRequestNotification &= ~MerchantPayRequestNotification.Error;
                }

                if (needSave)
                {
                    await _merchantRepo.SaveRequestAsync(r);
                }
            }
        }

        private async Task<int> GetNumberOfConfirmation(string address, string transactionId)
        {
            var height = await _bitcoinRepo.GetNextBlockId();
            var transaction = await _bitcoinRepo.GetWalletTransactionAsync(address, transactionId);
            if (transaction == null)
            {
                return 0;
            }

            return height - transaction.BlockNumber + _settings.TransactionConfirmation;
        }

        private async Task PostInfo(string url, string serializeObject)
        {
            try
            {
                await _httpClient.PostAsync(url, new StringContent(serializeObject));
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(ComponentName, "Sending confirmation", null, ex);
            }
        }
    }
}