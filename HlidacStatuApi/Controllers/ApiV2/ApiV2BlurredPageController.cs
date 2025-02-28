﻿using System.Data;
using System.Data.SqlClient;

using HlidacStatu.Entities;
using HlidacStatu.Repositories;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;
using static HlidacStatu.DS.Api.BlurredPage;

namespace HlidacStatuApi.Controllers.ApiV2
{

    [ApiExplorerSettings(IgnoreApi = true)]
    [SwaggerTag("BlurredPage")]
    [ApiController()]
    [Route("api/v2/bp")]
    public class ApiV2BlurredPageController : ControllerBase
    {
        private class processed
        {

            public BpGet request { get; set; }
            public DateTime? taken { get; set; } = null;
            public string takenByUser { get; set; } = null;
        }
        static System.Collections.Concurrent.ConcurrentDictionary<string, processed> idsToProcess = null;
        static long runningSaveThreads = 0;
        static long savingPagesInThreads = 0;
        static long savedInThread = 0;
        static long saved = 0;

        static object lockObj = new object();
        static ApiV2BlurredPageController()
        {
        }


        [ApiExplorerSettings(IgnoreApi = true)]
        //[Authorize(Roles = "blurredAPIAccess")]
        [HttpGet("Get")]
        public async Task<ActionResult<BpGet>> Get()
        {
            CheckRoleRecord(this.User.Identity.Name);

            using HlidacStatu.Q.Simple.Queue<BpGet> q = new HlidacStatu.Q.Simple.Queue<BpGet>(
                HlidacStatu.DS.Api.BlurredPage.BlurredPageProcessingQueueName,
                Devmasters.Config.GetWebConfigValue("RabbitMqConnectionString")
                );

            ulong? id = null;
            var sq = q.GetAndAck(out id);
            if (sq == null)
                return StatusCode(404);

            return sq;

        }

        [ApiExplorerSettings(IgnoreApi = true)]
        //[Authorize(Roles = "blurredAPIAccess")]
        [HttpPost("Save")]
        public async Task<ActionResult> Save([FromBody] BpSave data)
        {
            CheckRoleRecord(this.User.Identity.Name);

            if (data.prilohy != null)
            {
                int numOfPages = data.prilohy.Sum(m => m.pages.Count());
                if (numOfPages > 200)
                {
                    new Thread(
                        () =>
                        {
                            _ = Interlocked.Increment(ref savedInThread);
                            _ = Interlocked.Increment(ref runningSaveThreads);
                            _ = Interlocked.Add(ref savingPagesInThreads, numOfPages);

                            HlidacStatuApi.Code.Log.Logger.Info(
                                "{action} {code} for {part} for {pages}.",
                                "starting",
                                "thread",
                                "ApiV2BlurredPageController.BpSave",
                                numOfPages);
                            Devmasters.DT.StopWatchEx sw = new Devmasters.DT.StopWatchEx();
                            sw.Start();

                            var success = SaveData(data).ConfigureAwait(false).GetAwaiter().GetResult();
                            if (!success)
                                _ = SaveData(data).ConfigureAwait(false).GetAwaiter().GetResult();

                            sw.Stop();
                            HlidacStatuApi.Code.Log.Logger.Info(
                                "{action} {code} for {part} for {pages} in {duration} sec.",
                                "ends",
                                "thread",
                                "ApiV2BlurredPageController.BpSave",
                                numOfPages,
                                sw.Elapsed.TotalSeconds);

                            _ = Interlocked.Decrement(ref runningSaveThreads);
                            _ = Interlocked.Add(ref savingPagesInThreads, -1 * numOfPages);

                        }
                    ).Start();

                }
                else
                {
                    var success = await SaveData(data);
                    if (!success)
                        _ = await SaveData(data);
                }
            }

            _ = Interlocked.Increment(ref savedInThread);

            //_ = justInProcess.Remove(data.smlouvaId, out _);

            return StatusCode(200);
        }


