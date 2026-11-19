using UnityEngine;

namespace RhythmWare.UI
{
    // Original generated artwork, cached on each map entry. External covers still win.
    public static class SongCoverFactory
    {
        public static Sprite Create(Color accent,int seed)
        {
            const int size=256;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
            tex.filterMode=FilterMode.Bilinear;var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x-size*.5f)/size,v=(y-size*.5f)/size;
                float radius=Mathf.Sqrt(u*u+v*v),angle=Mathf.Atan2(v,u);
                bool grid=x%32<1||y%32<1;
                float ring=Mathf.Exp(-Mathf.Abs(radius-.28f)*180);
                float signal=Mathf.Exp(-Mathf.Abs(v-Mathf.Sin(u*(13+seed%5)+seed)*.12f)*100)*.35f;
                float diagonal=Mathf.Clamp01(1-Mathf.Abs(u+v-.22f)*15)*.1f;
                float spokes=Mathf.Abs(Mathf.Sin(angle*(3+seed%4)))<.04f&&radius<.20f?.2f:0;
                Color bg=Color.Lerp(CyberUI.Background,CyberUI.Raised,(u+v+1)*.5f);
                pixels[y*size+x]=Color.Lerp(bg,accent,Mathf.Clamp01(ring+signal+diagonal+spokes+(grid?.06f:0)));
            }
            tex.SetPixels(pixels);tex.Apply();return Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
        }
    }
}
