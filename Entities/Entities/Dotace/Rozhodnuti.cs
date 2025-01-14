﻿using System.Collections.Generic;
using System.Linq;

namespace HlidacStatu.Entities.Dotace
{
    public class Rozhodnuti
    {
        [Nest.Number]
        public decimal? CastkaPozadovana { get; set; }
        [Nest.Number]
        public decimal? CastkaRozhodnuta { get; set; }
        [Nest.Number]
        public decimal? CerpanoCelkem { get; set; }
        [Nest.Boolean]
        public bool? JePujcka { get; set; }
        [Nest.Keyword]
        public string IcoPoskytovatele { get; set; }
        [Nest.Text]
        public string Poskytovatel { get; set; }
        [Nest.Number]
        public int? Rok { get; set; }
        [Nest.Keyword]
        public string ZdrojFinanci { get; set; }
        [Nest.Object]
        public List<Cerpani> Cerpani { get; set; } = new List<Cerpani>();

        public void RecalculateCerpano()
        {
            if (Cerpani is null || Cerpani.Count == 0)
            {
                CerpanoCelkem = null;
            }
            else
            {
                CerpanoCelkem = Cerpani.Sum(c => c.CastkaSpotrebovana);
            }
        }
    }
}
