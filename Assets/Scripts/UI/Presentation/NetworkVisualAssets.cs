using UnityEngine;
using RhythmWare.Malware;

namespace RhythmWare.UI
{
    public static class NetworkVisualAssets
    {
        static readonly Sprite[] shapes=new Sprite[18];
        static Material material;
        static Sprite glow;
        public static Sprite Glow
        {
            get
            {
                if(glow!=null)return glow;
                const int size=128;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Bilinear;
                var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {float r=Vector2.Distance(new Vector2(x,y),new Vector2(63.5f,63.5f))/64;pixels[y*size+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2));}
                texture.SetPixels(pixels);texture.Apply();glow=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);return glow;
            }
        }
        public static Material Material => material!=null?material:(material=new Material(Shader.Find("Sprites/Default")));
        public static int Kind(MalwareData malware)
        {
            if(malware is SpywareData)return 1;if(malware is RansomwareData)return 2;if(malware is TrojanData)return 3;
            if(malware is AdwareData)return 4;if(malware is DDoSData)return 5;if(malware is WormData)return 6;
            if(malware is PhishingData)return 7;if(malware is KeyloggerData)return 8;if(malware is RootkitData)return 9;return 0;
        }
        public static Sprite Shape(int kind)
        {
            if(shapes[kind]!=null)return shapes[kind];
            int resolution=kind>=14?512:128;
            var tex=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false);tex.filterMode=FilterMode.Bilinear;
            var pixels=new Color32[resolution*resolution];
            for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++)
            {
                float a=(x-(resolution-1)*.5f)/(resolution*.5f),b=(y-(resolution-1)*.5f)/(resolution*.5f),r=Mathf.Sqrt(a*a+b*b);bool inside;
                switch(kind)
                {
                    case 1: inside=a*a/.72f+b*b/.24f<1;break; // eye
                    case 2: inside=Mathf.Abs(a)<.72f&&Mathf.Abs(b)<.7f && !(b>.25f&&Mathf.Abs(a)<.36f);break; // lock
                    case 3: inside=b>-.72f&&b<.85f&&Mathf.Abs(a)<(.85f-b)*.56f;break; // triangle
                    case 4: inside=Mathf.Abs(a)<.8f&&Mathf.Abs(b)<.58f;break; // window
                    case 5: inside=Mathf.Abs(b)<.8f&&Mathf.Abs(a)<.8f && Mathf.Abs(b% .42f)<.22f;break; // packet stack
                    case 6: inside=Mathf.Abs(b-Mathf.Sin(a*5)*.22f)<.26f&&Mathf.Abs(a)<.85f;break; // worm
                    case 7: inside=r<.78f&&r>.33f&&!(a<0&&b>0);break; // hook
                    case 8: inside=Mathf.Abs(a)<.83f&&Mathf.Abs(b)<.52f&&!(Mathf.Abs(a)<.15f&&b>0);break; // keyboard
                    case 9: inside=r<.8f&&(r>.48f||Mathf.Abs(a)<.14f||Mathf.Abs(b)<.14f);break; // kernel
                    case 10: inside=r<.94f&&r>.79f;break; // ring
                    case 11: inside=Mathf.Abs(a)<.96f&&Mathf.Abs(b)<.94f&&(Mathf.Abs(a)>.90f||Mathf.Abs(b)>.84f);break; // SPACE frame
                    case 12: inside=r<.94f;break; // contrast disc behind character art
                    case 13: inside=Mathf.Abs(a)<.96f&&Mathf.Abs(b)<.94f;break; // keycap fill
                    case 14: inside=Mathf.Abs(a)+Mathf.Abs(b)<.94f&&Mathf.Abs(a)+Mathf.Abs(b)>.79f;break;
                    case 15: inside=Mathf.Abs(a)+Mathf.Abs(b)<.94f;break;
                    case 16: inside=Mathf.Abs(b)<.94f&&Mathf.Abs(a)+Mathf.Abs(b)*.18f<.96f&&
                        (Mathf.Abs(b)>.80f||Mathf.Abs(a)+Mathf.Abs(b)*.18f>.91f);break;
                    case 17: inside=Mathf.Abs(b)<.94f&&Mathf.Abs(a)+Mathf.Abs(b)*.18f<.96f;break;
                    default: inside=Mathf.Abs(a)+Mathf.Abs(b)<.9f;break; // packet diamond
                }
                pixels[y*resolution+x]=inside?(Color32)Color.white:(Color32)Color.clear;
            }
            tex.SetPixels32(pixels);tex.Apply();shapes[kind]=Sprite.Create(tex,new Rect(0,0,resolution,resolution),new Vector2(.5f,.5f),resolution);return shapes[kind];
        }
        public static LineRenderer Line(Transform parent,string name,Vector3[] points,Color color,float width,int order=-45)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var lr=go.AddComponent<LineRenderer>();
            lr.sharedMaterial=Material;lr.positionCount=points.Length;lr.SetPositions(points);lr.useWorldSpace=true;
            lr.startColor=lr.endColor=color;lr.startWidth=lr.endWidth=width;lr.sortingOrder=order;lr.numCornerVertices=3;return lr;
        }
    }
}
