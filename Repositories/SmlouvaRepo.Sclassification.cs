using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Devmasters.Cache.File;

using HlidacStatu.Entities;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HlidacStatu.Repositories
{
    public static partial class SmlouvaRepo
    {
        private static volatile Devmasters.Cache.File.Manager oldStemCacheManager
            = Devmasters.Cache.File.Manager.GetSafeInstance("SmlouvyStems",
                smlouvaKeyId => GetRawStemsFromServerAsync(smlouvaKeyId).ConfigureAwait(false).GetAwaiter().GetResult(),
                TimeSpan.FromDays(365 * 10)); //10 years

        private static volatile Devmasters.Cache.AWS_S3.Manager<byte[], string> stemCacheManager
            = Devmasters.Cache.AWS_S3.Manager<byte[], string>.GetSafeInstance(
                "SmlouvyStems/",
                key => GetRawStemsFromServerAsync(key).ConfigureAwait(false).GetAwaiter().GetResult(),
                TimeSpan.Zero,
                new string[] { Devmasters.Config.GetWebConfigValue("Minio.Cache.Endpoint") },
                Devmasters.Config.GetWebConfigValue("Minio.Cache.Bucket"),
                Devmasters.Config.GetWebConfigValue("Minio.Cache.AccessKey"),
                Devmasters.Config.GetWebConfigValue("Minio.Cache.SecretKey"),
                key => $"{key.Substring(0,3)}/stem_smlouva_{key}"
                );


        private static async Task<byte[]> GetRawStemsFromServerAsync(KeyAndId smlouvaKeyId)
        {
            return await GetRawStemsFromServerAsync(smlouvaKeyId.ValueForData);
        }

        private static async Task<byte[]> GetRawStemsFromServerAsync(string smlouvaId)
        {
            Smlouva s = await SmlouvaRepo.LoadAsync(smlouvaId);

            if (s == null)
                return null;

            var settings = new JsonSerializerSettings();
            settings.ContractResolver = new Util.FirstCaseLowercaseContractResolver();

            string stemmerResponse = CallEndpoint("stemmer",
                JsonConvert.SerializeObject(s, settings),
                s.Id,
                1000 * 60 * 30);
            try
            {
                var jtoken = JToken.Parse(stemmerResponse);
            }
            catch (JsonReaderException e)
            {
                Util.Consts.Logger.Error($"Stemmer returned incomplete json for {smlouvaId}", e);
                throw;
            }

            return Encoding.UTF8.GetBytes(stemmerResponse);
        }

        public static bool MigrateRawStems(string sId)
        {
            var key = new KeyAndId() { ValueForData = sId, CacheNameOnDisk = $"stem_smlouva_{sId}" };

            if (oldStemCacheManager.Exists(key) == false)
                return true;
            
            var data = oldStemCacheManager.Get(key);

            if (data == null)
                return true;

            try
            {
                stemCacheManager.Set(sId, data);
                oldStemCacheManager.Delete(key);

            }
            catch (Exception)
            {
                try
                {

                }
                catch (Exception e)
                {
                    stemCacheManager.Set(sId, data);
                    oldStemCacheManager.Delete(key);
                    Util.Consts.Logger.Debug("Deleting stems cache for " + sId);

                    return false;
                }
            }


            return true;
        }
        public static string GetRawStems(string smlouvaId, bool rewriteStems = false)
        {
            if (string.IsNullOrEmpty(smlouvaId))
                return null;
            if (rewriteStems)
            {
                InvalidateStemCache(smlouvaId);
            }

            var data = stemCacheManager.Get(smlouvaId);
            if (data == null)
                return null;

            return Encoding.UTF8.GetString(data);
        }

        public static void InvalidateStemCache(string smlouvaId)
        {
            Util.Consts.Logger.Debug("Deleting stems cache for " + smlouvaId);

            stemCacheManager.Delete(smlouvaId);
        }

        public static Dictionary<Smlouva.SClassification.ClassificationsTypes, decimal> GetClassificationFromServer(Smlouva s,
            bool rewriteStems = false)
        {
            var data = new Dictionary<Smlouva.SClassification.ClassificationsTypes, decimal>();
            if (s.Prilohy.Any(m => m.EnoughExtractedText) == false)
                return null;

            var settings = new JsonSerializerSettings();
            settings.ContractResolver = new Util.FirstCaseLowercaseContractResolver();

            var stems = GetRawStems(s.Id, rewriteStems);
            if (string.IsNullOrEmpty(stems))
            {
                return data;
            }

            string classifierResponse = "";
            try
            {
                classifierResponse = CallEndpoint("classifier",
                    stems,
                    s.Id,
                    180000);
            }
            catch
            {
                //retry once with new stems
                stems = GetRawStems(s.Id, true);
                classifierResponse = CallEndpoint("classifier",
                    stems,
                    s.Id,
                    180000);
            }

            //string finalizerResponse = CallEndpoint("finalizer",
            //        classifierResponse,
            //        s.Id,
            //        30000);

            var jsonData = JObject.Parse(classifierResponse);

            if (jsonData.Children().Count() == 0)
            {
                data.Add(Smlouva.SClassification.ClassificationsTypes.OSTATNI, 0.6M);
                return data;
            }

            foreach (JProperty item in jsonData.Children())
            {
                string key = item.Name.Replace("-", "_")
                    .Replace("_generic", "_obecne"); // jsonData[i][0].Value<string>().Replace("-", "_");
                decimal prob = Util.ParseTools.ToDecimal(item.Value.ToString()) ?? 0;
                if (prob > 0)
                {
                    if (Enum.TryParse<Smlouva.SClassification.ClassificationsTypes>(key, out var typ))
                    {
                        if (!data.ContainsKey(typ))
                            data.Add(typ, prob);
                        else if (typ == Smlouva.SClassification.ClassificationsTypes.OSTATNI)
                            Util.Consts.Logger.Warning($"Classification type lookup failure : {key}");
                    }
                    else
                    {
                        Util.Consts.Logger.Warning("Classification type lookup failure - Invalid key " + key);
                        if (!data.ContainsKey(Smlouva.SClassification.ClassificationsTypes.OSTATNI))
                            data.Add(Smlouva.SClassification.ClassificationsTypes.OSTATNI, prob);
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// Gets classification
        /// </summary>
        /// <param name="idSmlouvy"></param>
        /// <returns>Classification json</returns>
        public static async Task<string> GetClassificationExplanationAsync(string idSmlouvy)
        {
            if (string.IsNullOrWhiteSpace(idSmlouvy))
                return null;

            Smlouva s = await SmlouvaRepo.LoadAsync(idSmlouvy);

            if (s == null)
                return null;

            var settings = new JsonSerializerSettings();
            settings.ContractResolver = new Util.FirstCaseLowercaseContractResolver();

            var response = CallEndpoint("explain_json",
                JsonConvert.SerializeObject(s, settings),
                idSmlouvy,
                1000 * 60 * 10);
            return System.Text.RegularExpressions.Regex.Unescape(response);
        }

        /// <summary>
        /// Sets new classification on smlouva. And saves data also to audit table []. 
        /// Originally created for manual override of classification.
        /// </summary>
        /// <param name="typeValues">new classification</param>
        /// <param name="username">author</param>
        public static async Task OverrideClassificationAsync(this Smlouva smlouva, int[] typeValues, string username)
        {
            if (typeValues.Length == 0)
                throw new ArgumentException($"typeValues is empty");
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException($"username is empty");

            List<Smlouva.SClassification.Classification> newClassification = new List<Smlouva.SClassification.Classification>();
            for (int i = 0; i < typeValues.Length; i++)
            {
                if (!Enum.IsDefined(typeof(Smlouva.SClassification.ClassificationsTypes), typeValues[i]))
                    throw new ArgumentException(
                        $"TypeValue [{typeValues[i]}] is not defined in {nameof(Smlouva.SClassification.ClassificationsTypes)}.");

                var classItem = new Smlouva.SClassification.Classification
                {
                    TypeValue = typeValues[i],
                    ClassifProbability = (i == 0) ? 0.8m : 0.7m
                };
                newClassification.Add(classItem);
            }

            // save to db
            ClassificationOverrideRepo.Save(username, smlouva.Id, smlouva.Classification, newClassification);

            smlouva.Classification.TypesToProperties(newClassification.ToArray());
            smlouva.Classification.LastUpdate = DateTime.Now;
            await SmlouvaRepo.SaveAsync(smlouva);
        }

        private static string CallEndpoint(string endpoint, string content, string id, int timeoutMs)
        {
            var url = classificationBaseUrl() + $"/{endpoint}?doc_id={id}";
            using (Devmasters.Net.HttpClient.URLContent request = new Devmasters.Net.HttpClient.URLContent(url))
            {
                request.Method = Devmasters.Net.HttpClient.MethodEnum.POST;
                request.Tries = 3;
                request.TimeInMsBetweenTries = 5000;
                request.Timeout = timeoutMs;
                request.ContentType = "application/json; charset=utf-8";
                request.RequestParams.RawContent = content;
                Devmasters.Net.HttpClient.TextContentResult response = null;
                Devmasters.DT.StopWatchEx sw = new Devmasters.DT.StopWatchEx();
                try
                {
                    sw.Start();
                    response = request.GetContent();
                    sw.Stop();
                    Util.Consts.Logger.Debug($"Called classifier endpoint [{endpoint}] for {id} from {url} in {sw.ElapsedMilliseconds}ms.");

                    return response.Text;
                }
                catch (Exception e)
                {
                    sw.Stop();
                    Util.Consts.Logger.Error($"Error classifier endpoint [{endpoint}] for {id} from {url} in {sw.ElapsedMilliseconds}ms, error {e.Message}", e);
                    throw;
                }
            }
        }

        private static string classificationBaseUrl()
        {
            string[] baseUrl = Devmasters.Config.GetWebConfigValue("Classification.Service.Url")
                .Split(',', ';');
            //Dictionary<string, DateTime> liveEndpoints = new Dictionary<string, DateTime>();

            return baseUrl[Util.Consts.Rnd.Next(baseUrl.Length)];

        }

        public static bool SetClassification(this Smlouva smlouva, bool rewrite = false, bool rewriteStems = false) //true if changed
        {
            if (smlouva.Prilohy == null
                || !smlouva.Prilohy.Any(m => m.EnoughExtractedText))
                return false;

            if (!rewrite && !rewriteStems && (smlouva.Classification?.LastUpdate) != null
                && ((smlouva.Classification?.GetClassif()) == null || smlouva.Classification.GetClassif().Count() != 0))
                return false;

            var types = GetClassificationFromServer(smlouva, rewriteStems);
            if (types == null)
            {
                smlouva.Classification = null;
            }
            else
            {
                Smlouva.SClassification.ClassificationsTypes[] vyjimkyClassif =
                    new Smlouva.SClassification.ClassificationsTypes[]
                    {
                    Smlouva.SClassification.ClassificationsTypes.finance_formality,
                    Smlouva.SClassification.ClassificationsTypes.finance_repo,
                    Smlouva.SClassification.ClassificationsTypes.finance_bankovni,
                    };

                var smluvniStrany = smlouva.Prijemce.Concat(new Smlouva.Subjekt[] { smlouva.Platce })
                    .Select(m => Firmy.Get(m.ico))
                    .Where(m => m.Valid == true)
                    .ToArray();
                if (types.Count(m => vyjimkyClassif.Contains(m.Key)) > 0
                    && smluvniStrany.Any(m => m.ESA2010?.StartsWith("12") == true) == false
                    )
                {
                    foreach (var vc in vyjimkyClassif)
                        types.Remove(vc);
                }

                Smlouva.SClassification.Classification[] newClass = types
                    .Select(m => new Smlouva.SClassification.Classification()
                    {
                        TypeValue = (int)m.Key,
                        ClassifProbability = m.Value
                    }
                    ).ToArray();

                //

                var newClassRelevant = smlouva.relevantClassif(newClass);
                smlouva.Classification = new Smlouva.SClassification(newClassRelevant)
                {
                    LastUpdate = DateTime.Now
                };
            }
            return true;
        }
    }
}