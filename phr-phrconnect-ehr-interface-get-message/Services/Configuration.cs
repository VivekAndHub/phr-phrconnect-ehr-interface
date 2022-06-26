﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.ApplicationInsights;

namespace Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.Services
{
    public class Configuration : IConfiguration
    {
        public string[] ShieldThumbPrint { get; private set; }
        public string[] ShieldAllowedURIs { get; private set; }
        public Boolean IsProdEnv { get; private set; }
        public int BatchSizeLimit { get; private set; }
        public int PollingInterval { get; private set; }
        public int TraceLimit { get; private set; }
        public int PollingTimeoutInSeconds { get; private set; }

        public X509Certificate2Collection ShieldCertificates { get; private set; }
        
        /// <summary>
        /// Load environment variables
        /// </summary>
        public Configuration()
        {
            this.ShieldThumbPrint = Environment.GetEnvironmentVariable("ShieldThumbPrint").Split(',');
            this.ShieldAllowedURIs = Environment.GetEnvironmentVariable("ShieldAllowedURIs").Split(',');
            this.IsProdEnv = this.Parse<Boolean>(Environment.GetEnvironmentVariable("IsProdEnv"));
            this.BatchSizeLimit = this.Parse<Int32>(Environment.GetEnvironmentVariable("BatchSizeLimit"));
            this.PollingInterval = this.Parse<Int32>(Environment.GetEnvironmentVariable("PollingInterval"));
            this.TraceLimit = this.Parse<Int32>(Environment.GetEnvironmentVariable("TraceLimit"));
            this.PollingInterval = this.Parse<Int32>(Environment.GetEnvironmentVariable("DefaultPollingTimeoutInSeconds"));

            //Load Shield Certificate
            //Get certificate from store
            X509Certificate2Collection certificates = new X509Certificate2Collection();
            foreach (var thumbprint in this.ShieldThumbPrint)
            {
                using (X509Store certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {

                    certStore.Open(OpenFlags.ReadOnly);
                    certificates.AddRange(certStore.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false));


                }
            }
            this.ShieldCertificates = certificates;

            
        }

        /// <summary>
        /// Convert string to specified type.
        /// </summary>
        /// <typeparam name="T">Type to convert to</typeparam>
        /// <param name="value">Value to return</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public T Parse<T>(string value)
        {
            var parser = TypeDescriptor.GetConverter(typeof(T));
            if (parser != null)
            {
                return (T)parser.ConvertFromString(value);
            }

            throw new ArgumentException("Unable to get parser fro the variable.");
        }
    }
}
