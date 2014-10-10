﻿using System.Net;
using AppGet.FlightPlans;
using AppGet.Http;
using AppGet.Serialization;
using NLog;

namespace AppGet.PackageProvider
{
    public class AppGetServerProvider : IPackageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public AppGetServerProvider(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }


        public PackageInfo FindPackage(string name)
        {
            _logger.Info("Finding package " + name);
            var requestBuilder = new HttpRequestBuilder("http://appget.azurewebsites.net/api/v1/");

            var request = requestBuilder.Build("packages/{package}/latest");
            request.AddSegment("package", name);

            try
            {
                var package = _httpClient.Get<PackageInfo>(request);
                return package.Resource;
            }
            catch (HttpException ex)
            {
                if (ex.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                throw;
            }
        }
    }
}