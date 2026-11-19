using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using RhythmWare.Core;

namespace RhythmWare.Rhythm
{
    // Explicit copy conversion. Originals and old leaderboard identities survive.
    public static class LegacyChartConversion
    {
        public static ChartData ConvertCopy(ChartData source,out string report)
        {
            if(source.laneLayout==LaneLayout.Id){report="Mapa de cinco teclas.";return UnityEngine.Object.Instantiate(source);}
            if(source.laneLayout!=LaneLayout.LegacyId)throw new InvalidOperationException("Formato de carriles no compatible.");
            var copy=UnityEngine.Object.Instantiate(source);copy.chartId=Guid.NewGuid().ToString("N");copy.laneLayout=LaneLayout.Id;copy.formatVersion=2;
            int[] mapping={0,1,1,2,3,3,4};var occupied=new double[LaneLayout.Count];
            for(int i=0;i<occupied.Length;i++)occupied[i]=double.NegativeInfinity;
            copy.notes=new List<NoteData>();int conflicts=0;
            foreach(var note in source.notes.OrderBy(n=>n.timestamp))
            {
                if(note.laneIndex<0||note.laneIndex>=mapping.Length)throw new InvalidOperationException("Carril antiguo inválido.");
                int lane=mapping[note.laneIndex];
                // A merged tap/hold conflict cannot be played. Keep the first
                // event in the copy and explicitly report every omitted event.
                if(note.timestamp<=occupied[lane]+.09){conflicts++;continue;}
                copy.notes.Add(new NoteData{timestamp=note.timestamp,laneIndex=lane,malwareData=note.malwareData,speedMultiplier=note.speedMultiplier,duration=note.duration});
                occupied[lane]=note.endTimestamp;
            }
            copy.songTitle=source.songTitle+" / 5 teclas";
            report="Copia de 7 a 5 teclas: "+copy.notes.Count+" notas; "+conflicts+" conflictos omitidos. Revisa los patrones y guarda con otro nombre. El original se conserva.";
            copy.conversionNotice=report;return copy;
        }
    }
}
