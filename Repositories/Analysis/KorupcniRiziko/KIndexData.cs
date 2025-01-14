﻿using HlidacStatu.Entities;
using HlidacStatu.Repositories;
using HlidacStatu.Repositories.ES;

using Nest;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HlidacStatu.Lib.Analysis.KorupcniRiziko
{

    [Nest.ElasticsearchType(IdProperty = nameof(Ico))]
    public partial class KIndexData
    {

        public static KIndexData Empty(string ico, string jmeno = null)
        {
            KIndexData kidx = new KIndexData();
            kidx.Ico = ico;
            kidx.Jmeno = jmeno ?? Firmy.GetJmeno(ico);
            kidx.roky = Consts.AvailableCalculationYears
                .Select(rok => Annual.Empty(rok))
                .ToList();
            return kidx;
        }

        public static int[] KIndexLimits = { 0, 3, 6, 9, 12, 15 };



        /// <summary>
        /// Returns IMG html tag with path to icon.
        /// </summary>
        public static string KindexImageIcon(KIndexLabelValues label, string style, bool showNone = false, string title = "")
        {
            if (string.IsNullOrEmpty(title))
            {
                return $"<img title='K–Index {label.ToString()} - Index klíčových rizik' src='{KIndexLabelIconUrl(label, showNone: showNone)}' class='kindex' style='{style}'>";
            }
            return $"<img title='{title}' src='{KIndexLabelIconUrl(label, showNone: showNone)}' class='kindex' style='{style}'>";
        }

        public bool ContainsKIndexReady()
        {
            return roky.Where(m => m != null).Any(r => r.KIndexReady);
        }

        //todo: neměl by tenhle být deprecated a používat se lastReadyKindex
        [Obsolete("Použij LastReadyKIndex")]
        public Annual LastKIndex(int? maxYear = null)
        {
            maxYear = maxYear ?? HlidacStatu.Util.ParseTools.ToInt(Devmasters.Config.GetWebConfigValue("KIndexMaxYear"));
            if (maxYear.HasValue)
                return roky?
                    .Where(m => m.Rok <= maxYear.Value)?
                    .OrderByDescending(m => m.Rok)?
                    .FirstOrDefault();
            else
                return LastReadyKIndex(maxYear) ?? roky?.OrderByDescending(m => m.Rok)?.FirstOrDefault();
        }

        public Annual LastReadyKIndex(int? maxYear = null)
        {
            maxYear = maxYear ?? HlidacStatu.Util.ParseTools.ToInt(Devmasters.Config.GetWebConfigValue("KIndexMaxYear"));
            if (maxYear.HasValue)
                return roky?
                    .Where(m => m.KIndexReady)?
                    .Where(m => m.Rok <= maxYear.Value)?
                    .OrderByDescending(m => m.Rok)?
                    .FirstOrDefault();
            else
                return roky?
                    .Where(m => m.KIndexReady)?
                    .OrderByDescending(m => m.Rok)?
                    .FirstOrDefault();

        }
        public int? LastKIndexRok()
        {
            return LastReadyKIndex()?.Rok;
        }

        public decimal? LastKIndexValue()
        {
            return LastReadyKIndex()?.KIndex;
        }
        public KIndexLabelValues LastKIndexLabel(int? maxYear = null)
        {
            return LastKIndexLabel(out int? tmp, maxYear);
        }
        public KIndexLabelValues LastKIndexLabel(out int? rok, int? maxYear = null)
        {
            maxYear = maxYear ?? HlidacStatu.Util.ParseTools.ToInt(Devmasters.Config.GetWebConfigValue("KIndexMaxYear"));
            var val = LastReadyKIndex(maxYear);
            rok = null;
            if (val == null)
                return KIndexLabelValues.None;
            else
            {
                rok = val.Rok;
                return val.KIndexLabel;
            }
        }

        public static string GetMetodikaURL() => "https://texty.hlidacstatu.cz/k-index-index-korupcniho-rizika-metodika/";
        public static string GetKratkaMetodikaURL() => "https://texty.hlidacstatu.cz/co-je-to-k-index/";

        public string GetUrl(bool local = true)
        {
            return GetUrl(local, null);
        }
        public string GetUrl(bool local, int? rok = null)
        {
            string url = "/kindex/detail/" + this.Ico;
            if (rok.HasValue)
                url = url + $"?rok={rok.Value}";
            if (!local)
                url = "https://www.hlidacstatu.cz" + url;

            return url;
        }

        List<Annual> _roky = new List<Annual>();
        public List<Annual> roky
        {
            get { return _roky; }

            set
            {
                _roky = value
                    .Where(m => m != null)
                    .Select(m => { m.Ico = this.Ico; return m; })
                    .ToList();
            }
        }

        public Annual ForYear(int year)
        {
            return roky.Where(m => m != null && m.Rok == year).FirstOrDefault();
        }

        public string Ico { get; set; }
        public string Jmeno { get; set; }
        public UcetniJednotkaInfo UcetniJednotka { get; set; } = new UcetniJednotkaInfo();

        [Nest.Date]
        public DateTime LastSaved { get; set; }

        public async Task SaveAsync(string comment, bool useTempDb = false)
        {
            await Backup.CreateBackupAsync(comment, this.Ico, useTempDb);

            //calculate fields before saving
            this.LastSaved = DateTime.Now;
            var client = await Manager.GetESClient_KIndexAsync();
            if (useTempDb)
                client = await Manager.GetESClient_KIndexTempAsync();

            var res = await client.IndexAsync<KIndexData>(this, o => o.Id(this.Ico)); //druhy parametr musi byt pole, ktere je unikatni
            if (!res.IsValid)
            {
                throw new ApplicationException(res.ServerError?.ToString());
            }
        }

        public static KIndexLabelValues CalculateLabel(decimal? value)
        {
            KIndexLabelValues label = KIndexLabelValues.None;
            for (int i = 0; i < KIndexLimits.Length; i++)
            {
                if (value > KIndexLimits[i])
                    label = (KIndexLabelValues)i;
            }

            return label;
        }
        public static async Task<KIndexData> GetDirectAsync((string ico, bool useTempDb) param)
        {
            if (Consts.KIndexExceptions.Contains(param.ico) && param.useTempDb == false)
                return null;

            var client = await Manager.GetESClient_KIndexAsync();
            if (param.useTempDb)
                client = await Manager.GetESClient_KIndexTempAsync();


            var res = await client.GetAsync<KIndexData>(param.ico);
            if (res.Found == false)
                return null;
            else if (!res.IsValid)
            {
                throw new ApplicationException(res.ServerError?.ToString());
            }
            else
            {
                KIndexData f = res.Source;
                //fill Annual
                foreach (var r in f.roky)
                {
                    if (r != null)
                        r.Ico = param.ico;
                }
                return f;
            }
        }



        public static string KIndexLabelColor(KIndexLabelValues label)
        {
            switch (label)
            {
                case KIndexLabelValues.None:
                    return "#000000";
                case KIndexLabelValues.A:
                    return "#00A5FF";
                case KIndexLabelValues.B:
                    return "#0064B4";
                case KIndexLabelValues.C:
                    return "#002D5A";
                case KIndexLabelValues.D:
                    return "#9099A3";
                case KIndexLabelValues.E:
                    return "#F2B41E";
                case KIndexLabelValues.F:
                    return "#D44820";
                default:
                    return "#000000";
            }
        }


        public static string KIndexLabelIconUrl(KIndexLabelValues value, bool local = true, bool showNone = false)
        {
            string url = "";
            if (local == false)
                url = "https://www.hlidacstatu.cz";

            bool hranate = Devmasters.Config.GetWebConfigValue("KIdxIconStyle") == "hranate";
            switch (value)
            {
                case KIndexLabelValues.None:
                    if (showNone)
                        return url + $"/Content/kindex/{(hranate ? "hranate" : "kulate")}/icon-.svg";
                    else
                        return url + "/Content/Img/1x1.png ";
                default:
                    return url + $"/Content/kindex/{(hranate ? "hranate" : "kulate")}/icon{value.ToString()}.svg";

            }
        }

        public async Task<IOrderedEnumerable<Backup>> GetPreviousVersionsAsync(bool futureData = false)
        {
            ElasticClient client = await Manager.GetESClient_KIndexBackupAsync();
            if (futureData)
                client = await Manager.GetESClient_KIndexBackupTempAsync();

            ISearchResponse<Backup> searchResults = null;
            try
            {
                searchResults = await client.SearchAsync<Backup>(s =>
                        s.Query(q => q.Term(f => f.KIndex.Ico, this.Ico)));

                if (searchResults.IsValid && searchResults.Hits.Count > 0)
                {
                    var hits = searchResults.Hits.Select(h => h.Source).OrderByDescending(s => s.Created);
                    return hits;
                }
            }
            catch (Exception e)
            {
                string origQuery = $"ico:{this.Ico};";
                AuditRepo.Add(Audit.Operations.Search, "", "", "KindexFeedback", "error", origQuery, null);
                if (searchResults != null && searchResults.ServerError != null)
                {
                    Manager.LogQueryError<Backup>(searchResults,
                        $"Exception for {origQuery}",
                        ex: e);
                }
                else
                {
                    Util.Consts.Logger.Error(origQuery, e);
                }
            }

            return Enumerable.Empty<Backup>().OrderBy(x => 1);

        }

        public static async Task<Backup> GetPreviousVersionAsync(string id)
        {
            ElasticClient client = await Manager.GetESClient_KIndexBackupAsync();
            if (!string.IsNullOrEmpty(Devmasters.Config.GetWebConfigValue("UseKindexTemp")))
                client = await Manager.GetESClient_KIndexBackupTempAsync();

            try
            {
                var searchResult = await client.GetAsync<Backup>(id);

                if (searchResult.IsValid)
                {
                    return searchResult.Source;
                }
            }
            catch (Exception e)
            {
                string origQuery = $"id:{id};";
                AuditRepo.Add(Audit.Operations.Search, "", "", "KindexFeedback", "error", origQuery, null);
                Util.Consts.Logger.Error(origQuery, e);
            }

            return null;
        }

    }
}

