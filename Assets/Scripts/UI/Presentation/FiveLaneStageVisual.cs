using System.Collections.Generic;
using UnityEngine;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    // Code-native, transparent stage geometry, independent from every backdrop.
    public sealed class FiveLaneStageVisual : MonoBehaviour
    {
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<GameObject> parts=new List<GameObject>();
        static readonly Color Ink=new Color(.035f,.025f,.08f), Cream=new Color(1,.91f,.72f);
        public void RefreshLayout()
        {
            foreach(var part in parts)Destroy(part);parts.Clear();foreach(var mesh in meshes)Destroy(mesh);meshes.Clear();
            if(Camera.main==null)return;
            float unit=EpicStageLayout.WorldRect(Camera.main).height/1080;
            float topY=EpicStageLayout.OriginPixel.y,bottomY=EpicStageLayout.RoadEndY;
            float topHalf=EpicStageLayout.RoadHalfWidth(topY),bottomHalf=EpicStageLayout.RoadHalfWidth(bottomY);
            for(int row=0;row<LaneLayout.Count;row++)
            {
                int lane=LaneLayout.VisualOrder[row];float a=EpicStageLayout.LaneEdges[row],b=EpicStageLayout.LaneEdges[row+1];
                Color top=new Color(.025f,.022f,.065f),bottom=lane==LaneLayout.Center?new Color(.075f,.060f,.105f):LaneLayout.IsLeft(lane)?new Color(.028f,.065f,.105f):new Color(.080f,.032f,.105f);
                Surface("Column_"+lane,new[]{new Vector2(960+a*topHalf,topY),new Vector2(960+b*topHalf,topY),new Vector2(960+b*bottomHalf,bottomY),new Vector2(960+a*bottomHalf,bottomY)},top,bottom,-78);
            }
            for(int i=0;i<EpicStageLayout.LaneEdges.Length;i++)
            {
                float edge=EpicStageLayout.LaneEdges[i];var points=new[]{new Vector2(960+edge*topHalf,topY),new Vector2(960+edge*bottomHalf,bottomY)};
                bool outer=i==0||i==5;Color tint=outer?Cream:i==2||i==3?new Color(1,.83f,.34f,.70f):i<2?new Color(.43f,1,.80f,.65f):new Color(1,.38f,.77f,.65f);
                Stroke("ColumnInk",points,Ink,(outer?13:8)*unit,-77);Stroke("ColumnEdge",points,tint,(outer?4:2)*unit,-75);
            }
            Stroke("Horizon",new[]{new Vector2(960-topHalf,topY),new Vector2(960+topHalf,topY)},Cream,3*unit,-75);
            float hitHalf=EpicStageLayout.RoadHalfWidth(EpicStageLayout.HitLineY);
            Stroke("JudgmentGlow",new[]{new Vector2(960-hitHalf,EpicStageLayout.HitLineY),new Vector2(960+hitHalf,EpicStageLayout.HitLineY)},new Color(1,.91f,.72f,.13f),22*unit,-74);
            Stroke("JudgmentLine",new[]{new Vector2(960-hitHalf,EpicStageLayout.HitLineY),new Vector2(960+hitHalf,EpicStageLayout.HitLineY)},Cream,3*unit,-73);
        }
        void Surface(string name,Vector2[] pixels,Color top,Color bottom,int order)
        {
            var vertices=new Vector3[pixels.Length];var colors=new Color[pixels.Length];
            for(int i=0;i<vertices.Length;i++){vertices[i]=EpicStageLayout.PixelToWorld(pixels[i]);colors[i]=i<2?top:bottom;}
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.colors=colors;mesh.uv=new Vector2[pixels.Length];mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();meshes.Add(mesh);
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=NetworkVisualAssets.Material;renderer.sortingOrder=order;parts.Add(go);
        }
        void Stroke(string name,Vector2[] pixels,Color color,float width,int order)
        {
            var points=new Vector3[pixels.Length];for(int i=0;i<pixels.Length;i++)points[i]=EpicStageLayout.PixelToWorld(pixels[i]);
            parts.Add(NetworkVisualAssets.Line(transform,name,points,color,width,order).gameObject);
        }
        void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}


