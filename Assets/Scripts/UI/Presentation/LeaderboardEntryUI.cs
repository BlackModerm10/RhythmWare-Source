using TMPro;
using UnityEngine;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    public sealed class LeaderboardEntryUI : MonoBehaviour
    {
        public static readonly float[] Columns={0,.07f,.37f,.60f,.76f,.9f,1};
        readonly TextMeshProUGUI[] labels=new TextMeshProUGUI[6];
        public void Build()
        {
            for(int i=0;i<labels.Length;i++)
            {
                labels[i]=CyberUI.Label(transform,"",new Vector2(Columns[i],0),new Vector2(Columns[i+1],1),18);
                labels[i].textWrappingMode=TextWrappingModes.NoWrap;labels[i].richText=false;
            }
        }
        public void Bind(LeaderboardScore score,int place)
        {
            labels[0].text=place.ToString();labels[1].text=score.playerName;labels[2].text=score.score.ToString("N0");
            labels[3].text=score.accuracy.ToString("F2")+"%";labels[4].text=score.maxCombo+"x";labels[5].text=score.rank;
            labels[5].color=score.rank?.StartsWith("S")==true?CyberUI.Cyan:Color.white;
        }
    }
}
