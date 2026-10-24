// CPU regression tests for subdivision planning and attribute interpolation.
// Real KK/KKS runtime tests are still required for native mesh and skin behavior.
using System;
using System.Collections.Generic;
using GINRope;
using UnityEngine;
static class RopeMeshSlicesTests {
 static int checks;
 static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
 static bool Near(float a,float b){return Math.Abs((double)a-b)<.00005;}
 static void Equal(Vector3 a,Vector3 b,string message){Check(Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.z,b.z),message);}
 static Vector3 World(Vector3 point,int orientation){
  if(orientation==0)return new Vector3(point.y,point.x,point.z);
  if(orientation==1)return point;
  if(orientation==2)return new Vector3(point.x,point.z,point.y);
  return new Vector3(point.x*.8f-point.y*.6f,point.x*.48f+point.y*.64f-point.z*.6f,point.x*.36f+point.y*.48f+point.z*.8f);
 }
 static Mesh Quad(){
  Mesh mesh=new Mesh();
  mesh.vertices=new Vector3[]{new Vector3(-1,-10,2),new Vector3(1,-10,2),new Vector3(1,10,2),new Vector3(-1,10,2)};
  mesh.subMeshCount=2;mesh.SetTriangles(new int[]{0,1,2},0,false);mesh.SetTriangles(new int[]{0,2,3},1,false);
  AddChannels(mesh);return mesh;
 }
 static void AddChannels(Mesh mesh){
  Vector3[] positions=mesh.vertices,normals=new Vector3[mesh.vertexCount];Vector4[] tangents=new Vector4[mesh.vertexCount];Color[] colors=new Color[mesh.vertexCount];
  List<Vector2> uv2=new List<Vector2>();List<Vector3> uv3=new List<Vector3>();List<Vector4> uv4=new List<Vector4>();
  for(int i=0;i<positions.Length;i++){
   Vector3 point=positions[i];normals[i]=new Vector3(0,0,1);tangents[i]=new Vector4(1,0,0,-1);colors[i]=new Color(point.x,point.y,point.z,1);
   uv2.Add(new Vector2(point.x,point.y));uv3.Add(new Vector3(point.x,point.y,3));uv4.Add(new Vector4(point.x,point.y,3,4));
  }
  mesh.normals=normals;mesh.tangents=tangents;mesh.colors=colors;
  mesh.SetUVs(0,uv2);mesh.SetUVs(1,uv3);mesh.SetUVs(2,uv4);mesh.SetUVs(3,uv4);
#if KKS
  mesh.SetUVs(4,uv2);mesh.SetUVs(5,uv3);mesh.SetUVs(6,uv4);mesh.SetUVs(7,uv4);
#endif
 }
 static Vector3[] Convert(Vector3[] positions,int orientation){Vector3[] result=new Vector3[positions.Length];for(int i=0;i<result.Length;i++)result[i]=World(positions[i],orientation);return result;}
 static double SignedArea(Vector3 a,Vector3 b,Vector3 c){return (((double)b.x-a.x)*((double)c.y-a.y)-((double)b.y-a.y)*((double)c.x-a.x))*.5;}
 static List<Vector4> Uv(Mesh mesh,int channel){List<Vector4> result=new List<Vector4>();mesh.GetUVs(channel,result);return result;}
 static void Rectangles(){
  for(int orientation=0;orientation<4;orientation++)for(int segments=2;segments<=62;segments+=3){
   Mesh mesh=Quad();Vector3[] before=mesh.vertices,world=Convert(before,orientation);
   Vector3[] result=LongitudinalMesh.Subdivide(mesh,world,World(new Vector3(0,-10,2),orientation),World(new Vector3(0,10,2),orientation),segments,65535);
   Vector3[] positions=mesh.vertices,normals=mesh.normals;Vector4[] tangents=mesh.tangents;Color[] colors=mesh.colors;
   Check(positions.Length==4+(segments-1)*3,"Adjacent triangles must share each cut on their common edge.");
   Check(result.Length==positions.Length&&mesh.subMeshCount==2,"Vertex count and material boundaries must be retained.");
   for(int i=0;i<before.Length;i++)Equal(before[i],positions[i],"Original vertices must retain their order and coordinates.");
   double area=0;
   for(int submesh=0;submesh<2;submesh++){
    int[] indices=mesh.GetIndices(submesh);
    for(int i=0;i<indices.Length;i+=3){
     Vector3 a=positions[indices[i]],b=positions[indices[i+1]],c=positions[indices[i+2]];double triangleArea=SignedArea(a,b,c);
     Check(triangleArea>=-1e-7,"Subdivision must retain triangle winding.");area+=triangleArea;
     double low=Math.Min(a.y,Math.Min(b.y,c.y)),high=Math.Max(a.y,Math.Max(b.y,c.y));
     Check(high-low<=20.0/segments+.00001,"A generated face must not span more than one longitudinal band.");
    }
   }
   Check(Math.Abs(area-40)<.0001,"Subdivision must retain the rest surface area.");
   List<Vector4> uv0=Uv(mesh,0),uv1=Uv(mesh,1),uv2=Uv(mesh,2),uv3=Uv(mesh,3);
   Check(mesh.GetTestUvDimension(0)==2&&mesh.GetTestUvDimension(1)==3&&mesh.GetTestUvDimension(2)==4,"Represented UV components must be retained.");
   for(int i=0;i<positions.Length;i++){
    Vector3 point=positions[i];Equal(result[i],World(point,orientation),"World coordinates must match interpolated local geometry.");
    Check(Near(uv0[i].x,point.x)&&Near(uv0[i].y,point.y),"UVs must interpolate along the original edge.");
    Check(uv1[i].z==3&&uv2[i].w==4&&uv3[i].w==4,"Higher UV components must survive subdivision.");
    Check(Near(colors[i].r,point.x)&&Near(colors[i].g,point.y)&&Near(colors[i].b,point.z),"Floating point colors must interpolate without quantization.");
    Check(normals[i].z==1&&tangents[i].x==1&&tangents[i].w==-1,"Normals and tangent handedness must survive subdivision.");
   }
#if KKS
   for(int channel=4;channel<8;channel++){
    List<Vector4> values=Uv(mesh,channel);Check(values.Count==positions.Length,"All eight KKS UV channels must be retained.");
    for(int i=0;i<values.Count;i++)Check(Near(values[i].x,positions[i].x)&&Near(values[i].y,positions[i].y),"Additional KKS UV channels must interpolate.");
   }
#endif
  }
 }
 static void Seams(){
  Mesh mesh=new Mesh();mesh.vertices=new Vector3[]{new Vector3(-1,-10,0),new Vector3(1,-10,0),new Vector3(1,10,0),new Vector3(-1,-10,0),new Vector3(1,-10,0),new Vector3(1,10,0)};
  mesh.subMeshCount=2;mesh.SetTriangles(new int[]{0,1,2},0,false);mesh.SetTriangles(new int[]{3,4,5},1,false);
  List<Vector2> source=new List<Vector2>();Vector3[] initial=mesh.vertices;
  for(int i=0;i<initial.Length;i++)source.Add(new Vector2(initial[i].x+(i>=3?10:0),initial[i].y));mesh.SetUVs(0,source);
  LongitudinalMesh.Subdivide(mesh,initial,new Vector3(0,-10,0),new Vector3(0,10,0),22,65535);
  Vector3[] positions=mesh.vertices;List<Vector4> values=Uv(mesh,0);
  for(int submesh=0;submesh<2;submesh++)foreach(int index in mesh.GetIndices(submesh))
   Check(Near(values[index].x,positions[index].x+submesh*10),"Coincident seam vertices must never be merged across source indices.");
  for(int i=0;i<initial.Length;i++)Equal(initial[i],positions[i],"Duplicated original vertices must be retained.");
 }
 static Mesh Cylinder(int sides){
  List<Vector3> positions=new List<Vector3>();List<int> side=new List<int>(),bottom=new List<int>(),top=new List<int>();
  for(int ring=0;ring<2;ring++)for(int i=0;i<=sides;i++){
   double angle=i*Math.PI*2/sides;positions.Add(new Vector3((float)Math.Cos(angle),ring==0?-10:10,(float)Math.Sin(angle)));
  }
  for(int i=0;i<sides;i++){
   int a=i,b=i+1,c=i+sides+1,d=i+sides+2;side.Add(a);side.Add(c);side.Add(d);side.Add(a);side.Add(d);side.Add(b);
  }
  for(int ring=0;ring<2;ring++){
   int center=positions.Count;positions.Add(new Vector3(0,ring==0?-10:10,0));
   for(int i=0;i<=sides;i++){double angle=i*Math.PI*2/sides;positions.Add(new Vector3((float)Math.Cos(angle),ring==0?-10:10,(float)Math.Sin(angle)));}
   List<int> indices=ring==0?bottom:top;
   for(int i=0;i<sides;i++){indices.Add(center);indices.Add(center+i+1);indices.Add(center+i+2);}
  }
  Mesh mesh=new Mesh();mesh.vertices=positions.ToArray();mesh.subMeshCount=3;
  mesh.SetTriangles(side.ToArray(),0,false);mesh.SetTriangles(bottom.ToArray(),1,false);mesh.SetTriangles(top.ToArray(),2,false);AddChannels(mesh);return mesh;
 }
 static void CylinderCaps(){
  foreach(int segments in new int[]{8,22,62}){
   Mesh mesh=Cylinder(24);Vector3[] before=mesh.vertices;int[] bottom=mesh.GetIndices(1),top=mesh.GetIndices(2);
   LongitudinalMesh.Subdivide(mesh,before,new Vector3(0,-10,0),new Vector3(0,10,0),segments,65535);
   Vector3[] after=mesh.vertices;Check(after.Length>before.Length,"Sparse cylinder sides need intermediate vertices.");
   for(int i=0;i<before.Length;i++)Equal(before[i],after[i],"Cylinder rest vertices must remain unchanged.");
   for(int cap=1;cap<=2;cap++){
    int[] expected=cap==1?bottom:top,actual=mesh.GetIndices(cap);Check(actual.Length==expected.Length,"An end cap on one axial plane must not be multiplied.");
    for(int i=0;i<actual.Length;i++)Check(actual[i]==expected[i],"Flat end-cap topology must remain unchanged.");
   }
   int[] side=mesh.GetIndices(0);
   for(int i=0;i<side.Length;i+=3){double low=Double.PositiveInfinity,high=Double.NegativeInfinity;for(int corner=0;corner<3;corner++){double y=after[side[i+corner]].y;low=Math.Min(low,y);high=Math.Max(high,y);}Check(high-low<=20.0/segments+.00001,"Cylinder sides must be divided along the rest line.");}
  }
 }
 static void Rejections(){
  for(int kind=0;kind<8;kind++){
   Mesh mesh=Quad();Vector3[] world=mesh.vertices;int segments=22,limit=65535;
   if(kind==0)limit=10;
   if(kind==1)mesh.SetTestTopology(1,MeshTopology.Lines);
   if(kind==2)mesh.normals=new Vector3[1];
   if(kind==3)mesh.SetTriangles(new int[]{0,1,99},0,false);
   if(kind==4)world[1]=new Vector3(Single.NaN,0,0);
   if(kind==5)segments=257;
   if(kind==6)mesh.SetUVs(0,new List<Vector2>{new Vector2(0,0)});
   if(kind==7)mesh.colors=new Color[]{new Color(Single.PositiveInfinity,0,0,0),new Color(),new Color(),new Color()};
   mesh.Writes=0;bool rejected=false;
   try{LongitudinalMesh.Subdivide(mesh,world,new Vector3(0,-10,0),new Vector3(0,10,0),segments,limit);}catch(ArgumentException){rejected=true;}
   Check(rejected,"Invalid or excessive input must be rejected.");Check(mesh.Writes==0,"A rejected operation must not mutate the runtime mesh.");
  }
  Mesh dense=Quad();dense.BlendShapeCount=2;dense.Writes=0;Vector3[] initial=dense.vertices;
  Vector3[] unchanged=LongitudinalMesh.Subdivide(dense,initial,new Vector3(0,-10,0),new Vector3(0,10,0),1,65535);
  Check(Object.ReferenceEquals(initial,unchanged)&&dense.Writes==0&&dense.BlendShapeCount==2,"Dense meshes must remain an exact no-op.");
  Mesh excessive=Quad();int[] repeated=new int[30000];for(int i=0;i<repeated.Length;i++)repeated[i]=i%3;
  excessive.SetTriangles(repeated,0,false);excessive.Writes=0;bool bounded=false;
  try{LongitudinalMesh.Subdivide(excessive,excessive.vertices,new Vector3(0,-10,0),new Vector3(0,10,0),256,65535);}catch(ArgumentException){bounded=true;}
  Check(bounded&&excessive.Writes==0,"Predicted triangle-band work must be bounded before building excessive geometry.");
 }
 static void BakedPose(){
  Mesh mesh=Quad();Vector3[] posed=mesh.vertices;for(int i=0;i<posed.Length;i++)posed[i]=new Vector3(posed[i].x+.25f,posed[i].y,posed[i].z+.5f);
  mesh.vertices=posed;mesh.BlendShapeCount=1;
  LongitudinalMesh.Subdivide(mesh,posed,new Vector3(.25f,-10,2.5f),new Vector3(.25f,10,2.5f),22,65535);
  Check(mesh.BlendShapeCount==0,"Only the private baked mesh must discard stale blend-shape deltas when its vertex count changes.");
  Vector3[] result=mesh.vertices;for(int i=0;i<posed.Length;i++)Equal(posed[i],result[i],"The already baked pose must be retained.");
 }
 static void IndexWidth(){
  const int copies=400;Vector3[] positions=new Vector3[copies*4];int[] indices=new int[copies*6];Vector3[] quad=Quad().vertices;
  for(int copy=0;copy<copies;copy++){
   Array.Copy(quad,0,positions,copy*4,4);int at=copy*6,offset=copy*4;
   indices[at]=offset;indices[at+1]=offset+1;indices[at+2]=offset+2;indices[at+3]=offset;indices[at+4]=offset+2;indices[at+5]=offset+3;
  }
  Mesh mesh=new Mesh();mesh.vertices=positions;mesh.subMeshCount=1;mesh.SetTriangles(indices,0,false);mesh.Writes=0;
#if KKS
  Vector3[] result=LongitudinalMesh.Subdivide(mesh,positions,new Vector3(0,-10,0),new Vector3(0,10,0),62,500000);
  Check(result.Length==74800&&mesh.vertexCount==74800,"KKS subdivision must support meshes beyond the 16-bit vertex limit.");
  Check(mesh.indexFormat==UnityEngine.Rendering.IndexFormat.UInt32,"KKS must switch large generated meshes to 32-bit indices.");
#else
  bool rejected=false;
  try{LongitudinalMesh.Subdivide(mesh,positions,new Vector3(0,-10,0),new Vector3(0,10,0),62,500000);}catch(ArgumentException){rejected=true;}
  Check(rejected&&mesh.Writes==0,"KK must reject geometry beyond its 16-bit vertex limit without mutation.");
#endif
 }
 public static void Main(){Rectangles();Seams();CylinderCaps();Rejections();BakedPose();IndexWidth();Console.WriteLine("Mesh subdivision: "+checks+" assertions passed.");}
}
