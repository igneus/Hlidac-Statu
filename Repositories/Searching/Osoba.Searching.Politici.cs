﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HlidacStatu.Repositories.Searching
{
    public class Politici
    {
        public static Random Rnd = new Random();

        public static List<Tuple<string, string[]>> PoliticiStems = null;

        static object initLock = new object();

        static Politici()
        {
            if (PoliticiStems == null)
            {
                lock (initLock)
                {
                    if (PoliticiStems == null)
                    {
                        PoliticiStems = InitPoliticiStems();
                        // Newtonsoft.Json.JsonConvert.DeserializeObject<List<Tuple<string, string[]>>>(
                        //System.IO.File.ReadAllText(@"politiciStem.json")
                        //);
                    }
                }
            }
        }

        static List<Tuple<string, string[]>> InitPoliticiStems()
        {
            HashSet<string> slova = new HashSet<string>();
            string[] prefixes = ("pan kolega poslanec předseda místopředseda prezident premiér "
                                 + "paní slečna kolegyně poslankyně předsedkyně místopředsedkyně prezidentka premiérka ")
                .Split(' ');
            string[] blacklist = { "poslanec celý" };
            string[] whitelist = { };

            var politiciStems = new List<Tuple<string, string[]>>();

            var path = Connectors.Init.WebAppDataPath;
            if (string.IsNullOrWhiteSpace(path))
                path = Devmasters.IO.IOTools.GetExecutingDirectoryName(true);

            foreach (var s in System.IO.File.ReadAllLines(path + "Czech.3-2-5.dic"))
            {
                slova.Add(s);
            }

            foreach (var p in OsobaRepo.Politici.Get())
            {
                var cols = new string[] { p.Jmeno.ToLower(), p.Prijmeni.ToLower() };
                var key = p.NameId;
                
                List<string> names = new List<string>();
                for (int i = 1; i < cols.Length; i++)
                {
                    string jmeno = cols[0];
                    string prijmeni = cols.Skip(1).Take(i).Aggregate((f, s) => f + " " + s);
                    names.Add(jmeno + " " + prijmeni);
                    if (!slova.Contains(prijmeni))
                        names.Add(prijmeni);
                }

                foreach (var n in names)
                {
                    var fname = (n).Split(' ');
                    if (!blacklist.Contains(n) && fname.Length > 1)
                        politiciStems.Add(new Tuple<string, string[]>(key, fname));

                    foreach (var pref in prefixes)
                    {
                        if (!blacklist.Contains(pref + " " + n))
                        {
                            var nn = (pref + " " + n).Split(' ');
                            if (nn.Length > 1)
                                politiciStems.Add(new Tuple<string, string[]>(key, nn));
                        }
                    }
                }
            }

            return politiciStems;
        }

        public static async Task<string[]> StemsAsync(string text)
        {
            using var wc = new System.Net.Http.HttpClient();
            var data = new System.Net.Http.StringContent(Newtonsoft.Json.JsonConvert.ToString(text));
            var res = await wc.PostAsync(classificationBaseUrl() + "/text_stemmer?ngrams=1", data).ConfigureAwait(false);

            return Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(await res.Content.ReadAsStringAsync().ConfigureAwait(false));
        }

        private static string classificationBaseUrl()
        {
            string[] baseUrl = Devmasters.Config.GetWebConfigValue("Classification.Service.Url")
                .Split(',', ';');
            //Dictionary<string, DateTime> liveEndpoints = new Dictionary<string, DateTime>();

            return baseUrl[Rnd.Next(baseUrl.Length)];
        }

        /// <summary>
        /// returns Osoba.NameId[]
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static async Task<string[]> FindCitationsAsync(string text)
        {
            var stopw = new Devmasters.DT.StopWatchEx();
            stopw.Start();
            string[] sText = await StemsAsync(text);
            stopw.Stop();
            //Console.WriteLine($"stemmer {stopw.ExactElapsedMiliseconds} ");
            stopw.Restart();
            List<string> found = new List<string>();
            foreach (var kv in PoliticiStems)
            {
                string zkratka = kv.Item1;
                string[] politik = kv.Item2;

                for (int i = 0; i < sText.Length - (politik.Length - 1); i++)
                {
                    bool same = true;
                    for (int j = 0; j < politik.Length; j++)
                    {
                        if (sText[i + j] == politik[j])
                            same = same & true;
                        else
                        {
                            same = false;
                            break;
                        }
                    }

                    if (same)
                    {
                        if (!found.Contains(zkratka))
                            found.Add(zkratka);
                        break;
                    }
                }
            }

            stopw.Stop();
            //Console.WriteLine($"location {stopw.ExactElapsedMiliseconds} ");
            return found.ToArray();
        }
    }
}