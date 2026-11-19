using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using RhythmWare.Rhythm;

namespace RhythmWare.Core
{
    public static class OnlineCampaigns
    {
        public sealed class Definition {public string key,id,revision,canonical;public int heads,judgments,maxScore;public double lastTail,duration;}
        static List<Definition> definitions;
        public static IReadOnlyList<Definition> All
        {
            get {
                if(definitions!=null)return definitions;definitions=new List<Definition>();
                foreach(var key in new[]{"Sara","Jack","Dinner"})
                {
                    string json=Resources.Load<TextAsset>("CampaignFive/"+key).text;var chart=ChartData.FromJson(json);
                    int n=chart.notes.Count+chart.notes.Count(x=>x.isHoldNote);string hash;
                    using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json.Replace("\r\n","\n")))).Replace("-","").ToLowerInvariant();
                    definitions.Add(new Definition{key=key,id=chart.StableId,revision=hash,canonical=chart.ToJson(),heads=chart.notes.Count,judgments=n,lastTail=chart.notes.Max(x=>x.endTimestamp),duration=key=="Sara"?155:key=="Jack"?107:129,maxScore=n*1000+5*n*(n+1)+100000});
                    UnityEngine.Object.Destroy(chart);
                }
                return definitions;
            }
        }
        public static Definition Find(ChartData chart)=>chart==null?null:All.FirstOrDefault(x=>x.id==chart.StableId&&x.canonical==chart.ToJson());
    }
}
