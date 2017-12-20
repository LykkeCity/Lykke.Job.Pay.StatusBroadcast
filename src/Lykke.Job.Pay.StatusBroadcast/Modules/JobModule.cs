using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Bitcoint.Api.Client;
using Common.Log;
using Lykke.AzureRepositories;
using Lykke.AzureRepositories.Azure.Tables;
using Lykke.Core;
using Lykke.Job.Pay.StatusBroadcast.Core;
using Lykke.Job.Pay.StatusBroadcast.Core.Services;
using Lykke.Job.Pay.StatusBroadcast.Services;
using Lykke.Pay.Service.GenerateAddress.Client;
using Lykke.Pay.Service.StoreRequest.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Lykke.Job.Pay.StatusBroadcast.Modules
{
    public class JobModule : Module
    {
        private readonly AppSettings.StatusBroadcastSettings _settings;
        private readonly ILog _log;
        // NOTE: you can remove it if you don't need to use IServiceCollection extensions to register service specific dependencies
        private readonly IServiceCollection _services;

        public JobModule(AppSettings.StatusBroadcastSettings settings, ILog log)
        {
            _settings = settings;
            _log = log;

            _services = new ServiceCollection();
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(_settings)
                .SingleInstance();

            builder.RegisterInstance(_log)
                .As<ILog>()
                .SingleInstance();

            builder.RegisterType<HealthService>()
                .As<IHealthService>()
                .SingleInstance()
                .WithParameter(TypedParameter.From(TimeSpan.FromSeconds(30)));

            
            var merchantPayRequestRepository =
            new MerchantPayRequestRepository(
                new AzureTableStorage<MerchantPayRequest>(_settings.Db.MerchantPayRequestConnectionString, "MerchantPayRequest", null));

            builder.RegisterInstance(merchantPayRequestRepository)
                .As<IMerchantPayRequestRepository>()
                .SingleInstance();

            var bitcoinAggRepository = new BitcoinAggRepository(
                new AzureTableStorage<BitcoinAggEntity>(
                    _settings.Db.MerchantPayRequestConnectionString, "BitcoinAgg",
                    null),
                new AzureTableStorage<BitcoinHeightEntity>(
                    _settings.Db.MerchantPayRequestConnectionString, "BitcoinHeight",
                    null));
            builder.RegisterInstance(bitcoinAggRepository)
                .As<IBitcoinAggRepository>()
                .SingleInstance();

            var merchantOrderRequestRepository =
                new MerchantOrderRequestRepository(
                    new AzureTableStorage<MerchantOrderRequest>(_settings.Db.MerchantPayRequestConnectionString, "MerchantOrderRequest", null));

            builder.RegisterInstance(merchantOrderRequestRepository)
                .As<IMerchantOrderRequestRepository>()
                .SingleInstance();
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
            builder.RegisterInstance(client).SingleInstance();

            builder.RegisterType<StatusProcessor>()
                .As<IStatusProcessor>()
                .SingleInstance();

           
            builder.Populate(_services);
        }


    }
}