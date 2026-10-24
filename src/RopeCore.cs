using System;
using System.IO;
using System.Collections.Generic;
namespace GINRope {
 public struct V3 {
  public double X,Y,Z;
  public V3(double x,double y,double z){X=x;Y=y;Z=z;}
  public static V3 operator +(V3 a,V3 b){return new V3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
  public static V3 operator -(V3 a,V3 b){return new V3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
  public static V3 operator *(V3 a,double s){return new V3(a.X*s,a.Y*s,a.Z*s);}
  public double Length {get{return Math.Sqrt(X*X+Y*Y+Z*Z);}}
  public bool Finite {get{return !double.IsNaN(X)&&!double.IsInfinity(X)&&!double.IsNaN(Y)&&!double.IsInfinity(Y)&&!double.IsNaN(Z)&&!double.IsInfinity(Z);}}
  public static V3 Lerp(V3 a,V3 b,double t){return a*(1-t)+b*t;}
 }
 // Fit mesh points in physical units, independent of their translation and orientation.
 public sealed class LongAxis {
  int count;
  V3 mean;
  double xx,xy,xz,yy,yz,zz;
  public V3 Center {get{if(count==0)throw new ArgumentException("No mesh points.");return mean;}}
  public void Add(V3 point){
   if(!point.Finite||Math.Abs(point.X)>1e12||Math.Abs(point.Y)>1e12||Math.Abs(point.Z)>1e12)throw new ArgumentException("Invalid mesh point.");
   if(count==Int32.MaxValue)throw new ArgumentException("Too many mesh points.");
   // Welford's centered sum avoids subtracting large, nearly equal squared coordinates.
   V3 delta=point-mean;count++;mean=mean+delta*(1.0/count);V3 remaining=point-mean;
   xx+=delta.X*remaining.X;xy+=delta.X*remaining.Y;xz+=delta.X*remaining.Z;
   yy+=delta.Y*remaining.Y;yz+=delta.Y*remaining.Z;zz+=delta.Z*remaining.Z;
  }
  public V3 PrincipalDirection(){return PrincipalDirection(1e-8);}
  public V3 PrincipalDirection(double relativeTolerance){
   if(!Settings.Finite(relativeTolerance)||relativeTolerance<=0||relativeTolerance>.001)throw new ArgumentException("Invalid mesh-axis tolerance.");
   double scale=Math.Max(xx,Math.Max(yy,zz));
   if(count<2||!Settings.Finite(scale)||scale/count<=1e-20)throw new ArgumentException("Mesh points have no usable length.");
   double[,] a=new double[3,3]{{xx/scale,xy/scale,xz/scale},{xy/scale,yy/scale,yz/scale},{xz/scale,yz/scale,zz/scale}};
   double[,] basis=new double[3,3]{{1,0,0},{0,1,0},{0,0,1}};
   // A full symmetric eigensolver also finds axes orthogonal to an initial guess.
   for(int pass=0;pass<32;pass++){
    int p=0,q=1;double off=Math.Abs(a[0,1]);
    if(Math.Abs(a[0,2])>off){p=0;q=2;off=Math.Abs(a[0,2]);}
    if(Math.Abs(a[1,2])>off){p=1;q=2;off=Math.Abs(a[1,2]);}
    if(off<=1e-14)break;
    double angle=0.5*Math.Atan2(2*a[p,q],a[q,q]-a[p,p]),c=Math.Cos(angle),s=Math.Sin(angle);
    double pp=a[p,p],qq=a[q,q],pq=a[p,q];
    a[p,p]=c*c*pp-2*c*s*pq+s*s*qq;a[q,q]=s*s*pp+2*c*s*pq+c*c*qq;a[p,q]=a[q,p]=0;
    for(int k=0;k<3;k++){
     if(k!=p&&k!=q){double kp=a[k,p],kq=a[k,q];a[k,p]=a[p,k]=c*kp-s*kq;a[k,q]=a[q,k]=s*kp+c*kq;}
     double vp=basis[k,p],vq=basis[k,q];basis[k,p]=c*vp-s*vq;basis[k,q]=s*vp+c*vq;
    }
   }
   double largest=Math.Max(a[0,0],Math.Max(a[1,1],a[2,2])),tolerance=largest*relativeTolerance;
   V3 result=new V3();
   // Equal longest axes have no unique direction. Project Y, then X, then Z
   // into that eigenspace, so a cube and other symmetric meshes stay deterministic.
   int[] priority=new int[]{1,0,2};
   foreach(int axis in priority){
    result=new V3();
    for(int k=0;k<3;k++)if(largest-a[k,k]<=tolerance){double component=basis[axis,k];result=result+new V3(basis[0,k],basis[1,k],basis[2,k])*component;}
    if(result.Length>1e-6)break;
   }
   double length=result.Length;if(!result.Finite||length<=1e-6)throw new ArgumentException("Mesh axis could not be determined.");result=result*(1/length);
   double dominant=result.Y,absolute=Math.Abs(dominant),signTolerance=Math.Max(Math.Abs(result.X),Math.Max(absolute,Math.Abs(result.Z)))*relativeTolerance;
   if(Math.Abs(result.X)>absolute+signTolerance){dominant=result.X;absolute=Math.Abs(dominant);}
   if(Math.Abs(result.Z)>absolute+signTolerance)dominant=result.Z;
   return dominant<0?result*(-1):result;
  }
 }
 public sealed class Settings {
  public double Length=1, Gravity=9.81, Damping=4, Bend=0;
  public int Iterations=24;
  public bool Paused=false;
  public Settings Copy(){return (Settings)MemberwiseClone();}
  public void Validate(){
   if(!Finite(Length)||Length<0.25||Length>4||!Finite(Gravity)||Math.Abs(Gravity)>30||
      !Finite(Damping)||Damping<0||Damping>30||!Finite(Bend)||Bend<0||Bend>1||
      Iterations<4||Iterations>64)throw new FormatException("Invalid rope settings");
  }
  public static bool Finite(double v){return !double.IsNaN(v)&&!double.IsInfinity(v);}
 }
 public sealed class Solver {
  public readonly V3[] P;
  readonly V3[] previous;
  readonly double[] lengths, lambdas;
  readonly double total;
  public double AuthoredLength{get{return total;}}
  public Solver(V3[] initial){
   if(initial==null||initial.Length<3||initial.Length>128)throw new ArgumentException("A rope needs 3 to 128 chain nodes.");
   P=(V3[])initial.Clone(); previous=(V3[])initial.Clone();
   lengths=new double[P.Length-1]; lambdas=new double[lengths.Length];
   for(int i=0;i<P.Length;i++)if(!P[i].Finite)throw new ArgumentException("Non-finite node.");
   for(int i=0;i<lengths.Length;i++){lengths[i]=(P[i+1]-P[i]).Length;if(lengths[i]<0.00001)throw new ArgumentException("Coincident adjacent nodes.");total+=lengths[i];}
  }
  public void Reset(V3 a,V3 b,Settings s){
   s.Validate(); if(!a.Finite||!b.Finite)throw new ArgumentException("Non-finite anchor.");
   double span=(b-a).Length, L=total*s.Length, distance=0;
   double sag=Math.Sqrt(Math.Max(0,L*L-span*span))*0.45;
   for(int i=0;i<P.Length;i++){
    double t=distance/total;
    P[i]=V3.Lerp(a,b,t)+new V3(0,-Math.Sin(Math.PI*t)*sag,0);
    previous[i]=P[i]; if(i<lengths.Length)distance+=lengths[i];
   }
   Project(a,b,s,1.0/120);
   Array.Copy(P,previous,P.Length);
  }
  public void Step(double h,V3 a,V3 b,Settings s){
   s.Validate(); if(!Settings.Finite(h)||h<=0||h>0.05||!a.Finite||!b.Finite)throw new ArgumentException("Invalid step.");
   double retain=Math.Exp(-s.Damping*h);
   for(int i=1;i<P.Length-1;i++){
    V3 current=P[i];
    P[i]=P[i]+(P[i]-previous[i])*retain+new V3(0,-s.Gravity*h*h,0);
    previous[i]=current;
   }
   Project(a,b,s,h);
   for(int i=0;i<P.Length;i++)if(!P[i].Finite){Reset(a,b,s);return;}
  }
  // For explicit simulation/bake reconciliation only. Rendering uses a private copy.
  public void Reconcile(V3 a,V3 b,Settings s){Project(a,b,s,1.0/120);}
  void Project(V3 a,V3 b,Settings s,double h){
   Project(P,a,b,s,h,lambdas);
   previous[0]=a;previous[P.Length-1]=b;
  }
  // The render caller owns both arrays: neither positions nor velocity history
  // in this solver are touched by additional cameras or repeated render passes.
  internal void ProjectCopy(V3[] points,V3 a,V3 b,Settings s,double[] scratch){
   if(points==null||points.Length!=P.Length||Object.ReferenceEquals(points,P)||scratch==null||scratch.Length!=lambdas.Length||Object.ReferenceEquals(scratch,lambdas))throw new ArgumentException("Invalid display projection buffers.");
   s.Validate();if(!a.Finite||!b.Finite)throw new ArgumentException("Non-finite anchor.");
   Project(points,a,b,s,1.0/120,scratch);
  }
  void Project(V3[] points,V3 a,V3 b,Settings s,double h,double[] scratch){
   double scale=Math.Max(s.Length,(b-a).Length/total*1.00001);
   Array.Clear(scratch,0,scratch.Length);
   for(int pass=0;pass<s.Iterations;pass++){
    points[0]=a;points[points.Length-1]=b;
    for(int k=0;k<lengths.Length;k++){
     int i=(pass%2==0)?k:lengths.Length-1-k;
     Distance(points,i,i+1,lengths[i]*scale,0,ref scratch[i]);
    }
    if(s.Bend>0){
     double alpha=(1-s.Bend)*0.01/(h*h);
     for(int i=0;i<P.Length-2;i++){
      double lambda=0;
      Distance(points,i,i+2,(lengths[i]+lengths[i+1])*scale,alpha,ref lambda);
     }
    }
   }
   points[0]=a;points[points.Length-1]=b;
  }
  static void Distance(V3[] points,int i,int j,double rest,double alpha,ref double lambda){
   V3 delta=points[j]-points[i]; double distance=delta.Length;
   if(distance<1e-10)return;
   double wi=(i==0||i==points.Length-1)?0:1,wj=(j==0||j==points.Length-1)?0:1;
   if(wi+wj==0)return;
   double dl=(-(distance-rest)-alpha*lambda)/(wi+wj+alpha);lambda+=dl;
   V3 correction=delta*(dl/distance);
   points[i]=points[i]-correction*wi;points[j]=points[j]+correction*wj;
  }
 }
 public sealed class Record {
  public int Item,A,B;
  public string[] Paths;
  // Local position xyz, local rotation xyzw, local scale xyz, before control.
  public float[][] Poses;
  public Settings Settings=new Settings();
  public bool TimelineSync,UseBake;
  public BakeClip Bake;
  // Generated chain endpoints in the original item's local coordinate space.
  public bool AutoRig,AutoRigVertical;
  public int AutoRigIntermediate;
  public float[] AutoRigStart,AutoRigEnd;
  public string[] AutoRigSources;
 }
 public sealed class BakeClip {
  public double Start,End;
  public int Fps,Nodes;
  public float[][] Frames;
  public long Bytes{get{return Frames==null?0:(long)Frames.Length*Nodes*12;}}
  public void Validate(){
   if(!Settings.Finite(Start)||!Settings.Finite(End)||Start<0||End<=Start||(Fps!=30&&Fps!=60)||Nodes<3||Nodes>128||Frames==null||Frames.Length<2||Frames.Length>18001||Frames.Length!=(int)Math.Ceiling((End-Start)*Fps)+1||Bytes>8*1024*1024)throw new FormatException("Invalid or oversized rope bake.");
   foreach(float[] frame in Frames){if(frame==null||frame.Length!=Nodes*3)throw new FormatException("Invalid bake frame.");foreach(float v in frame)if(!Settings.Finite(v)||Math.Abs(v)>1000000)throw new FormatException("Invalid bake coordinate.");}
  }
  public void Sample(double time,V3[] result){
   if(result==null||result.Length!=Nodes||!Settings.Finite(time))throw new ArgumentException("Invalid bake sampling request.");
   time=Math.Max(Start,Math.Min(End,time));int index=Math.Min(Frames.Length-2,(int)Math.Floor((time-Start)*Fps));
   double first=Start+(double)index/Fps,last=Math.Min(End,Start+(double)(index+1)/Fps),t=(time-first)/(last-first);
   float[] a=Frames[index],b=Frames[index+1];
   for(int i=0;i<Nodes;i++){int k=i*3;result[i]=new V3(a[k]*(1-t)+b[k]*t,a[k+1]*(1-t)+b[k+1]*t,a[k+2]*(1-t)+b[k+2]*t);}
  }
 }
 public static class Codec {
  public static byte[] Encode(IList<Record> records){
   using(MemoryStream stream=new MemoryStream()){
    using(BinaryWriter w=new BinaryWriter(stream)){
     w.Write(0x47524F50);w.Write(3);w.Write(records.Count);
     foreach(Record r in records){
      r.Settings.Validate();w.Write(r.Item);w.Write(r.A);w.Write(r.B);w.Write(r.Paths.Length);
      for(int i=0;i<r.Paths.Length;i++){w.Write(r.Paths[i]);for(int j=0;j<10;j++)w.Write(r.Poses[i][j]);}
      w.Write(r.Settings.Length);w.Write(r.Settings.Gravity);w.Write(r.Settings.Damping);w.Write(r.Settings.Bend);w.Write(r.Settings.Iterations);w.Write(r.Settings.Paused);
      w.Write(r.TimelineSync);w.Write(r.UseBake);w.Write(r.Bake!=null);
      if(r.Bake!=null){r.Bake.Validate();if(r.Bake.Nodes!=r.Paths.Length)throw new FormatException("Bake node count mismatch.");w.Write(r.Bake.Start);w.Write(r.Bake.End);w.Write(r.Bake.Fps);w.Write(r.Bake.Frames.Length);foreach(float[] frame in r.Bake.Frames)foreach(float value in frame)w.Write(value);}
      w.Write(r.AutoRig);
      if(r.AutoRig){ValidateAutoRig(r,r.Paths.Length);w.Write(r.AutoRigVertical);w.Write(r.AutoRigIntermediate);for(int i=0;i<3;i++)w.Write(r.AutoRigStart[i]);for(int i=0;i<3;i++)w.Write(r.AutoRigEnd[i]);w.Write(r.AutoRigSources==null?0:r.AutoRigSources.Length);if(r.AutoRigSources!=null)foreach(string source in r.AutoRigSources)w.Write(source);}
      if(stream.Length>32*1024*1024)throw new FormatException("Rope scene data exceeds limit.");
     }
     w.Flush();return stream.ToArray();
    }
   }
  }
  public static List<Record> Decode(byte[] data){
   if(data==null||data.Length>32*1024*1024)throw new FormatException("Rope data exceeds limit.");
   List<Record> records=new List<Record>();
   using(MemoryStream stream=new MemoryStream(data))
   using(BinaryReader r=new BinaryReader(stream)){
    if(r.ReadInt32()!=0x47524F50)throw new FormatException("Unsupported rope data.");int version=r.ReadInt32();if(version!=1&&version!=2&&version!=3)throw new FormatException("Unsupported rope data.");
    int count=r.ReadInt32();if(count<0||count>256)throw new FormatException("Invalid rope count.");
    for(int c=0;c<count;c++){
     Record v=new Record();v.Item=r.ReadInt32();v.A=r.ReadInt32();v.B=r.ReadInt32();
     int n=r.ReadInt32();if(n<3||n>128)throw new FormatException("Invalid node count.");
     v.Paths=new string[n];v.Poses=new float[n][];
     for(int i=0;i<n;i++){
      v.Paths[i]=r.ReadString();if(v.Paths[i].Length>2048)throw new FormatException("Invalid path.");
      v.Poses[i]=new float[10];for(int j=0;j<10;j++){v.Poses[i][j]=r.ReadSingle();if(!Settings.Finite(v.Poses[i][j]))throw new FormatException("Invalid pose.");}
     }
     v.Settings.Length=r.ReadDouble();v.Settings.Gravity=r.ReadDouble();v.Settings.Damping=r.ReadDouble();v.Settings.Bend=r.ReadDouble();v.Settings.Iterations=r.ReadInt32();v.Settings.Paused=r.ReadBoolean();v.Settings.Validate();
     if(version>=2){
      v.TimelineSync=r.ReadBoolean();v.UseBake=r.ReadBoolean();
      if(r.ReadBoolean()){
       BakeClip clip=new BakeClip();clip.Start=r.ReadDouble();clip.End=r.ReadDouble();clip.Fps=r.ReadInt32();clip.Nodes=n;int frames=r.ReadInt32();
       long bytes=(long)frames*n*12;if(frames<2||frames>18001||bytes>8*1024*1024||bytes>stream.Length-stream.Position)throw new FormatException("Invalid bake size.");
       clip.Frames=new float[frames][];for(int f=0;f<frames;f++){clip.Frames[f]=new float[n*3];for(int k=0;k<n*3;k++)clip.Frames[f][k]=r.ReadSingle();}clip.Validate();v.Bake=clip;
      }
     }
     if(version>=3&&r.ReadBoolean()){
      v.AutoRig=true;v.AutoRigVertical=r.ReadBoolean();v.AutoRigIntermediate=r.ReadInt32();
      v.AutoRigStart=new float[3];v.AutoRigEnd=new float[3];for(int i=0;i<3;i++)v.AutoRigStart[i]=r.ReadSingle();for(int i=0;i<3;i++)v.AutoRigEnd[i]=r.ReadSingle();
      int sources=r.ReadInt32();if(sources<0||sources>64)throw new FormatException("Invalid generated rope source count.");v.AutoRigSources=new string[sources];for(int i=0;i<sources;i++)v.AutoRigSources[i]=r.ReadString();ValidateAutoRig(v,n);
     }
     records.Add(v);
    }
    if(stream.Position!=stream.Length)throw new FormatException("Trailing rope data.");
   }
   return records;
  }
  static void ValidateAutoRig(Record value,int nodes){
   if(value.AutoRigIntermediate<3||value.AutoRigIntermediate>30||nodes!=value.AutoRigIntermediate+2||value.AutoRigStart==null||value.AutoRigEnd==null||value.AutoRigStart.Length!=3||value.AutoRigEnd.Length!=3)throw new FormatException("Invalid generated rope metadata.");
   double distanceSquared=0;
   for(int i=0;i<3;i++){
    double start=value.AutoRigStart[i],end=value.AutoRigEnd[i];
    if(!Settings.Finite(start)||!Settings.Finite(end)||Math.Abs(start)>1000000||Math.Abs(end)>1000000)throw new FormatException("Invalid generated rope coordinate.");
    double delta=end-start;distanceSquared+=delta*delta;
   }
   if(distanceSquared<0.0000000001)throw new FormatException("Generated rope endpoints overlap.");
   if(value.AutoRigSources!=null){if(value.AutoRigSources.Length>64)throw new FormatException("Invalid generated rope source count.");foreach(string source in value.AutoRigSources)if(String.IsNullOrEmpty(source)||source.Length>2048)throw new FormatException("Invalid generated rope source path.");}
  }
 }
}