        [ApiExplorerSettings(IgnoreApi = true)]
        //[Authorize(Roles = "blurredAPIAccess")]
        [HttpPost("Log")]
        public async Task<ActionResult> Log([FromBody] string log)
        {
            CheckRoleRecord(this.User.Identity.Name);

            HlidacStatuApi.Code.Log.Logger.Error(
                "{action} {from} {user} {ip} {message}",
                "RemoteLog",
                "BlurredPageMinion",
                HttpContext?.User?.Identity?.Name,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                log);

            return StatusCode(200);
        }

        private static void CheckRoleRecord(string username)
        {
            //check if user is in blurredAPIAccess roles
            try
            {
                var found = HlidacStatu.Connectors.DirectDB.GetList<string, string>(
                    "select u.Id, ur.UserId from AspNetUsers u left join AspNetUserRoles ur on u.id = ur.UserId and ur.RoleId='e9a30ca6-8aa7-423c-88f2-b7dd24eda7f8' where u.UserName = @username",
                    System.Data.CommandType.Text, new IDataParameter[] { new SqlParameter("username", username) }
                    );
                if (found.Count() == 0)
                    return;
                if (found.Count() == 1 && found.First().Item2 == null)
                {
                    HlidacStatu.Connectors.DirectDB.NoResult(
                        @"insert into AspNetUserRoles select  (select id from AspNetUsers where Email like @username) as userId,'e9a30ca6-8aa7-423c-88f2-b7dd24eda7f8' as roleId",
                        System.Data.CommandType.Text, new IDataParameter[] { new SqlParameter("username", username) }
                        );
                }

            }
            catch (Exception e)
            {
                HlidacStatuApi.Code.Log.Logger.Error("cannot add {username} to the role blurredAPIAccess", e, username);
            }

        }


        private static async Task<bool> SaveData(BpSave data)
        {
            List<Task> tasks = new List<Task>();
            List<PageMetadata> pagesMD = new List<PageMetadata>();
            foreach (var p in data.prilohy)
            {
                foreach (var page in p.pages)
                {
                    PageMetadata pm = new PageMetadata();
                    pm.SmlouvaId = data.smlouvaId;
                    pm.PrilohaId = p.uniqueId;
                    pm.PageNum = page.page;
                    pm.Blurred = new PageMetadata.BlurredMetadata()
                    {
                        BlackenAreaBoundaries = page.blackenAreaBoundaries
                            .Select(b => new PageMetadata.BlurredMetadata.Boundary()
                            {
                                X = b.x,
                                Y = b.y,
                                Width = b.width,
                                Height = b.height
                            }
                            ).ToArray(),
                        TextAreaBoundaries = page.textAreaBoundaries
                            .Select(b => new PageMetadata.BlurredMetadata.Boundary()
                            {
                                X = b.x,
                                Y = b.y,
                                Width = b.width,
                                Height = b.height
                            }
                            ).ToArray(),
                        AnalyzerVersion = page.analyzerVersion,
                        Created = DateTime.Now,
                        ImageWidth = page.imageWidth,
                        ImageHeight = page.imageHeight,
                        BlackenArea = page.blackenArea,
                        TextArea = page.textArea
                    };
                    pagesMD.Add(pm);
                    var t = PageMetadataRepo.SaveAsync(pm);
                    tasks.Add(t);
                    //t.Wait();
                }
            }


            if (pagesMD.Count > 0)
            {
                var sml = await SmlouvaRepo.LoadAsync(data.smlouvaId);
                foreach (var pril in sml.Prilohy)
                {
                    IEnumerable<PageMetadata>? blurredPages = pagesMD.Where(m => m.PrilohaId == pril.UniqueHash());

                    if (blurredPages.Any())
                    {
                        var pb = new Smlouva.Priloha.BlurredPagesStats(blurredPages);
                        pril.BlurredPages = pb;
                    }
                    else
                    {
                        if (pril.BlurredPages != null)
                        {
                            //keep
                        }
                        else
                        {
                            pril.BlurredPages = null;
                        }
                    }
                }
                _ = await SmlouvaRepo.SaveAsync(sml, updateLastUpdateValue: false, skipPrepareBeforeSave: true);

            }
            try
            {
                Task.WaitAll(tasks.ToArray());
                _ = idsToProcess.Remove(data.smlouvaId, out var dt);
                return true;
            }
            catch (Exception e)
            {
                HlidacStatuApi.Code.Log.Logger.Error(
                    "{action} {code} for {part} exception.",
                    e,
                    "saving",
                    "thread",
                    "ApiV2BlurredPageController.BpSave"
                    );
                return false;
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "PrivateApi")]
        [HttpGet("AddTask")]
        public async Task<ActionResult<bool>> AddTask(string id, bool force =true)
        {
            var sml = await SmlouvaRepo.LoadAsync(id, includePrilohy: false);
            if (sml != null)
            {
                if (sml.Prilohy != null)
                {
                    var toProcess = sml.Prilohy.Where(p => (p.BlurredPages == null || force));
                    if (toProcess.Any())
                    {

                        var request = new BpGet()
                        {
                            smlouvaId = id,
                            prilohy = toProcess
                                .Select(priloha => new BpGet.BpGPriloha()
                                {
                                    uniqueId = priloha.UniqueHash(),
                                    url = priloha.LocalCopyUrl(id, true)
                                }
                                )
                                .ToArray()
                        };

                        using HlidacStatu.Q.Simple.Queue<BpGet> q = new HlidacStatu.Q.Simple.Queue<BpGet>(
                            BlurredPageProcessingQueueName,
                            Devmasters.Config.GetWebConfigValue("RabbitMqConnectionString")
                            );

                        q.Send(request);

                        return true;
                    }
                }
            }
            return false;

        }

