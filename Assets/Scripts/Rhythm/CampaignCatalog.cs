using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RhythmWare.UI;
using RhythmWare.Malware;
using RhythmWare.Core;

namespace RhythmWare.Rhythm
{
    public static class CampaignCatalog
    {
        public static ChartData CreateChart(string key)
        {
            var source=Resources.Load<TextAsset>("CampaignFive/"+key);if(source==null)throw new FileNotFoundException("Campaign chart missing: "+key);
            var catalog=ChartData.GetDefaultMalwareCatalog();
            if(catalog.TryGetValue("Spyware",out var m)&&m is SpywareData spy){spy.minOpacity=key=="Sara"?.6f:.38f;spy.fadeStartProgress=.4f;}
            var chart=ChartData.FromJson(source.text,catalog);
            chart.audioClipPath=GameContentPaths.ResolveAudio(chart.audioClipPath);return chart;
        }
        public static List<LevelItemInfo> CreateLevels()
        {
            string[] keys={"Sara","Jack","Dinner"};
            string[] titles={"01 / Tutorial guiado","02 / Defensa de la red","03 / Batalla de jefe"};
            string[] descriptions={"Aprende los cinco controles, las notas sostenidas y cómo defenderte de las amenazas. Una práctica guiada te prepara para completar tu primera misión.","Pon a prueba tu pulso y combina lo aprendido. Mantén el firewall a salvo y neutraliza los paquetes infectados.","Entra en el escritorio de Bonzi Buddy. No confíes en su sonrisa: reconoce el espionaje, frena los anuncios y rechaza sus regalos engañosos."};
            var result=new List<LevelItemInfo>();
            for(int i=0;i<keys.Length;i++)
            {
                var chart=CreateChart(keys[i]);result.Add(new LevelItemInfo{levelTitle=titles[i],songTitle=chart.songTitle,artist=chart.artist,bpm=chart.bpm,difficulty=chart.difficulty,noteCount=chart.notes.Count,chartData=chart,audioClipPath=chart.audioClipPath,description=descriptions[i],themeColor=i==0?CandyMenuGraphic.Mint:i==1?CandyMenuGraphic.Pink:new Color(.78f,.53f,1),threatBadge=i==2?"[BOSS]":i==0?"[TUTORIAL]":"[INTERMEDIO]"});
            }
            return result;
        }
    }
}
