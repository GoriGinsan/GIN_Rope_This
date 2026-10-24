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
 static void Main(){
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
