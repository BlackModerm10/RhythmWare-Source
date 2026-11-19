using System;
using System.Collections.Generic;
using System.Linq;

namespace RhythmWare.Core
{
    public sealed class AudioCalibrationSession
    {
        public const double Period=.6, MaximumError=.28;
        public const int ListenBeats=4, MeasureBeats=24, TargetSamples=16;
        readonly List<double> errors=new List<double>();readonly HashSet<int> beats=new HashSet<int>();
        public double StartDsp { get; private set; }
        public double EndDsp=>StartDsp+(ListenBeats+MeasureBeats)*Period+MaximumError;
        public int Count=>errors.Count;
        public void Begin(double dsp){StartDsp=dsp;errors.Clear();beats.Clear();}
        public bool Record(double dsp)
        {
            if(double.IsNaN(dsp)||double.IsInfinity(dsp)||Count>=TargetSamples)return false;
            int beat=(int)Math.Round((dsp-StartDsp)/Period);double error=dsp-(StartDsp+beat*Period);
            if(beat<ListenBeats||beat>=ListenBeats+MeasureBeats||Math.Abs(error)>MaximumError||beats.Contains(beat))return false;
            beats.Add(beat);errors.Add(error);return true;
        }
        static double Median(IEnumerable<double> values){var a=values.OrderBy(v=>v).ToArray();return a.Length==0?0:a.Length%2==0?(a[a.Length/2-1]+a[a.Length/2])*.5:a[a.Length/2];}
        public double MedianError=>Median(errors);
        public double Variation=>1.4826*Median(errors.Select(e=>Math.Abs(e-MedianError)));
        public int UsableSamples=>errors.Count(e=>Math.Abs(e-MedianError)<=Math.Max(.020,3*Variation));
        public bool HasEstimate=>Count>=12&&UsableSamples>=12&&Variation<=.060;
        public float SuggestedOffset=>(float)(Math.Round(-MedianError/.005)*.005);
    }
}
