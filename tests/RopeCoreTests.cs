using System;
using System.Collections.Generic;
using System.IO;
using GINRope;
class Tests {
 static int assertions;
 static void Check(bool condition,string description){assertions++;if(!condition)throw new Exception(description);}
 static Solver Make(int n,double spacing){V3[] p=new V3[n];for(int i=0;i<n;i++)p[i]=new V3(i*spacing,0,0);return new Solver(p);}
 static void Run(string name,Action test){test();Console.WriteLine("PASS "+name);}
 static Record MakeRecord(int nodes){
  Record row=new Record();row.Item=7;row.A=8;row.B=9;row.Paths=new string[nodes];row.Poses=new float[nodes][];
  for(int i=0;i<nodes;i++){row.Paths[i]="0/"+i;row.Poses[i]=new float[]{i,0,0,0,0,0,1,1,1,1};}return row;
 }
 // Fixed schema 1/2 layout, independent of the current encoder.
 static byte[] LegacyData(int version){
  using(MemoryStream stream=new MemoryStream()){
   using(BinaryWriter w=new BinaryWriter(stream)){
    w.Write(0x47524F50);w.Write(version);w.Write(1);w.Write(7);w.Write(8);w.Write(9);w.Write(3);
    for(int i=0;i<3;i++){w.Write("0/"+i);float[] pose=new float[]{i,0,0,0,0,0,1,1,1,1};foreach(float v in pose)w.Write(v);}
    w.Write(1.25);w.Write(8.0);w.Write(5.0);w.Write(0.3);w.Write(32);w.Write(true);
    if(version>=2){w.Write(true);w.Write(true);w.Write(true);w.Write(1.0);w.Write(1.05);w.Write(30);w.Write(3);for(int frame=0;frame<3;frame++)for(int k=0;k<9;k++)w.Write((float)(frame+k));}
    w.Flush();return stream.ToArray();
   }
  }
 }
 static byte[] EncodeOne(Record row){return Codec.Encode(new Record[]{row});}
 static byte[] WithSources(byte[] source,string[] paths){
  using(MemoryStream stream=new MemoryStream()){
   stream.Write(source,0,source.Length-4);
   using(BinaryWriter w=new BinaryWriter(stream)){w.Write(paths.Length);foreach(string path in paths)w.Write(path);w.Flush();return stream.ToArray();}
  }
 }
 static void RejectMetadata(Record row,string description){bool rejected=false;try{EncodeOne(row);}catch(FormatException){rejected=true;}Check(rejected,description);}
 static void RejectDecoded(byte[] data,string description){bool rejected=false;try{Codec.Decode(data);}catch(FormatException){rejected=true;}Check(rejected,description);}
 static double Dot(V3 a,V3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
 static V3 Rotate(V3 value,double x,double y,double z){
  x*=Math.PI/180;y*=Math.PI/180;z*=Math.PI/180;
  V3 first=new V3(value.X,value.Y*Math.Cos(x)-value.Z*Math.Sin(x),value.Y*Math.Sin(x)+value.Z*Math.Cos(x));
  V3 second=new V3(first.X*Math.Cos(y)+first.Z*Math.Sin(y),first.Y,-first.X*Math.Sin(y)+first.Z*Math.Cos(y));
  return new V3(second.X*Math.Cos(z)-second.Y*Math.Sin(z),second.X*Math.Sin(z)+second.Y*Math.Cos(z),second.Z);
 }
 static LongAxis BoxAxis(V3 halfSize,V3 angles,V3 translation,bool reverse){
  LongAxis fit=new LongAxis();
  for(int k=0;k<8;k++){int i=reverse?7-k:k;V3 vertex=new V3((i%2==0?-1:1)*halfSize.X,((i/2)%2==0?-1:1)*halfSize.Y,((i/4)%2==0?-1:1)*halfSize.Z);fit.Add(Rotate(vertex,angles.X,angles.Y,angles.Z)+translation);}
  return fit;
 }
 static void RejectAxis(LongAxis fit,string description){bool rejected=false;try{fit.PrincipalDirection();}catch(ArgumentException){rejected=true;}Check(rejected,description);}
 static void Main(){
  Run("Long axis follows rotated slender geometry in all directions",delegate{
   V3[] angles=new V3[]{new V3(0,0,0),new V3(90,0,0),new V3(0,0,90),new V3(90,90,0),new V3(12,25,31),new V3(25,40,55),new V3(-37,121,219)};
   foreach(V3 rotation in angles){
    V3 expected=Rotate(new V3(0,1,0),rotation.X,rotation.Y,rotation.Z);
    LongAxis fit=BoxAxis(new V3(0.1,10,0.05),rotation,new V3(0,0,0),false);V3 direction=fit.PrincipalDirection();
    Check(direction.Finite&&Math.Abs(direction.Length-1)<1e-12,"Long axis is not a finite unit vector");
    Check(Math.Abs(Dot(direction,expected))>1-1e-11,"Rotated long axis selected the thickness direction");
    Check(fit.Center.Length<1e-12,"Symmetric mesh center drifted");
    LongAxis reversed=BoxAxis(new V3(0.1,10,0.05),rotation,new V3(0,0,0),true);
    Check((direction-reversed.PrincipalDirection()).Length<1e-10,"Vertex ordering changed long-axis orientation");
   }
   LongAxis alongZ=BoxAxis(new V3(0.01,0.02,10),new V3(),new V3(),false);Check((alongZ.PrincipalDirection()-new V3(0,0,1)).Length<1e-12,"Pure Z axis was missed");
   LongAxis alongX=BoxAxis(new V3(10,0.02,0.01),new V3(),new V3(),false);Check((alongX.PrincipalDirection()-new V3(1,0,0)).Length<1e-12,"Pure X axis was missed");
  });
  Run("Long axis retains physical scale and tolerates large translations",delegate{
   V3 angles=new V3(63,-17,28),offset=new V3(999990,-888880,777770),expected=Rotate(new V3(0,1,0),angles.X,angles.Y,angles.Z);
   // A scaled cylinder-like cloud: scale must affect the fit before rotation.
   LongAxis fit=BoxAxis(new V3(0.1,10,0.1),angles,offset,false);
   Check(Math.Abs(Dot(fit.PrincipalDirection(),expected))>1-1e-10,"Translated nonuniformly scaled geometry lost its long axis");Check((fit.Center-offset).Length<1e-8,"Translation corrupted mesh center");
   LongAxis wide=BoxAxis(new V3(20,1,0.1),angles,offset,false);V3 wideExpected=Rotate(new V3(1,0,0),angles.X,angles.Y,angles.Z);
   Check(Math.Abs(Dot(wide.PrincipalDirection(),wideExpected))>1-1e-10,"Physical scale was ignored when choosing the long axis");
   LongAxis far=new LongAxis();far.Add(new V3(1e11,-1e11,1e11));far.Add(new V3(1e11,-1e11,1e11+10));
   Check((far.PrincipalDirection()-new V3(0,0,1)).Length<1e-12,"Large coordinate cancellation lost a valid axis");Check(Math.Abs(far.Center.Z-(1e11+5))<1e-5,"Large translated center is incorrect");
   LongAxis extremes=new LongAxis();extremes.Add(new V3(-1e12,0,0));extremes.Add(new V3(1e12,0,0));Check((extremes.PrincipalDirection()-new V3(1,0,0)).Length<1e-12,"Safe maximum coordinates overflowed");
  });
  Run("Equal long axes have a deterministic geometric fallback",delegate{
   LongAxis cube=BoxAxis(new V3(1,1,1),new V3(),new V3(),false);Check((cube.PrincipalDirection()-new V3(0,1,0)).Length<1e-12,"Cube did not prefer Y");
   LongAxis plane=BoxAxis(new V3(1,0,1),new V3(),new V3(),false);Check((plane.PrincipalDirection()-new V3(1,0,0)).Length<1e-12,"XZ plane did not prefer X");
   double h=Math.Sqrt(0.5);LongAxis tilted=new LongAxis();tilted.Add(new V3(h,h,0));tilted.Add(new V3(-h,-h,0));tilted.Add(new V3(0,0,1));tilted.Add(new V3(0,0,-1));
   Check((tilted.PrincipalDirection()-new V3(h,h,0)).Length<1e-10,"Tied tilted eigenspace did not use projected Y");
   LongAxis near=BoxAxis(new V3(Math.Sqrt(1+5e-9),1,0.1),new V3(),new V3(),false);Check((near.PrincipalDirection()-new V3(0,1,0)).Length<1e-12,"Nearly equal axes did not stay deterministic");
   LongAxis distinct=BoxAxis(new V3(Math.Sqrt(1+1e-5),1,0.1),new V3(),new V3(),false);Check((distinct.PrincipalDirection()-new V3(1,0,0)).Length<1e-12,"Distinct longer axis was treated as a tie");
   LongAxis sign=new LongAxis();sign.Add(new V3(-1,1,0));sign.Add(new V3(1,-1,0));Check((sign.PrincipalDirection()-new V3(-h,h,0)).Length<1e-10,"Dominant-component sign tie did not prefer positive Y");
  });
  Run("Float mesh precision can use a stable symmetry tolerance",delegate{
   V3[] rotations=new V3[]{new V3(27,45,17),new V3(90,0,0),new V3(-17,63,29)};
   foreach(V3 angles in rotations){
    // Round both the rotation matrix and the translated world vertices to
    // single precision, then return to the item frame as Unity does.
    float[,] matrix=new float[3,3];V3[] columns=new V3[]{Rotate(new V3(1,0,0),angles.X,angles.Y,angles.Z),Rotate(new V3(0,1,0),angles.X,angles.Y,angles.Z),Rotate(new V3(0,0,1),angles.X,angles.Y,angles.Z)};
    for(int k=0;k<3;k++){matrix[0,k]=(float)columns[k].X;matrix[1,k]=(float)columns[k].Y;matrix[2,k]=(float)columns[k].Z;}
    LongAxis fit=new LongAxis();
    for(int k=0;k<8;k++){
     float x=(k%2==0?-1:1)*.05f,y=((k/2)%2==0?-1:1)*.05f,z=((k/4)%2==0?-1:1)*.05f;
     float worldX=matrix[0,0]*x+matrix[0,1]*y+matrix[0,2]*z+2,worldY=matrix[1,0]*x+matrix[1,1]*y+matrix[1,2]*z+2,worldZ=matrix[2,0]*x+matrix[2,1]*y+matrix[2,2]*z+2;
     float dx=worldX-2,dy=worldY-2,dz=worldZ-2;
     fit.Add(new V3(matrix[0,0]*dx+matrix[1,0]*dy+matrix[2,0]*dz,matrix[0,1]*dx+matrix[1,1]*dy+matrix[2,1]*dz,matrix[0,2]*dx+matrix[1,2]*dy+matrix[2,2]*dz));
    }
    Check((fit.PrincipalDirection(1e-5)-new V3(0,1,0)).Length<1e-10,"Float rotation round-trip destabilized symmetric mesh direction");
   }
   LongAxis slight=BoxAxis(new V3(Math.Sqrt(1+1e-6),1,.1),new V3(),new V3(),false);
   Check((slight.PrincipalDirection()-new V3(1,0,0)).Length<1e-12,"Default precision changed");
   Check((slight.PrincipalDirection(1e-5)-new V3(0,1,0)).Length<1e-12,"Explicit float tolerance did not stabilize nearly equal axes");
   Check((slight.PrincipalDirection(.001)-new V3(0,1,0)).Length<1e-12,"Maximum valid tolerance was rejected");
   LongAxis diagonal=new LongAxis();diagonal.Add(new V3(-1-1e-6,1,0));diagonal.Add(new V3(1+1e-6,-1,0));
   V3 stableSign=diagonal.PrincipalDirection(1e-5);Check(stableSign.Y>0&&stableSign.X<0,"Float noise flipped the sign of nearly equal opposite components");
   V3 preciseSign=diagonal.PrincipalDirection();Check(preciseSign.X>0&&preciseSign.Y<0,"Default sign precision no longer distinguishes separate components");
   LongAxis distinctSign=new LongAxis();distinctSign.Add(new V3(-1-.001,1,0));distinctSign.Add(new V3(1+.001,-1,0));
   V3 longerComponent=distinctSign.PrincipalDirection(1e-5);Check(longerComponent.X>0&&longerComponent.Y<0,"Clearly dominant component did not determine a positive sign");
   double[] invalid=new double[]{0,-1,Double.NaN,Double.PositiveInfinity,Double.NegativeInfinity,.0010001};
   foreach(double tolerance in invalid){bool rejected=false;try{slight.PrincipalDirection(tolerance);}catch(ArgumentException){rejected=true;}Check(rejected,"Unsafe axis tolerance accepted");}
  });
  Run("Unusable mesh points fail safely without poisoning the fit",delegate{
   LongAxis fit=new LongAxis();RejectAxis(fit,"Empty mesh accepted");bool rejected=false;try{V3 unused=fit.Center;}catch(ArgumentException){rejected=true;}Check(rejected,"Empty mesh has a center");
   fit.Add(new V3(3,4,5));RejectAxis(fit,"Single mesh point accepted");fit.Add(new V3(3,4,5));RejectAxis(fit,"Coincident mesh points accepted");
   V3[] invalid=new V3[]{new V3(Double.NaN,0,0),new V3(0,Double.PositiveInfinity,0),new V3(0,0,Double.NegativeInfinity),new V3(1e12+1,0,0),new V3(0,-1e12-1,0),new V3(Double.MaxValue,0,0)};
   foreach(V3 point in invalid){rejected=false;try{fit.Add(point);}catch(ArgumentException){rejected=true;}Check(rejected,"Unsafe mesh point accepted");Check((fit.Center-new V3(3,4,5)).Length==0,"Rejected point changed accumulated geometry");}
   fit.Add(new V3(3,8,5));Check((fit.PrincipalDirection()-new V3(0,1,0)).Length<1e-12,"Rejected points poisoned subsequent valid geometry");
   LongAxis tiny=new LongAxis();tiny.Add(new V3(0,0,0));tiny.Add(new V3(1e-12,0,0));RejectAxis(tiny,"Numerically unusable tiny mesh accepted");
   LongAxis pair=new LongAxis();pair.Add(new V3(0,0,0));pair.Add(new V3(3,4,0));Check((pair.PrincipalDirection()-new V3(0.6,0.8,0)).Length<1e-12,"Two distinct mesh points did not produce their line");
  });
  Run("Pinned ends and sag",delegate{
   Solver rope=Make(17,0.125);Settings s=new Settings();V3 a=new V3(0,0,0),b=new V3(1,0,0);rope.Reset(a,b,s);
   for(int k=0;k<2400;k++)rope.Step(1.0/120,a,b,s);
   Check((rope.P[0]-a).Length<1e-10,"A drifted");Check((rope.P[16]-b).Length<1e-10,"B drifted");Check(rope.P[8].Y<-0.2,"No sag");
   for(int i=0;i<16;i++)Check(Math.Abs((rope.P[i+1]-rope.P[i]).Length-0.125)<0.015,"Rest length drift");
  });
  Run("Moving anchors and unreachable span stay finite",delegate{
   Solver rope=Make(25,0.1);Settings s=new Settings();s.Iterations=48;rope.Reset(new V3(0,0,0),new V3(1,0,0),s);
   for(int k=0;k<4000;k++){
    V3 a=new V3(Math.Sin(k*0.01),Math.Cos(k*0.007),0),b=new V3(a.X+5,a.Y+Math.Sin(k*0.02),0);
    rope.Step(1.0/120,a,b,s);foreach(V3 v in rope.P)Check(v.Finite&&v.Length<100,"Simulation diverged");
    Check((rope.P[24]-b).Length<1e-10,"Moving B drifted");
   }
  });
  Run("Coincident endpoints and extreme damping",delegate{
   Solver rope=Make(12,0.1);Settings s=new Settings();s.Damping=20;s.Bend=0.4;
   rope.Reset(new V3(0,0,0),new V3(0,0,0),s);
   for(int k=0;k<1000;k++)rope.Step(1.0/120,new V3(0,0,0),new V3(0,0,0),s);
   foreach(V3 v in rope.P)Check(v.Finite&&v.Length<10,"Folded rope diverged");
  });
  Run("Reconcile does not advance gravity",delegate{
   Solver rope=Make(3,0.5);Settings s=new Settings();V3 a=new V3(0,0,0),b=new V3(1,0,0);rope.Reset(a,b,s);
   for(int i=0;i<100;i++)rope.Reconcile(a,b,s);
   Check(Math.Abs(rope.P[1].Y)<1e-10,"Camera pass advanced time");
  });
  Run("Persistence round-trip and corrupt data rejection",delegate{
   Record v=new Record();v.Item=7;v.A=8;v.B=9;v.Paths=new string[]{"0","0/0","0/0/0"};v.Poses=new float[3][];
   for(int i=0;i<3;i++)v.Poses[i]=new float[]{i,0,0,0,0,0,1,1,1,1};
   v.Settings.Bend=0.45;List<Record> rows=new List<Record>();rows.Add(v);byte[] data=Codec.Encode(rows);List<Record> read=Codec.Decode(data);
   Check(read.Count==1&&read[0].Item==7&&read[0].B==9,"IDs lost");Check(read[0].Paths[2]=="0/0/0"&&read[0].Poses[2][0]==2,"Pose lost");
   Check(Math.Abs(read[0].Settings.Bend-0.45)<1e-10,"Settings lost");
   data[0]=0;bool rejected=false;try{Codec.Decode(data);}catch(FormatException){rejected=true;}Check(rejected,"Corrupt data accepted");
   rejected=false;try{Codec.Decode(new byte[0]);}catch(EndOfStreamException){rejected=true;}Check(rejected,"Truncated data accepted");
  });
  Run("Baked sampling is independent of seek order",delegate{
   BakeClip clip=new BakeClip();clip.Start=1;clip.End=1.05;clip.Fps=30;clip.Nodes=3;clip.Frames=new float[3][];
   for(int f=0;f<3;f++){clip.Frames[f]=new float[9];for(int n=0;n<3;n++){clip.Frames[f][n*3]=n;clip.Frames[f][n*3+1]=f;}}
   clip.Validate();V3[] p=new V3[3];clip.Sample(1.02,p);double expected=p[1].Y;
   clip.Sample(999,p);Check(p[1].Y==2,"Bake end is not clamped");clip.Sample(-1,p);Check(p[1].Y==0,"Bake start is not clamped");clip.Sample(1.02,p);Check(Math.Abs(p[1].Y-expected)<1e-12,"Seek order changed bake");
   Record row=new Record();row.Paths=new string[]{"0","0/0","0/0/0"};row.Poses=new float[3][];for(int i=0;i<3;i++)row.Poses[i]=new float[]{i,0,0,0,0,0,1,1,1,1};row.Bake=clip;row.UseBake=true;row.TimelineSync=true;
   List<Record> source=new List<Record>();source.Add(row);Record read=Codec.Decode(Codec.Encode(source))[0];Check(read.UseBake&&read.TimelineSync&&read.Bake.Frames.Length==3,"Baked mode lost on save");read.Bake.Sample(1.02,p);Check(Math.Abs(p[1].Y-expected)<1e-12,"Baked motion changed on save");
   clip.Frames[1][2]=Single.NaN;bool rejected=false;try{Codec.Encode(source);}catch(FormatException){rejected=true;}Check(rejected,"Invalid bake data accepted");
  });
  Run("Legacy scene schemas retain their original behavior",delegate{
   for(int version=1;version<=2;version++){
    Record row=Codec.Decode(LegacyData(version))[0];Check(row.Item==7&&row.A==8&&row.B==9,"Legacy item IDs lost");Check(row.Paths.Length==3&&row.Poses[2][0]==2,"Legacy poses lost");
    Check(row.Settings.Length==1.25&&row.Settings.Gravity==8&&row.Settings.Damping==5&&row.Settings.Bend==0.3&&row.Settings.Iterations==32&&row.Settings.Paused,"Legacy settings lost");
    Check(!row.AutoRig&&row.AutoRigStart==null&&row.AutoRigEnd==null,"Legacy item gained a generated rig");
    if(version==1)Check(!row.TimelineSync&&!row.UseBake&&row.Bake==null,"Schema 1 animation defaults changed");
    else Check(row.TimelineSync&&row.UseBake&&row.Bake.Nodes==3&&row.Bake.Frames[2][8]==10,"Schema 2 bake lost");
    Record upgraded=Codec.Decode(EncodeOne(row))[0];Check(!upgraded.AutoRig&&upgraded.Paths[2]==row.Paths[2],"Legacy scene cannot be saved as schema 3");
   }
  });
  Run("Generated rig metadata round-trips all counts and both axes",delegate{
   for(int intermediate=3;intermediate<=30;intermediate++)for(int direction=0;direction<2;direction++){
    Record row=MakeRecord(intermediate+2);row.AutoRig=true;row.AutoRigVertical=direction==1;row.AutoRigIntermediate=intermediate;
    // Coordinates need not align with local X/Y when the item was rotated.
    row.AutoRigStart=new float[]{0.125f,4.5f,-2};row.AutoRigEnd=new float[]{-1.5f,-0.25f,3.75f};
    row.AutoRigSources=new string[]{"0/1:0","0/2:1"};
    Record read=Codec.Decode(EncodeOne(row))[0];Check(read.AutoRig&&read.AutoRigVertical==row.AutoRigVertical&&read.AutoRigIntermediate==intermediate,"Generated rig mode or count lost");
    Check(read.Paths.Length==intermediate+2&&read.Poses.Length==intermediate+2,"Generated rig node count changed");
    for(int i=0;i<3;i++)Check(read.AutoRigStart[i]==row.AutoRigStart[i]&&read.AutoRigEnd[i]==row.AutoRigEnd[i],"Generated rig local endpoints changed");
    Check(read.AutoRigSources.Length==2&&read.AutoRigSources[0]=="0/1:0"&&read.AutoRigSources[1]=="0/2:1","Generated rig source renderer paths changed");
   }
   Record ordinary=MakeRecord(3);Record plain=Codec.Decode(EncodeOne(ordinary))[0];Check(!plain.AutoRig&&plain.AutoRigStart==null&&plain.AutoRigEnd==null,"Ordinary rope became generated");
  });
  Run("Generated rig metadata rejects unsafe or inconsistent data",delegate{
   Record row=MakeRecord(5);row.AutoRig=true;row.AutoRigVertical=true;row.AutoRigIntermediate=3;row.AutoRigStart=new float[]{0,1,0};row.AutoRigEnd=new float[]{0,-1,0};
   byte[] valid=EncodeOne(row);
   row.AutoRigIntermediate=2;RejectMetadata(row,"Too few generated intermediate nodes accepted");row.AutoRigIntermediate=31;RejectMetadata(row,"Too many generated intermediate nodes accepted");row.AutoRigIntermediate=4;RejectMetadata(row,"Generated node count mismatch accepted");row.AutoRigIntermediate=3;
   row.AutoRigStart=null;RejectMetadata(row,"Missing generated start accepted");row.AutoRigStart=new float[2];RejectMetadata(row,"Short generated start accepted");row.AutoRigStart=new float[]{0,1,0};
   row.AutoRigEnd=new float[4];RejectMetadata(row,"Long generated end accepted");row.AutoRigEnd=new float[]{0,-1,0};
   float[] invalid=new float[]{Single.NaN,Single.PositiveInfinity,Single.NegativeInfinity,1000001,-1000001};
   foreach(float coordinate in invalid){row.AutoRigStart[0]=coordinate;RejectMetadata(row,"Invalid generated start coordinate accepted");row.AutoRigStart[0]=0;row.AutoRigEnd[2]=coordinate;RejectMetadata(row,"Invalid generated end coordinate accepted");row.AutoRigEnd[2]=0;}
   row.AutoRigEnd=new float[]{0,1,0};RejectMetadata(row,"Overlapping generated endpoints accepted");row.AutoRigEnd=new float[]{0,1.000001f,0};RejectMetadata(row,"Unsafe tiny generated span accepted");
   // Metadata with zero sources occupies 34 bytes: enabled, axis, count, start xyz, end xyz, source count.
   byte[] broken=(byte[])valid.Clone();Buffer.BlockCopy(BitConverter.GetBytes(31),0,broken,broken.Length-32,4);RejectDecoded(broken,"Decoder accepted generated count outside limits");
   broken=(byte[])valid.Clone();Buffer.BlockCopy(BitConverter.GetBytes(4),0,broken,broken.Length-32,4);RejectDecoded(broken,"Decoder accepted generated node count mismatch");
   broken=(byte[])valid.Clone();Buffer.BlockCopy(BitConverter.GetBytes(Single.NaN),0,broken,broken.Length-28,4);RejectDecoded(broken,"Decoder accepted non-finite generated coordinate");
   broken=(byte[])valid.Clone();Buffer.BlockCopy(BitConverter.GetBytes(1000001f),0,broken,broken.Length-8,4);RejectDecoded(broken,"Decoder accepted oversized generated coordinate");
   broken=(byte[])valid.Clone();Buffer.BlockCopy(broken,broken.Length-28,broken,broken.Length-16,12);RejectDecoded(broken,"Decoder accepted coincident generated endpoints");
   Record noSources=Codec.Decode(valid)[0];Check(noSources.AutoRigSources.Length==0,"Null generated sources do not round-trip as zero sources");
   row.AutoRigEnd=new float[]{0,-1,0};row.AutoRigSources=new string[64];for(int i=0;i<64;i++)row.AutoRigSources[i]="0/"+i+":0";
   Check(Codec.Decode(EncodeOne(row))[0].AutoRigSources.Length==64,"Maximum generated source count rejected");
   row.AutoRigSources=new string[65];RejectMetadata(row,"Too many generated source paths accepted");
   row.AutoRigSources=new string[]{""};RejectMetadata(row,"Empty generated source path accepted");row.AutoRigSources=new string[]{null};RejectMetadata(row,"Null generated source path accepted");row.AutoRigSources=new string[]{new String('a',2049)};RejectMetadata(row,"Oversized generated source path accepted");
   row.AutoRigSources=new string[]{new String('a',2048)};Check(Codec.Decode(EncodeOne(row))[0].AutoRigSources[0].Length==2048,"Maximum generated source path rejected");
   broken=(byte[])valid.Clone();Buffer.BlockCopy(BitConverter.GetBytes(65),0,broken,broken.Length-4,4);RejectDecoded(broken,"Decoder accepted too many generated source paths");
   broken=(byte[])valid.Clone();Buffer.BlockCopy(BitConverter.GetBytes(-1),0,broken,broken.Length-4,4);RejectDecoded(broken,"Decoder accepted negative generated source count");
   RejectDecoded(WithSources(valid,new string[]{""}),"Decoder accepted empty generated source path");RejectDecoded(WithSources(valid,new string[]{new String('a',2049)}),"Decoder accepted oversized generated source path");
   byte[] truncated=new byte[valid.Length-1];Array.Copy(valid,truncated,truncated.Length);bool rejected=false;try{Codec.Decode(truncated);}catch(EndOfStreamException){rejected=true;}Check(rejected,"Decoder accepted truncated generated metadata");
  });
  Console.WriteLine("ASSERTIONS "+assertions);
 }
}
