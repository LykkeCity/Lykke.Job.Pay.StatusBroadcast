using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Common;
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
        private readonly IMerchantOrderRequestRepository _merchantOrderRepo;
        private readonly AppSettings.StatusBroadcastSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly IBitcoinAggRepository _bitcoinRepo;

        public StatusProcessor(IMerchantPayRequestRepository merchantRepo, ILog log, AppSettings.StatusBroadcastSettings settings,
            HttpClient httpClient, IBitcoinAggRepository bitcoinRepo, IMerchantOrderRequestRepository merchantOrderRepo)
        {
            _log = log;
            _merchantRepo = merchantRepo;
            _settings = settings;
            _httpClient = httpClient;
            _bitcoinRepo = bitcoinRepo;
            _merchantOrderRepo = merchantOrderRepo;
        }

        private async Task ProcessRequests()
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
                            NumberOfConfirmation = GetNumberOfConfirmation(r.DestinationAddress, r.TransactionId),
                            TimeStamp = DateTime.UtcNow.Ticks,
                            Url = $"{_settings.LykkePayBaseUrl}transaction/{r.TransactionId}"
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

        private async Task ProcessOrders()
        {
            var orders = await _merchantOrderRepo.GetAllAsync();


            foreach (var r in orders)
            {
                bool needSave = false;
                if ((r.MerchantPayRequestNotification & MerchantPayRequestNotification.Success) ==
                    MerchantPayRequestNotification.Success &&
                    !string.IsNullOrEmpty(r.SuccessUrl))
                {
                    await PostInfo(r.SuccessUrl, JsonConvert.SerializeObject(new PaymentSuccessReturn
                    {
                        PaymentResponse = new PaymentSuccessResponse
                        {
                            TransactionId = r.TransactionId,
                            Currency = r.AssetId,
                            NumberOfConfirmation = GetNumberOfConfirmation(r.SourceAddress, r.TransactionId),
                            TimeStamp = DateTime.UtcNow.Ticks,
                            Url = $"{_settings.LykkePayBaseUrl}transaction/{r.TransactionId}"
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
                    await PostInfo(r.ProgressUrl, JsonConvert.SerializeObject(new PaymentInProgressReturn
                    {
                        PaymentResponse = new PaymentInProgressResponse
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
                    var transferStatus = string.IsNullOrEmpty(r.TransactionStatus)
                        ? InvoiceStatus.Unpaid
                        : r.TransactionStatus.ParsePayEnum<InvoiceStatus>();
                    PaymentError paymentError;
                    switch (transferStatus)
                    {
                        case InvoiceStatus.Draft:
                        case InvoiceStatus.InProgress:
                        case InvoiceStatus.Paid:
                        case InvoiceStatus.Removed:
                            paymentError = PaymentError.TRANSACTION_NOT_DETECTED;
                            break;
                        case InvoiceStatus.LatePaid:
                        case InvoiceStatus.Unpaid:
                            paymentError = PaymentError.PAYMENT_EXPIRED;
                            break;
                        case InvoiceStatus.Overpaid:
                            paymentError = PaymentError.AMOUNT_ABOVE;
                            break;
                        case InvoiceStatus.Underpaid:
                            paymentError = PaymentError.AMOUNT_BELOW;
                            break;
                        default:
                            paymentError = PaymentError.TRANSACTION_NOT_DETECTED;
                            break;

                    }
                    await PostInfo(r.ErrorUrl, JsonConvert.SerializeObject(
                        new PaymentErrorReturn
                        {
                            PaymentResponse = new PaymentErrorResponse
                            {
                                PaymentError = paymentError,
                                TimeStamp = DateTime.UtcNow.Ticks
                            }
                        }));
                    needSave = true;
                    r.MerchantPayRequestNotification &= ~MerchantPayRequestNotification.Error;
                }

                if (needSave)
                {
                    await _merchantOrderRepo.SaveRequestAsync(r);
                }
            }
        }

        public async Task ProcessAsync()
        {
            await ProcessRequests();
            await ProcessOrders();
        }

        private int GetNumberOfConfirmation(string address, string transactionId)
        {
            //var height = await _bitcoinRepo.GetNextBlockId();
            //var transaction = await _bitcoinRepo.GetWalletTransactionAsync(address, transactionId);
            //if (transaction == null)
            //{
            //    return 0;
            //}

            //return height - transaction.BlockNumber + _settings.TransactionConfirmation;
            return _settings.TransactionConfirmation;
        }

        private async Task PostInfo(string url, string serializeObject)
        {
            try
            {
                await _log.WriteInfoAsync(ComponentName, "Sending confirmation", JsonConvert.SerializeObject(new
                {
                    url,
                    message = serializeObject
                }));
                var result = await _httpClient.PostAsync(url, new StringContent(serializeObject, Encoding.UTF8, "application/json"));
                await _log.WriteInfoAsync(ComponentName, "Sending confirmation result", JsonConvert.SerializeObject(new
                {
                    url,
                    result.StatusCode,
                    body= await result.Content.ReadAsStringAsync()
                }));
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(ComponentName, "Sending confirmation", null, ex);
            }
        }
    }
}