﻿using Devmasters.Collections;

using HlidacStatu.Entities;
using HlidacStatu.Entities.Enhancers;

using System.Collections.Generic;

namespace HlidacStatu.Plugin.Enhancers
{


    public class FormalDataNormalizer : IEnhancer
    {

        public int Priority => 1;

        public string Description
        {
            get
            {
                return "FormalNormalizer updater";
            }
        }

        public string Name
        {
            get
            {
                return "FormalNormalizer";
            }
        }

        List<string> ciziStaty = new List<string>();
        public void SetInstanceData(object data)
        {
            var test = data as List<string>;
            if (test != null)
                ciziStaty = test;
        }
        bool changed = false;
        public bool Update(ref Smlouva item)
        {


            if (item.Platce != null)
                item.Platce.ico = GetNormalizedIco(item.Platce.ico, "platce.ico", ref item);

            if (item.VkladatelDoRejstriku != null)
                item.VkladatelDoRejstriku.ico = GetNormalizedIco(item.VkladatelDoRejstriku.ico, "vkladatelDoRejstriku.ico", ref item);

            if (item.Prijemce != null)
            {
                foreach (var p in item.Prijemce)
                {
                    p.ico = GetNormalizedIco(p.ico, "prijemce.ico", ref item);
                }
            }

            return changed;
        }

        private string GetNormalizedIco(string ico, string parametrName, ref Smlouva item)
        {
            if (!string.IsNullOrEmpty(ico))
            {
                var newIco = System.Text.RegularExpressions.Regex.Replace(ico, @"[^0-9]", string.Empty);
                newIco = Util.ParseTools.NormalizeIco(newIco);
                if (newIco != ico && Util.DataValidators.CheckCZICO(newIco) && Util.DataValidators.IsFirmaIcoZahranicni(ico) == false)
                {
                    item.Enhancements = item.Enhancements.AddOrUpdate(new Enhancement("Normalizováno IČO", "", parametrName, ico, newIco, this));
                    changed = true;
                    return newIco;
                }
            }
            return ico;
        }

    }
}
