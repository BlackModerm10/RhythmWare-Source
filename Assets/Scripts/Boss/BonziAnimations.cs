using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmWare.Boss
{
    public sealed class BonziAnimations
    {
        [Serializable] class BoundsData { public float x,y,width,height;public Rect Rect=>new Rect(x,y,width,height); }
        [Serializable] class Clip { public string name;public int[] frames;public float fps;public BoundsData bounds; }
        [Serializable] class Layout { public int columns,cellWidth,cellHeight,strideX,strideY,padding;public float fps,pivotX,pivotY;public Clip[] clips; }
        readonly Dictionary<string,Sprite[]> clips=new Dictionary<string,Sprite[]>();
        readonly Dictionary<string,float> rates=new Dictionary<string,float>();
        readonly Dictionary<string,Rect> bounds=new Dictionary<string,Rect>();
        public IEnumerable<string> Names=>clips.Keys;
        public int TotalFrames { get; private set; }
        public bool UsesExtendedAtlas { get; private set; }
        public Vector2 SourcePivot { get; private set; }=new Vector2(100,151);
        public Material Material { get; private set; }
        public bool Available => clips.Count>0&&Material!=null;
        public BonziAnimations()
        {
            var atlas=Resources.Load<Texture2D>("Boss/BonziExtendedAtlas");var json=Resources.Load<TextAsset>("Boss/BonziExtendedClips");
            UsesExtendedAtlas=atlas!=null&&json!=null;
            if(!UsesExtendedAtlas){atlas=Resources.Load<Texture2D>("Boss/BonziAtlas");json=Resources.Load<TextAsset>("Boss/BonziClips");}
            var sourceMaterial=Resources.Load<Material>("Boss/BonziKey");if(atlas==null||json==null||sourceMaterial==null)return;
            Material=new Material(sourceMaterial);Material.mainTexture=atlas;
            var softKey=Resources.Load<Shader>("Boss/BonziSoftKey");if(UsesExtendedAtlas&&softKey!=null&&softKey.isSupported)Material.shader=softKey;
            var layout=JsonUtility.FromJson<Layout>(json.text);
            if(UsesExtendedAtlas)SourcePivot=new Vector2(layout.pivotX,layout.pivotY);
            int strideX=layout.strideX>0?layout.strideX:layout.cellWidth,strideY=layout.strideY>0?layout.strideY:layout.cellHeight;
            Vector2 pivot=UsesExtendedAtlas?new Vector2(SourcePivot.x/layout.cellWidth,1-SourcePivot.y/layout.cellHeight):new Vector2(.5f,.5f);
            var sprites=new Dictionary<int,Sprite>();
            foreach(var clip in layout.clips)
            {
                var frames=new Sprite[clip.frames.Length];
                for(int i=0;i<frames.Length;i++)
                {
                    int n=clip.frames[i];
                    if(!sprites.TryGetValue(n,out var sprite))
                    {
                        sprite=Sprite.Create(atlas,new Rect(n%layout.columns*strideX+layout.padding,atlas.height-(n/layout.columns*strideY+layout.padding+layout.cellHeight),layout.cellWidth,layout.cellHeight),pivot,100,0,SpriteMeshType.FullRect);
                        sprites[n]=sprite;
                    }
                    frames[i]=sprite;
                }
                clips[clip.name]=frames;
                rates[clip.name]=clip.fps>0?clip.fps:layout.fps>0?layout.fps:10;
                bounds[clip.name]=clip.bounds!=null?clip.bounds.Rect:new Rect(60,39,90,112);
            }
            TotalFrames=sprites.Count;
        }
        public double Duration(string name)=>clips.TryGetValue(name,out var frames)?frames.Length/rates[name]:1;
        public Rect VisibleBounds(string name)=>bounds.TryGetValue(name,out var r)?r:new Rect(60,39,90,112);
        public int FrameNumber(string name,double clock,bool loop=true)
        {if(!clips.TryGetValue(name,out var frames))return 0;int index=Mathf.Max(0,(int)(clock*rates[name]));return loop?index%frames.Length:Mathf.Min(index,frames.Length-1);}
        public Sprite Frame(string name,double clock,bool loop=true)
        {
            if(!clips.TryGetValue(name,out var frames))return null;
            return frames[FrameNumber(name,clock,loop)];
        }
        public void Dispose()
        {
            var seen=new HashSet<Sprite>();foreach(var frames in clips.Values)foreach(var s in frames)if(seen.Add(s))UnityEngine.Object.Destroy(s);clips.Clear();
            if(Material!=null)UnityEngine.Object.Destroy(Material);
        }
    }
}
