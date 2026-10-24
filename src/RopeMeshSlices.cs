using System;
using System.Collections.Generic;
using UnityEngine;
namespace GINRope {
 // Subdivide only the private, posed runtime mesh. Long source triangles need
 // intermediate vertices before skin weights can make them follow a rope.
 public static class LongitudinalMesh {
  const int MaxIndices=3000000,MaxWork=2000000;
  struct EdgePlane:IEquatable<EdgePlane> {
   public int A,B,Plane;
   public bool Equals(EdgePlane other){return A==other.A&&B==other.B&&Plane==other.Plane;}
   public override bool Equals(object other){return other is EdgePlane&&Equals((EdgePlane)other);}
   public override int GetHashCode(){unchecked{return ((A*397)^B)*397^Plane;}}
  }
  struct Cut {public int A,B;public double T;}
  struct Point {public int Index;public double Distance;}
  sealed class Planner {
   public readonly int OriginalCount,Limit;
   public readonly double[] Distance;
   public readonly List<Cut> Cuts=new List<Cut>();
   readonly Dictionary<EdgePlane,int> cache=new Dictionary<EdgePlane,int>();
   public Planner(int original,int limit,double[] distance){OriginalCount=original;Limit=limit;Distance=distance;}
   public Point Intersection(Point first,Point second,int plane){
    if(first.Distance==plane)return first;
    if(second.Distance==plane)return second;
    int a,b;
    if(first.Index<OriginalCount&&second.Index<OriginalCount){a=first.Index;b=second.Index;}
    else if(first.Index<OriginalCount){
     Cut edge=Cuts[second.Index-OriginalCount];a=edge.A;b=edge.B;
     if(first.Index!=a&&first.Index!=b)throw Bad("A sliced edge is inconsistent.");
    }else if(second.Index<OriginalCount){
     Cut edge=Cuts[first.Index-OriginalCount];a=edge.A;b=edge.B;
     if(second.Index!=a&&second.Index!=b)throw Bad("A sliced edge is inconsistent.");
    }else{
     Cut left=Cuts[first.Index-OriginalCount],right=Cuts[second.Index-OriginalCount];
     a=left.A;b=left.B;
     if(a!=right.A||b!=right.B)throw Bad("A sliced edge is inconsistent.");
    }
    if(a>b){int temporary=a;a=b;b=temporary;}
    EdgePlane key=new EdgePlane{A=a,B=b,Plane=plane};int index;
    if(!cache.TryGetValue(key,out index)){
     if(Cuts.Count>=Limit-OriginalCount)throw Bad("Automatic mesh subdivision would exceed the vertex limit.");
     double denominator=Distance[b]-Distance[a];
     double amount=(plane-Distance[a])/denominator;
     if(!Finite(amount)||amount<0||amount>1)throw Bad("A sliced edge has invalid coordinates.");
     index=OriginalCount+Cuts.Count;
     Cuts.Add(new Cut{A=a,B=b,T=amount});cache.Add(key,index);
    }
    return new Point{Index=index,Distance=plane};
   }
  }
  sealed class UvChannel {public int Dimension;public Vector4[] Values;}
  public static Vector3[] Subdivide(Mesh mesh,Vector3[] world,Vector3 start,Vector3 end,int segments,int vertexLimit){
   if(mesh==null||world==null)throw Bad("Automatic mesh subdivision requires a readable runtime mesh.");
   int count=mesh.vertexCount;
#if !KKS
   vertexLimit=Math.Min(vertexLimit,65535);
#endif
   if(count<1||world.Length!=count||count>vertexLimit||vertexLimit>500000)
    throw Bad("Automatic mesh subdivision has invalid vertex data or limits.");
   if(segments<1||segments>256)throw Bad("Automatic mesh subdivision has an invalid segment count.");
   Check(start);Check(end);
   double x=(double)end.x-start.x,y=(double)end.y-start.y,z=(double)end.z-start.z;
   double square=x*x+y*y+z*z;
   if(!Finite(square)||square<=1e-24)throw Bad("Automatic mesh subdivision has no usable rest line.");
   double factor=segments/square;double[] distance=new double[count];
   for(int i=0;i<count;i++){
    Check(world[i]);
    distance[i]=(((double)world[i].x-start.x)*x+((double)world[i].y-start.y)*y+((double)world[i].z-start.z)*z)*factor;
    if(!Finite(distance[i]))throw Bad("Automatic mesh subdivision has invalid projected coordinates.");
   }
   int submeshes=mesh.subMeshCount;
   if(submeshes<1||submeshes>1024)throw Bad("Automatic mesh subdivision has an invalid material count.");
   int[][] original=new int[submeshes][];bool unsupported=false,needed=false;long inputIndices=0,work=0,predicted=0;
   for(int submesh=0;submesh<submeshes;submesh++){
    MeshTopology topology=mesh.GetTopology(submesh);bool triangles=topology==MeshTopology.Triangles;
    if(!triangles)unsupported=true;
    int[] indices=mesh.GetIndices(submesh);original[submesh]=indices;inputIndices+=indices.Length;
    if(inputIndices>MaxIndices||triangles&&indices.Length%3!=0)throw Bad("Automatic mesh subdivision has too many or invalid indices.");
    for(int i=0;i<indices.Length;i++)if(indices[i]<0||indices[i]>=count)throw Bad("Automatic mesh subdivision has an invalid vertex index.");
    if(!triangles)continue;
    for(int i=0;i<indices.Length;i+=3){
     double a=distance[indices[i]],b=distance[indices[i+1]],c=distance[indices[i+2]];
     double low=Math.Min(a,Math.Min(b,c)),high=Math.Max(a,Math.Max(b,c));
     int first=FirstBand(low,segments),last=LastBand(high,segments);if(last<first)last=first;
     int crossed=last-first;
     if(high-low>1.000001&&crossed>0)needed=true;
     work+=crossed+1;predicted+=3L*(crossed*2+1);
    }
   }
   // Already dense geometry remains byte-for-byte intact, including any
   // channels that are unnecessary for generated skinning.
   if(!needed)return world;
   if(unsupported)throw Bad("Automatic subdivision requires triangle mesh topology.");
   if(work>MaxWork||predicted>MaxIndices)throw Bad("Automatic mesh subdivision would create too much geometry.");
   Planner planner=new Planner(count,vertexLimit,distance);
   int[][] planned=new int[submeshes][];long actualIndices=0;
   Point[] firstPolygon=new Point[8],secondPolygon=new Point[8],thirdPolygon=new Point[8];
   for(int submesh=0;submesh<submeshes;submesh++){
    int[] indices=original[submesh];List<int> output=new List<int>(Math.Min(MaxIndices,indices.Length));
    for(int i=0;i<indices.Length;i+=3){
     Point a=new Point{Index=indices[i],Distance=distance[indices[i]]};
     Point b=new Point{Index=indices[i+1],Distance=distance[indices[i+1]]};
     Point c=new Point{Index=indices[i+2],Distance=distance[indices[i+2]]};
     double low=Math.Min(a.Distance,Math.Min(b.Distance,c.Distance)),high=Math.Max(a.Distance,Math.Max(b.Distance,c.Distance));
     int first=FirstBand(low,segments),last=LastBand(high,segments);if(last<first)last=first;
     for(int band=first;band<=last;band++){
      firstPolygon[0]=a;firstPolygon[1]=b;firstPolygon[2]=c;int polygonCount=3;
      Point[] polygon=firstPolygon;
      if(band>0){polygonCount=Clip(planner,polygon,polygonCount,secondPolygon,band,true);polygon=secondPolygon;}
      if(band<segments-1){polygonCount=Clip(planner,polygon,polygonCount,thirdPolygon,band+1,false);polygon=thirdPolygon;}
      for(int point=1;point+1<polygonCount;point++){
       int p=polygon[0].Index,q=polygon[point].Index,r=polygon[point+1].Index;
       if(p==q||q==r||r==p)continue;
       if(actualIndices>MaxIndices-3)throw Bad("Automatic mesh subdivision would create too many triangles.");
       output.Add(p);output.Add(q);output.Add(r);actualIndices+=3;
      }
     }
    }
    planned[submesh]=output.ToArray();
   }
   if(planner.Cuts.Count==0)return world;
   int resultCount=count+planner.Cuts.Count;
   Vector3[] positions=mesh.vertices,normals=mesh.normals;Vector4[] tangents=mesh.tangents;Color[] colors=mesh.colors;
   Validate(positions.Length,count);Validate(normals.Length,count);Validate(tangents.Length,count);Validate(colors.Length,count);
   if(positions.Length!=count)throw Bad("Automatic mesh subdivision has inconsistent positions.");
   for(int i=0;i<count;i++){
    Check(positions[i]);if(normals.Length!=0)Check(normals[i]);
    if(tangents.Length!=0)Check(tangents[i]);
    if(colors.Length!=0){Color color=colors[i];Check(new Vector4(color.r,color.g,color.b,color.a));}
   }
   int channels=4;
#if KKS
   channels=8;
#endif
   UvChannel[] uv=new UvChannel[channels];
   for(int channel=0;channel<channels;channel++){
    List<Vector4> values=new List<Vector4>();mesh.GetUVs(channel,values);Validate(values.Count,count);
    if(values.Count==0)continue;
    int dimension=2;
#if KKS
    dimension=mesh.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0+channel));
    if(dimension<1||dimension>4)throw Bad("Automatic mesh subdivision has an unsupported UV layout.");
    dimension=Math.Max(2,dimension);
#endif
    for(int i=0;i<values.Count;i++){
     Vector4 value=values[i];Check(value);
     // Unity 5.6 has no public UV dimension query. Preserve all represented
     // components instead of unconditionally truncating UVs to Vector2.
     if(value.w!=0)dimension=4;else if(value.z!=0)dimension=Math.Max(3,dimension);
    }
    uv[channel]=new UvChannel{Dimension=dimension,Values=Expand(values.ToArray(),planner,resultCount,false)};
   }
   Vector3[] resultWorld=Expand(world,planner,resultCount,false);
   Vector3[] resultPositions=Expand(positions,planner,resultCount,false);
   Vector3[] resultNormals=Expand(normals,planner,resultCount,true);
   Vector4[] resultTangents=Expand(tangents,planner,resultCount,true);
   Color[] resultColors=Expand(colors,planner,resultCount);
   // Allocate even the correctly dimensioned UV lists before touching the
   // mesh. A rejected operation leaves every channel of the runtime copy intact.
   object[] uvLists=new object[channels];
   for(int channel=0;channel<channels;channel++)if(uv[channel]!=null){
    UvChannel source=uv[channel];
    if(source.Dimension==2){List<Vector2> values=new List<Vector2>(resultCount);foreach(Vector4 value in source.Values)values.Add(new Vector2(value.x,value.y));uvLists[channel]=values;}
    else if(source.Dimension==3){List<Vector3> values=new List<Vector3>(resultCount);foreach(Vector4 value in source.Values)values.Add(new Vector3(value.x,value.y,value.z));uvLists[channel]=values;}
    else uvLists[channel]=new List<Vector4>(source.Values);
   }
   // The current blend-shape pose has already been baked by AutoRig.Prepare.
   // Old delta and skin arrays cannot be retained at a different vertex count.
   mesh.ClearBlendShapes();mesh.Clear(false);
#if KKS
   if(resultCount>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
#endif
   mesh.vertices=resultPositions;
   if(resultNormals.Length!=0)mesh.normals=resultNormals;
   if(resultTangents.Length!=0)mesh.tangents=resultTangents;
   if(resultColors.Length!=0)mesh.colors=resultColors;
   for(int channel=0;channel<channels;channel++)if(uv[channel]!=null){
    if(uv[channel].Dimension==2)mesh.SetUVs(channel,(List<Vector2>)uvLists[channel]);
    else if(uv[channel].Dimension==3)mesh.SetUVs(channel,(List<Vector3>)uvLists[channel]);
    else mesh.SetUVs(channel,(List<Vector4>)uvLists[channel]);
   }
   mesh.subMeshCount=submeshes;
   for(int submesh=0;submesh<submeshes;submesh++)mesh.SetTriangles(planned[submesh],submesh,false);
   mesh.RecalculateBounds();return resultWorld;
  }
  static int FirstBand(double value,int segments){return value<=0?0:value>=segments?segments-1:(int)Math.Floor(value);}
  static int LastBand(double value,int segments){return value<=0?0:value>=segments?segments-1:(int)Math.Ceiling(value)-1;}
  static int Clip(Planner planner,Point[] input,int count,Point[] output,int plane,bool above){
   if(count==0)return 0;int result=0;Point previous=input[count-1];bool previousInside=above?previous.Distance>=plane:previous.Distance<=plane;
   for(int i=0;i<count;i++){
    Point current=input[i];bool currentInside=above?current.Distance>=plane:current.Distance<=plane;
    if(currentInside!=previousInside)Append(output,ref result,planner.Intersection(previous,current,plane));
    if(currentInside)Append(output,ref result,current);
    previous=current;previousInside=currentInside;
   }
   if(result>1&&output[0].Index==output[result-1].Index)result--;
   return result;
  }
  static void Append(Point[] output,ref int count,Point value){
   if(count>0&&output[count-1].Index==value.Index)return;
   if(count>=output.Length)throw Bad("Automatic mesh subdivision produced an invalid polygon.");
   output[count++]=value;
  }
  static Vector3[] Expand(Vector3[] source,Planner planner,int count,bool normalize){
   if(source.Length==0)return source;Vector3[] result=new Vector3[count];Array.Copy(source,result,source.Length);
   for(int i=0;i<planner.Cuts.Count;i++){
    Cut cut=planner.Cuts[i];Vector3 a=source[cut.A],b=source[cut.B];
    Vector3 value=new Vector3(Blend(a.x,b.x,cut.T),Blend(a.y,b.y,cut.T),Blend(a.z,b.z,cut.T));
    result[planner.OriginalCount+i]=normalize?value.normalized:value;
   }
   return result;
  }
  static Vector4[] Expand(Vector4[] source,Planner planner,int count,bool tangent){
   if(source.Length==0)return source;Vector4[] result=new Vector4[count];Array.Copy(source,result,source.Length);
   for(int i=0;i<planner.Cuts.Count;i++){
    Cut cut=planner.Cuts[i];Vector4 a=source[cut.A],b=source[cut.B];
    Vector4 value=new Vector4(Blend(a.x,b.x,cut.T),Blend(a.y,b.y,cut.T),Blend(a.z,b.z,cut.T),Blend(a.w,b.w,cut.T));
    if(tangent){Vector3 direction=new Vector3(value.x,value.y,value.z).normalized;value=new Vector4(direction.x,direction.y,direction.z,value.w<0?-1:1);}
    result[planner.OriginalCount+i]=value;
   }
   return result;
  }
  static Color[] Expand(Color[] source,Planner planner,int count){
   if(source.Length==0)return source;Color[] result=new Color[count];Array.Copy(source,result,source.Length);
   for(int i=0;i<planner.Cuts.Count;i++){
    Cut cut=planner.Cuts[i];Color a=source[cut.A],b=source[cut.B];
    result[planner.OriginalCount+i]=new Color(Blend(a.r,b.r,cut.T),Blend(a.g,b.g,cut.T),Blend(a.b,b.b,cut.T),Blend(a.a,b.a,cut.T));
   }
   return result;
  }
  static float Blend(float a,float b,double t){return (float)((double)a*(1-t)+(double)b*t);}
  static void Validate(int length,int count){if(length!=0&&length!=count)throw Bad("Automatic mesh subdivision has inconsistent vertex channels.");}
  static bool Finite(double value){return !Double.IsNaN(value)&&!Double.IsInfinity(value);}
  static void Check(Vector3 value){if(!Finite(value.x)||!Finite(value.y)||!Finite(value.z))throw Bad("Automatic mesh subdivision has nonfinite vertex data.");}
  static void Check(Vector4 value){if(!Finite(value.x)||!Finite(value.y)||!Finite(value.z)||!Finite(value.w))throw Bad("Automatic mesh subdivision has nonfinite surface data.");}
  static ArgumentException Bad(string message){return new ArgumentException(message);}
 }
}
