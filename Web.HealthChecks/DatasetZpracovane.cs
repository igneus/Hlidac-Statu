﻿
using Microsoft.Extensions.Diagnostics.HealthChecks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace HlidacStatu.Web.HealthChecks
{
    public class DatasetZpracovane : IHealthCheck
    {

        public enum IntervalEnum
        {
            Day,
            Week,
            Month
        }

        private Options options;
        public class Options
        {
            public string DatasetId { get; set; }
            public int MinRecordsInInterval { get; set; }
            public IntervalEnum Interval { get; set; }
        }

        public DatasetZpracovane(Options options)
        {
            this.options = options;
        }


        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {

            try
            {
                string result = "";
                bool bad = false;
                DateTime dateFrom = DateTime.Now.Date;
                DateTime dateTo = DateTime.Now.Date;
                string dateQ = "";
                switch (options.Interval)
                {
                    case IntervalEnum.Day:
                        dateFrom = dateTo.AddDays(-1);
                        dateQ = $"{dateFrom:yyyy-MM-dd}";
                        break;
                    case IntervalEnum.Week:
                        dateFrom = dateTo.AddDays(-7);
                        dateQ = $"[{dateFrom:yyyy-MM-dd} TO {dateTo:yyyy-MM-dd}]";
                        break;
                    case IntervalEnum.Month:
                        dateFrom = dateTo.AddMonths(-1);
                        dateQ = $"[{dateFrom:yyyy-MM-dd} TO {dateTo:yyyy-MM-dd}]";
                        break;
                    default:
                        break;
                }

                var ds = HlidacStatu.Datasets.DataSet.CachedDatasets.Get(options.DatasetId);
                if (ds != null)
                {
                    var res = HlidacStatu.Datasets.DatasetRepo.Searching.SearchDataRaw(ds, $"DbCreated:{dateQ}", 1, 1,
                        "0", exactNumOfResults: true);

                    result += $"{dateFrom:yyyy-MM-dd} - {dateTo:MM-dd}: {res.Total} zaznamů \n";
                    if (res.Total < options.MinRecordsInInterval)
                        bad = true;


                    if (bad)
                        return Task.FromResult(HealthCheckResult.Degraded(result));
                    else
                        return Task.FromResult(HealthCheckResult.Healthy(result));
                }
                return Task.FromResult(HealthCheckResult.Unhealthy($"Dataset {options.DatasetId} doesn't exists."));
            }
            catch (Exception e)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Unknown status, cannot read data from network disk", e));
            }


        }




    }
}

