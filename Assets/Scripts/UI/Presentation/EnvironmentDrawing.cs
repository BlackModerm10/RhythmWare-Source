using System.Collections.Generic;
using UnityEngine;

namespace RhythmWare.UI
{
    // World-space environment drawing: never creates a Canvas or intercepts input.
    // Mesh storage and text objects are reused for the lifetime of the background.
    public sealed class EnvironmentDrawing
    {
        readonly GameObject root;
        readonly Mesh mesh;
        readonly List<Vector3> vertices=new List<Vector3>(8192);
        readonly List<Color> colors=new List<Color>(8192);
        readonly List<Vector2> uv=new List<Vector2>(8192);
        readonly List<int> triangles=new List<int>(12288);
        readonly List<TextMesh> labels=new List<TextMesh>();
        readonly int order;
        Rect world;
        int used;
        public bool Visible { get=>root!=null&&root.activeSelf;set{if(root!=null&&root.activeSelf!=value)root.SetActive(value);} }
        public int VertexCount=>vertices.Count;
        public EnvironmentDrawing(Transform parent,string name,int sortingOrder)
        {
            order=sortingOrder;root=new GameObject(name);root.transform.SetParent(parent,false);
            mesh=new Mesh{name=name};mesh.MarkDynamic();root.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=NetworkVisualAssets.Material;renderer.sortingOrder=order;
        }
        public void Begin(){vertices.Clear();colors.Clear();uv.Clear();triangles.Clear();used=0;world=EpicStageLayout.WorldRect(Camera.main);}
        Vector3 Point(Vector2 p)=>new Vector3(world.xMin+p.x/1920*world.width,world.yMax-p.y/1080*world.height,8);
        public void Polygon(Vector2[] p,Color color)
        {
            int start=vertices.Count;foreach(var pixel in p){vertices.Add(Point(pixel));colors.Add(color);uv.Add(Vector2.zero);}
            for(int i=1;i<p.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
        }
        void Vertex(Vector2 p,Color color){vertices.Add(Point(p));colors.Add(color);uv.Add(Vector2.zero);}
        void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int start=vertices.Count;Vertex(a,color);Vertex(b,color);Vertex(c,color);Vertex(d,color);
            triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
        }
        public void Quad(Rect r,Color color)=>Quad(new Vector2(r.x,r.y),new Vector2(r.xMax,r.y),new Vector2(r.xMax,r.yMax),new Vector2(r.x,r.yMax),color);
        public void Gradient(Rect r,Color left,Color right)
        {
            int start=vertices.Count;Quad(r,left);colors[start+1]=colors[start+2]=right;
        }
        public void Line(Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 d=b-a;if(d.sqrMagnitude<.0001f)return;Vector2 n=new Vector2(-d.y,d.x).normalized*width*.5f;
            Quad(a+n,b+n,b-n,a-n,color);
        }
        public void Ring(Vector2 center,float radius,float width,Color color)
        {for(int i=0;i<24;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,color);}}
        public void RoundedRect(Rect r,float radius,Color color)
        {
            radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);var p=new Vector2[20];
            for(int corner=0;corner<4;corner++)
            {
                Vector2 center=new Vector2(corner==0||corner==3?r.x+radius:r.xMax-radius,corner<2?r.y+radius:r.yMax-radius);
                for(int i=0;i<5;i++){float a=(-Mathf.PI+corner*Mathf.PI*.5f)+i*Mathf.PI/8;p[corner*5+i]=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;}
            }
            Polygon(p,color);
        }
        public void Label(string text,Vector2 position,float height,Color color,bool bold=false)
        {
            TextMesh label;if(used==labels.Count)
            {
                var child=new GameObject("DesktopText");child.transform.SetParent(root.transform,false);label=child.AddComponent<TextMesh>();
                label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=40;label.characterSize=.1f;label.anchor=TextAnchor.UpperLeft;
                var mr=child.GetComponent<MeshRenderer>();mr.sharedMaterial=label.font.material;mr.sortingOrder=order+1;labels.Add(label);
            }
            label=labels[used++];label.gameObject.SetActive(true);if(label.text!=text)label.text=text;label.color=color;label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
            label.transform.position=Point(position);float bounds=label.GetComponent<MeshRenderer>().localBounds.size.y;
            if(bounds>.001f)label.transform.localScale=Vector3.one*(height*world.height/1080/bounds);
        }
        public void End()
        {
            for(int i=used;i<labels.Count;i++)labels[i].gameObject.SetActive(false);
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        public void Dispose(){Object.Destroy(mesh);Object.Destroy(root);}
    }
}
