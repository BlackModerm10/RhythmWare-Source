using System.IO;
using UnityEngine;
using RhythmWare.Rhythm;
using RhythmWare.Malware;
using RhythmWare.UI;

namespace RhythmWare.Boss
{
    public static class BonziEncounter
    {
        public const string Id="bonzi-buddy";
        public static ChartData CreateChart() => CampaignCatalog.CreateChart("Dinner");
        public static LevelItemInfo CreateLevel() => CampaignCatalog.CreateLevels()[2];
        public static int PhaseAt(double time,double duration)
        {
            var chart=ChartManager.Instance?.SelectedChart;
            var starts=chart!=null&&chart.bossId==Id?chart.bossPhaseStarts:null;
            if(starts!=null&&starts.Length==3)return time<starts[1]?1:time<starts[2]?2:3;
            return time<duration*.34?1:time<duration*.67?2:3;
        }
    }
}
