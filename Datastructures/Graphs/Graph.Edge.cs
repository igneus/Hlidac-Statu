﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace HlidacStatu.DS.Graphs
{
    public partial class Graph
    {
        [System.Diagnostics.DebuggerDisplay("{debuggerdisplay,nq}")]
        public partial class Edge : IComparable<Edge>
        {
            [Obsolete("Tohle už nepoužíváme. Použij raději this.GetHashCode()")]
            [Newtonsoft.Json.JsonIgnore]
            public string UniqId
            {
                get
                {
                    var s = string.Format("{0} ==[{2} -> {3}]==> {1} {4}",
                        From?.UniqId ?? "Ø", To?.UniqId ?? "Ø",
                        RelFrom?.ToShortDateString() ?? "Ø", RelTo?.ToShortDateString() ?? "Ø",
                        Root ? "root" : ""
                        );
                    return s;
                }
            }
            public bool Root { get; set; }
            public Node From { get; set; }
            public Node To { get; set; }
            public DateTime? RelFrom { get; set; }
            public DateTime? RelTo { get; set; }
            public string Descr { get; set; }
            public int VazbaType { get; set; }
            public int Distance { get; set; }
            public Relation.AktualnostType Aktualnost { get; set; }

            public void UpdateAktualnost()
            {
                if (RelTo.HasValue)
                {
                    if ((DateTime.Now.Date - RelTo.Value).TotalDays < 0)
                        Aktualnost = Relation.AktualnostType.Aktualni;
                    else if ((DateTime.Now.Date - RelTo.Value).TotalDays < Relation.NedavnyVztahDelka.TotalDays)
                        Aktualnost = Relation.AktualnostType.Nedavny;
                    else
                        Aktualnost = Relation.AktualnostType.Neaktualni;
                }
                else
                {
                    Aktualnost = Relation.AktualnostType.Aktualni;
                }
            }


            [Newtonsoft.Json.JsonIgnore]
            private string debuggerdisplay
            {
                get
                {
                    var s = string.Format("{0} ==[{2} -> {3}]==> {1}",
                        From?.UniqId ?? "Ø", To?.UniqId ?? "Ø",
                        RelFrom?.ToShortDateString() ?? "Ø", RelTo?.ToShortDateString() ?? "Ø");
                    return s;
                }
            }

            public string Doba(string format = null, string betweenDatesDelimiter = null)
            {
                if (string.IsNullOrEmpty(format))
                    format = "({0})";
                if (string.IsNullOrEmpty(betweenDatesDelimiter))
                    betweenDatesDelimiter = " - ";
                string datumy = string.Empty;

                if (RelFrom.HasValue && RelTo.HasValue)
                {
                    datumy = string.Format("{0}{2}{1}", RelFrom.Value.ToShortDateString(), RelTo.Value.ToShortDateString(), betweenDatesDelimiter);
                }
                else if (RelTo.HasValue)
                {
                    datumy = string.Format("do {0}", RelTo.Value.ToShortDateString());
                }
                else if (RelFrom.HasValue)
                {
                    datumy = string.Format("od {0}", RelFrom.Value.ToShortDateString());
                }
                if (string.IsNullOrEmpty(datumy))
                    return string.Empty;

                return string.Format(format, datumy);
            }

            public double NumberOfDaysFromToday()
            {
                var today = DateTime.Now;
                if (RelFrom.HasValue && RelTo.HasValue)
                    return (today - RelTo.Value).TotalDays;
                else if (RelFrom.HasValue == false && RelTo.HasValue)
                    return (today - RelTo.Value).TotalDays;
                else if (RelFrom.HasValue && RelTo.HasValue == false)
                    return 0;
                else
                    return 0;

            }

            public double LengthOfEdgeInDays()
            {
                if (RelFrom.HasValue && RelTo.HasValue)
                    return (RelTo.Value - RelFrom.Value).TotalDays;
                else if (RelFrom.HasValue == false && RelTo.HasValue)
                    return (RelTo.Value - new DateTime(1990, 1, 1)).TotalDays;
                else if (RelFrom.HasValue && RelTo.HasValue == false)
                    return (DateTime.Now - RelFrom.Value).TotalDays;
                else
                    return double.MaxValue;

            }

            public static List<Edge> Merge(List<Edge> relFirst, List<Edge> relSecond)
            {

                if (relFirst == null)
                    relFirst = new List<Edge>();
                if (relSecond == null)
                    relSecond = new List<Edge>();

                //merge old and new 
                //first clean old
                //relFromDB = relFromDB.Where(m => !string.IsNullOrEmpty(m.To.Id)).ToList();
                //relFromFirmo = relFromFirmo.Where(m => !string.IsNullOrEmpty(m.To.Id)).ToList();

                List<Edge> finalRelations = new List<Edge>();
                for (int i = 0; i < relSecond.Count(); i++)
                {
                    var v = relSecond[i];
                    int? same = null;
                    if (relFirst != null)
                    {
                        for (int j = 0; j < relFirst.Count(); j++)
                        {
                            var oldV = relFirst[j];
                            if (oldV.To.Id == v.To.Id && oldV.To.Type == oldV.To.Type)
                            {

                                if (
                                        (oldV.RelFrom == v.RelFrom || oldV.RelTo == v.RelTo)
                                    //&& (oldV.Relationship == v.Relationship)
                                    )
                                {
                                    same = j;
                                    break;
                                }

                            }
                        }
                    }
                    if (same.HasValue)
                    {
                        var vl = relFirst.ToList();
                        vl.RemoveAt(same.Value);
                        relFirst = vl.ToList();
                    }
                    finalRelations.Add(v);

                }

                return finalRelations.Concat(relFirst).ToList();

            }

            [Obsolete()]
            public static Edge[] GetLongestEdges_obsolete(IEnumerable<Edge> relations)
            {
                if (relations == null)
                    return null;
                if (relations.Count() < 2)
                    return relations.ToArray();

                var longestE = new List<Edge>();

                var uniqEdges = relations
                                    .Select(r => string.Join("|", r.From.UniqId, r.To.UniqId))
                                    .Distinct();

                foreach (var uniq in uniqEdges)
                {
                    var eParts = uniq.Split('|');

                    var le = GetLongestEdge(relations
                                 .Where(r => r.From.UniqId == eParts[0] && r.To.UniqId == eParts[1]));

                    if (le != null)
                        longestE.Add(le);
                }

                return longestE.ToArray();
            }

            public static Edge[] GetLongestEdges(IEnumerable<Edge> relations)
            {
                var longestE = new List<Edge>();

                var uniqEdges = relations
                    .Select(r => string.Join("|", r.From?.UniqId ?? "", r.To?.UniqId ?? "", r.Distance.ToString()))
                    .Distinct();

                foreach (var uniq in uniqEdges)
                {
                    var eParts = uniq.Split('|');

                    var le = GetLongestEdgesBetweenSameNodes(relations
                                 .Where(r => (r.From?.UniqId ?? "") == eParts[0] && (r.To?.UniqId ?? "") == eParts[1] && r.Distance.ToString() == eParts[2]));

                    if (le != null)
                        longestE.AddRange(le);
                }

                return longestE.ToArray();
            }


            public static Edge[] GetLongestEdgesBetweenSameNodes(IEnumerable<Edge> relations)
            {
                if (relations == null)
                    return null;
                if (relations.Count() < 2)
                    return relations.ToArray();

                var uniqEdges = relations
                    .Select(r => string.Join("|", r.From?.UniqId ?? "", r.To?.UniqId ?? "", r.Distance.ToString()))
                    .Distinct();
                if (uniqEdges.Count() > 1)
                    throw new ArgumentOutOfRangeException("only same nodes and distance in relations");

                //if rels without limits, take it
                if (relations.Any(r => r.RelFrom.HasValue == false && r.RelTo.HasValue == false))
                {
                    var longest = GetLongestEdge(relations);
                    var e = new Edge()
                    {
                        Descr = longest.Descr,
                        From = longest.From,
                        To = longest.To,
                        Distance = longest.Distance,
                        RelFrom = null,
                        RelTo = null
                    };
                    e.UpdateAktualnost();
                    return new Edge[] { e };
                }

                bool changed = false;
                var tmp = new List<Edge>(relations.ToArray());
                do
                {
                    changed = false;

                    for (int i = 0; i < tmp.Count; i++)
                    {
                        for (int j = 0; j < tmp.Count; j++)
                        {
                            if (j == i)
                                continue;
                            if (Devmasters.DT.Util
                                .IsContinuingIntervals(tmp[i].RelFrom, tmp[i].RelTo, tmp[j].RelFrom, tmp[j].RelTo))
                            {
                                var merged = MergeEdges(tmp[i], tmp[j]);
                                if (i > j)
                                {
                                    tmp.RemoveAt(i);
                                    tmp.RemoveAt(j);
                                }
                                else
                                {
                                    tmp.RemoveAt(j);
                                    tmp.RemoveAt(i);
                                }
                                tmp.Insert(0, merged);
                                changed = true;
                                break;
                            }
                        }
                        if (changed)
                            break;
                    }

                } while (changed);

                var finalList = tmp.ToArray();
                return finalList;
            }
            public static MergedEdge MergeSameEdges(IEnumerable<Edge> edges)
            {
                if (edges == null)
                    return null;
                if (edges.Count() == 0)
                    return null;
                if (edges.Count() == 1)
                    return new MergedEdge(edges.First());
                var list = edges.ToList();
                MergedEdge ret = new MergedEdge(edges.First());
                for (int i = 1; i < list.Count; i++)
                {
                    ret = ret.MergeWith(list[i]);
                }
                return ret;
            }

            private static Edge MergeEdges(Edge e1, Edge e2)
            {
                Edge longest = GetLongestEdge(new[] { e1, e2 });
                Edge e = new Edge();

                if (e1.Descr == e2.Descr)
                    e.Descr = e1.Descr;
                else if (e1.Descr.Contains(e2.Descr))
                    e.Descr = e1.Descr;
                else if (e2.Descr.Contains(e1.Descr))
                    e.Descr = e2.Descr;
                else
                    e.Descr = e1.Descr + ", " + e2.Descr;
                e.From = longest.From;
                e.To = longest.To;
                e.Distance = longest.Distance;
                e.RelFrom = Devmasters.DT.Util.LowerDate(e1.RelFrom, e2.RelFrom);
                e.RelTo = Devmasters.DT.Util.HigherDate(e1.RelTo, e2.RelTo);
                e.UpdateAktualnost();
                return e;
            }

            public static Edge GetLongestEdge(IEnumerable<Edge> relations)
            {
                DateTime? fromDate = relations.Any(m => m.RelFrom == null) ? (DateTime?)null : relations.Min(m => m.RelFrom);
                DateTime? toDate = relations.Any(m => m.RelTo == null) ? (DateTime?)null : relations.Max(m => m.RelTo);
                Edge bestRelation = relations.Where(r => r.RelFrom == fromDate && r.RelTo == toDate).FirstOrDefault();
                if (bestRelation == null)
                {
                    return relations.OrderByDescending(m => m.LengthOfEdgeInDays()).FirstOrDefault();
                }
                else
                    return bestRelation;
            }

            public static Edge GetNewestPossibleEdge(IEnumerable<Edge> relations, DateTime? fromDate, DateTime? toDate)
            {
                Edge bestRelation = relations.Where(r =>
                                                            (fromDate.HasValue == false || fromDate <= r.RelFrom)
                                                            &&
                                                            (toDate.HasValue == false || r.RelTo <= toDate)
                                                        )
                                                .OrderByDescending(m => fromDate)
                                                .FirstOrDefault();
                if (bestRelation == null)
                {
                    //bestRelation = relations.Where(m => m.Relationship == RelationSimpleEnum.Souhrnny).FirstOrDefault();
                    return relations.OrderByDescending(m => m.LengthOfEdgeInDays()).FirstOrDefault();
                }
                else
                    return bestRelation;
            }

            public int CompareTo(Edge other)
            {
                if (other == null)
                    return 1;
                if (GetHashCode() == other.GetHashCode())
                    return 0;
                else
                    return -1;
            }
            public bool HasSameEdges(Edge other)
            {
                if (other == null)
                    return false;
                var same = (this.From?.UniqId == other.From?.UniqId
                               && this.To?.UniqId == other.To?.UniqId)
                        || (this.From?.UniqId == other.To?.UniqId
                               && this.To?.UniqId == other.From?.UniqId);
                return same;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(From, To, RelFrom, RelTo, Root);
                //unchecked
                //{
                //    int hash = 17;
                //    hash = hash * 23 + (From == null ? 0 : From.GetHashCode() );
                //    hash = hash * 23 + (To == null ? 0 : To.GetHashCode());
                //    hash = hash * 23 + (RelFrom==null ? 0 : RelFrom.Value.ToShortDateString().GetHashCode()) ;
                //    hash = hash * 23 + (RelTo == null ? 0 : RelTo.Value.ToShortDateString().GetHashCode());
                //    hash = hash * 23 + Root.GetHashCode();
                //    return hash;

                //}
            }
        }

    }

}
