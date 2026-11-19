using System;

namespace RhythmWare.Boss
{
    // Event-driven actor direction. Frequent notes/hits never restart a pose
    // every frame; attacks coalesce and important reactions get a clear turn.
    public sealed class BonziPerformance
    {
        readonly Func<string,double> duration;
        string current="idle",queued;
        double began,until,nextHurt,nextMock,lastIdle=-100,lastDefault=-100;
        int priority,phase=1;
        bool started,overclock;
        public string Current=>current;
        public double StartedAt=>began;
        public bool Loop { get; private set; }=true;
        public BonziPerformance(Func<string,double> clipDuration){duration=clipDuration;}
        public void Reset(){current="idle";queued=null;began=until=nextHurt=nextMock=0;lastIdle=lastDefault=-100;priority=0;phase=1;started=overclock=false;Loop=true;}
        void Play(string clip,double time,int importance,bool loop=false)
        {current=clip;began=Math.Max(0,time);until=loop?double.PositiveInfinity:began+duration(clip);priority=importance;Loop=loop;}
        public void Attack(string threat,double time)
        {
            if(priority>=10)return;
            string clip=threat=="Spyware"?"scan":threat=="Adware"?"present":"cast";
            if(threat=="Default"){if(time-lastDefault<1||priority>=2&&time<until)return;lastDefault=time;}
            if(priority>=2&&time<until){if(threat!="Default"||queued==null)queued=clip;return;}
            Play(clip,time,2);
        }
        public void Hit(double time)
        {
            if(priority>=10||time<nextHurt||priority>=4&&time<until||priority==2&&time-began<.75)return;
            nextHurt=time+2.4;Play("hurt",time,3);
        }
        public void Miss(double time)
        {if(priority>=10||time<nextMock||priority>=3&&time<until)return;nextMock=time+3.5;if(priority>=2&&time<until)queued="taunt";else Play("taunt",time,2);}
        public void Overclock(bool active,double time)
        {if(priority>=10)return;if(active&&!overclock)Play("stunned",time,5);overclock=active;}
        public void Revived(double time){if(priority>=10)return;Play("intro",time,4);started=true;}
        public void Tick(double time,bool speaking,double speechStarted,int nextPhase,bool contained)
        {
            if(!started){started=true;Play("intro",time,4);}
            if(contained){if(current!="defeat")Play("defeat",time,10);return;}
            if(nextPhase!=phase){phase=nextPhase;if(phase>1){Play(phase==2?"phase":"rage",time,5);return;}}
            if(priority>0&&time<until)return;
            if(queued!=null){string next=queued;queued=null;Play(next,time,2);return;}
            if(speaking){if(current!="talk"||!Loop)Play("talk",Math.Max(0,speechStarted),0,true);return;}
            if(time-lastIdle>7){lastIdle=time;Play("dance",time,1);return;}
            if(current!="idle"||!Loop)Play("idle",time,0,true);
        }
    }
}
