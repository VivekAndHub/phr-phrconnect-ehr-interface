using System;
using System.Collections.Generic;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using System.Text;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction;
using Microsoft.ApplicationInsights;

[assembly: FunctionsStartup(typeof(StartUp))]
namespace Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction
{

    class StartUp : FunctionsStartup
    {

        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddApplicationInsightsTelemetry();
            
            builder.Services.AddSingleton<Services.IConfiguration, Services.Configuration>();
            builder.Services.AddTransient<Services.ISamlValidator, Services.SamlValidator>();
            builder.Services.AddTransient<Services.IServiceBusOperation, Services.ServiceBusOperation>();
        }

    }
}





