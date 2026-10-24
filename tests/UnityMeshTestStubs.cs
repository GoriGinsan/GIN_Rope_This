// Data-only substitutes for CPU geometry regression tests. These types do not
// verify Unity's native Mesh implementation, rendering, skinning or allocation
// behavior. Compile this file only into the standalone mesh test executable;
// the plugin itself must reference the target game's real Unity assemblies.
using System;
using System.Collections.Generic;
namespace UnityEngine {
 public struct Vector2 {
  public float x,y;
  public Vector2(float x,float y){this.x=x;this.y=y;}
 }
 public struct Vector3 {
  public float x,y,z;
  public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public Vector3 normalized {
   get{double magnitude=Math.Sqrt((double)x*x+(double)y*y+(double)z*z);return magnitude>1e-5?new Vector3((float)(x/magnitude),(float)(y/magnitude),(float)(z/magnitude)):new Vector3();}
  }
 }
 public struct Vector4 {
  public float x,y,z,w;
  public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}
 }
 public struct Color {
  public float r,g,b,a;
  public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
 }
 public enum MeshTopology {Triangles,Lines}
 public sealed class Mesh {
  Vector3[] positions=new Vector3[0],surfaceNormals=new Vector3[0];
  Vector4[] surfaceTangents=new Vector4[0];Color[] vertexColors=new Color[0];
  readonly Dictionary<int,int[]> indices=new Dictionary<int,int[]>();
  readonly Dictionary<int,List<Vector4>> uv=new Dictionary<int,List<Vector4>>();
  readonly Dictionary<int,int> dimensions=new Dictionary<int,int>();
  readonly Dictionary<int,MeshTopology> topology=new Dictionary<int,MeshTopology>();
  int submeshes;
  public int Writes,BlendShapeCount;
  public int vertexCount {get{return positions.Length;}}
  public int subMeshCount {get{return submeshes;}set{submeshes=value;Writes++;}}
  public Vector3[] vertices {get{return (Vector3[])positions.Clone();}set{positions=(Vector3[])value.Clone();Writes++;}}
  public Vector3[] normals {get{return (Vector3[])surfaceNormals.Clone();}set{surfaceNormals=(Vector3[])value.Clone();Writes++;}}
  public Vector4[] tangents {get{return (Vector4[])surfaceTangents.Clone();}set{surfaceTangents=(Vector4[])value.Clone();Writes++;}}
  public Color[] colors {get{return (Color[])vertexColors.Clone();}set{vertexColors=(Color[])value.Clone();Writes++;}}
  public MeshTopology GetTopology(int submesh){return topology.ContainsKey(submesh)?topology[submesh]:MeshTopology.Triangles;}
  public int[] GetIndices(int submesh){return (int[])indices[submesh].Clone();}
  public void SetTriangles(int[] values,int submesh,bool calculateBounds){indices[submesh]=(int[])values.Clone();topology[submesh]=MeshTopology.Triangles;Writes++;}
  public void SetTestTopology(int submesh,MeshTopology value){topology[submesh]=value;}
  public void GetUVs(int channel,List<Vector4> values){if(uv.ContainsKey(channel))values.AddRange(uv[channel]);}
  public int GetTestUvDimension(int channel){return dimensions.ContainsKey(channel)?dimensions[channel]:0;}
  public void SetUVs(int channel,List<Vector2> values){
   List<Vector4> expanded=new List<Vector4>();foreach(Vector2 value in values)expanded.Add(new Vector4(value.x,value.y,0,0));
   SetUv(channel,expanded,2);
  }
  public void SetUVs(int channel,List<Vector3> values){
   List<Vector4> expanded=new List<Vector4>();foreach(Vector3 value in values)expanded.Add(new Vector4(value.x,value.y,value.z,0));
   SetUv(channel,expanded,3);
  }
  public void SetUVs(int channel,List<Vector4> values){SetUv(channel,values,4);}
  void SetUv(int channel,List<Vector4> values,int dimension){uv[channel]=new List<Vector4>(values);dimensions[channel]=dimension;Writes++;}
  public void ClearBlendShapes(){BlendShapeCount=0;Writes++;}
  public void Clear(bool keepVertexLayout){
   positions=new Vector3[0];surfaceNormals=new Vector3[0];surfaceTangents=new Vector4[0];vertexColors=new Color[0];
   indices.Clear();uv.Clear();dimensions.Clear();topology.Clear();submeshes=0;Writes++;
  }
  public void RecalculateBounds(){Writes++;}
#if KKS
  Rendering.IndexFormat format;
  public Rendering.IndexFormat indexFormat {get{return format;}set{format=value;Writes++;}}
  public int GetVertexAttributeDimension(Rendering.VertexAttribute attribute){return GetTestUvDimension((int)attribute-(int)Rendering.VertexAttribute.TexCoord0);}
#endif
 }
}
#if KKS
namespace UnityEngine.Rendering {
 public enum IndexFormat {UInt16,UInt32}
 public enum VertexAttribute {Position=0,Normal=1,Tangent=2,Color=3,TexCoord0=4,TexCoord1,TexCoord2,TexCoord3,TexCoord4,TexCoord5,TexCoord6,TexCoord7}
}
#endif