        ////[ApiExplorerSettings(IgnoreApi = true)]
        //[Authorize(Roles = "blurredAPIAccess")]
        //[HttpGet("Stats")]
        //public async Task<ActionResult<BlurredPageAPIStatistics>> Stats()
        //{
        //    DateTime now = DateTime.Now;
        //    var inProcess = idsToProcess.Where(m => m.Value.taken != null);
        //    var res = new BlurredPageAPIStatistics()
        //    {
        //        total = idsToProcess.Count,
        //        currTaken = inProcess.Count(),
        //        totalFailed = inProcess.Count(m => (now - m.Value.taken.Value) > MAXDURATION_OF_TASK_IN_MIN)
        //    };

        //    return res;
        //}
        ////[ApiExplorerSettings(IgnoreApi = true)]
        //[Authorize()]
        //[HttpGet("Stats2")]
        //public async Task<ActionResult<BlurredPageAPIStatistics>> Stats2()
        //{
        //    if (!
        //        (this.User?.IsInRole("Admin") == true || this.User?.Identity?.Name == "api@hlidacstatu.cz")
        //        )
        //        return StatusCode(403);

        //    DateTime now = DateTime.Now;
        //    var res = new BlurredPageAPIStatistics()
        //    {
        //        total = idsToProcess.Count,
        //        currTaken = justInProcess.Count(),
        //        totalFailed = justInProcess.Count(m => (now - m.Value.taken) > MAXDURATION_OF_TASK_IN_MIN)
        //    };

        //    res.runningSaveThreads = Interlocked.Read(ref runningSaveThreads);
        //    res.savingPagesInThreads = Interlocked.Read(ref savingPagesInThreads);
        //    res.activeTasks = justInProcess
        //            .GroupBy(k => k.Value.takenByUser, v => v, (k, v) => new BlurredPageAPIStatistics.perItemStat<long>() { email = k, count = v.Count() })
        //            .ToArray();

        //    res.longestTasks = justInProcess.OrderByDescending(o => (now - o.Value.taken.Value).TotalSeconds)
        //                    .Select(m => new BlurredPageAPIStatistics.perItemStat<decimal>() { email = m.Value.takenByUser, count = (decimal)(now - m.Value.taken.Value).TotalSeconds })
        //                    .ToArray();
        //    res.avgTaskLegth = justInProcess
        //            .GroupBy(k => k.Value.takenByUser, v => v, (k, v) => new BlurredPageAPIStatistics.perItemStat<decimal>()
        //            {
        //                email = k,
        //                count = (decimal)v.Average(a => (now - a.Value.taken.Value).TotalSeconds)
        //            })
        //            .OrderByDescending(o => o.count)
        //            .ToArray();
        //    savedInThread = Interlocked.Read(ref savedInThread);

        //    return res;
        //}





    }
}