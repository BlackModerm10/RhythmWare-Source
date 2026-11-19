using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RhythmWare.Core;
using RhythmWare.Rhythm;

namespace RhythmWare.UI
{
    public sealed class LeaderboardPanelUI : MonoBehaviour
    {
        TextMeshProUGUI empty, best, title;
        Button scope;ChartData currentChart;bool localMode,built,subscribed;float nextPoll;
        ScrollRect scroll;
        RectTransform content;
        IReadOnlyList<LeaderboardScore> scores;
        readonly List<LeaderboardEntryUI> rows=new List<LeaderboardEntryUI>();
        public void Build()
        {
            title=CyberUI.Label(transform,"GLOBAL // LEADERBOARD",new Vector2(0,.84f),new Vector2(.72f,1),23,CyberUI.Cyan);
            scope=CyberUI.Button(transform,"VER LOCAL",new Vector2(.74f,.85f),new Vector2(1,.99f),()=>{localMode=!localMode;Refresh(currentChart);});
            best=CyberUI.Label(transform,"",new Vector2(0,.7f),new Vector2(1,.85f),19);
            string[] headers={"#","OPERADOR","SCORE","ACC.","COMBO","RANK"};
            for(int i=0;i<headers.Length;i++)
            {
                var header=CyberUI.Label(transform,headers[i],new Vector2(LeaderboardEntryUI.Columns[i],.61f),new Vector2(LeaderboardEntryUI.Columns[i+1],.70f),16,CyberUI.Muted);
                header.rectTransform.offsetMin=new Vector2(2,2);header.rectTransform.offsetMax=new Vector2(-2,-2);
                header.textWrappingMode=TextWrappingModes.NoWrap;
                header.enableAutoSizing=true;header.fontSizeMin=12;header.fontSizeMax=16;
            }
            scroll=CyberUI.Scroll(transform,new Vector2(0,0),new Vector2(1,.61f),out content);
            empty=CyberUI.Label(transform,"NO SCORES YET",new Vector2(.01f,.25f),new Vector2(.98f,.58f),21,CyberUI.Muted);
            for(int i=0;i<9;i++)
            {
                var row=new GameObject("LeaderboardEntry",typeof(RectTransform)).AddComponent<LeaderboardEntryUI>();
                row.transform.SetParent(content,false);row.Build();rows.Add(row);
            }
            scroll.onValueChanged.AddListener(_=>RenderRows());
            built=true;Subscribe();
        }
        public void Refresh(ChartData chart)
        {
            currentChart=chart;bool official=OnlineCampaigns.Find(chart)!=null;scope.gameObject.SetActive(official);
            if(!official)localMode=true;
            if(official&&!localMode){OnlineService.Instance?.RequestBoard(chart);nextPoll=Time.unscaledTime+30;}
            Present();
        }
        void Subscribe(){if(!subscribed&&OnlineService.Instance!=null){OnlineService.Instance.Changed+=Present;subscribed=true;}}
        void OnEnable(){if(built){Subscribe();if(currentChart!=null)Refresh(currentChart);}}
        void OnDestroy(){if(subscribed&&OnlineService.Instance!=null)OnlineService.Instance.Changed-=Present;}
        void Update(){if(built&&!localMode&&currentChart!=null&&Time.unscaledTime>=nextPoll){nextPoll=Time.unscaledTime+30;OnlineService.Instance?.RequestBoard(currentChart);}}
        void Present()
        {
            if(!built||currentChart==null)return;
            scope.GetComponentInChildren<TextMeshProUGUI>().text=localMode?"VER GLOBAL":"VER LOCAL";
            if(!localMode)
            {
                title.text="GLOBAL // LEADERBOARD";var board=OnlineService.Instance?.Cached(currentChart);
                scores=board?.entries??(IReadOnlyList<LeaderboardScore>)Array.Empty<LeaderboardScore>();
                empty.gameObject.SetActive(scores.Count==0);
                empty.text=OnlineService.Instance?.Configured!=true?"La clasificación global todavía no está activada. Tu historial local sigue disponible.":board==null?"Conectando con la clasificación global…":"Sé el primero en completar este nivel.";
                var own=board?.own;best.text=own==null?(OnlineService.Instance?.Status??"Sin conexión global."):$"TU MEJOR: #{own.place} · {own.score:N0} · {own.accuracy:F2}%";
                content.sizeDelta=new Vector2(0,scores.Count*34);RenderRows();return;
            }
            title.text="LOCAL // HISTORIAL";empty.text="TODAVÍA NO HAY RESULTADOS";
            scores=Leaderboards.Provider.GetScores(currentChart.StableId,currentChart.DifficultyId);
            empty.gameObject.SetActive(scores.Count==0);
            string rank=scores.Count==0?"—":scores.OrderBy(s=>Array.IndexOf(new[]{"S+","S","A","B","C","F"},s.rank)).First().rank;
            best.text=scores.Count==0?"Tu próxima partida inicia el historial.":
                $"MEJOR {scores[0].score:N0}  /  {scores[0].accuracy:F2}%  /  {scores[0].maxCombo}x  /  {scores[0].rank}";
            content.sizeDelta=new Vector2(0,scores.Count*34);content.anchoredPosition=Vector2.zero;RenderRows();
        }
        void RenderRows()
        {
            if(scores==null)return;int start=Mathf.Max(0,Mathf.FloorToInt(content.anchoredPosition.y/34));
            for(int i=0;i<rows.Count;i++)
            {
                int index=start+i;rows[i].gameObject.SetActive(index<scores.Count);if(index>=scores.Count)continue;
                rows[i].Bind(scores[index],scores[index].place>0?scores[index].place:index+1);
                var rt=(RectTransform)rows[i].transform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);
                rt.sizeDelta=new Vector2(0,34);rt.anchoredPosition=new Vector2(0,-index*34);
            }
        }
    }
}
