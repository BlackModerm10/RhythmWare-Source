using UnityEngine;
using RhythmWare.Core;

namespace RhythmWare.Rhythm
{
    [System.Serializable]
    public class TimingWindow
    {
        public const float BasePerfect=.065f,BaseGood=.110f,BaseMiss=.160f;
        public static TimingWindow ForChart(ChartData chart)
        {
            float scale=chart!=null?Mathf.Clamp(chart.timingWindowScale,.75f,2):1;
            return new TimingWindow{perfectWindowSec=BasePerfect*scale,goodWindowSec=BaseGood*scale,missWindowSec=BaseMiss*scale};
        }
        [Header("Hit Windows (in seconds)")]
        public float perfectWindowSec = BasePerfect;
        public float goodWindowSec = BaseGood;
        public float missWindowSec = BaseMiss;

        public HitRating Evaluate(double timeDifferenceSec)
        {
            double absDiff = System.Math.Abs(timeDifferenceSec);

            if (absDiff <= perfectWindowSec)
            {
                return HitRating.Perfect;
            }
            if (absDiff <= goodWindowSec)
            {
                return HitRating.Good;
            }
            return HitRating.Miss;
        }

        public bool CanHit(double timeDifferenceSec)
        {
            return System.Math.Abs(timeDifferenceSec) <= missWindowSec;
        }
    }
}
