using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Studio;
using UnityEngine;

namespace GINRope {
 public sealed class TimelineBridge {
  const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  Type type;FieldInfo self;PropertyInfo time,duration,playing;MethodInfo seek,pause,play,interpolate;
  public bool Ready{get{return type!=null&&self.GetValue(null)!=null;}}
  public string Version{get{return type==null?"unavailable":type.Assembly.GetName().Version.ToString();}}
  public TimelineBridge(){
   foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()){Type found=assembly.GetType("Timeline.Timeline");if(found!=null){type=found;break;}}
   if(type==null)return;
   self=type.GetField("_self",Flags);time=type.GetProperty("playbackTime",Flags);duration=type.GetProperty("duration",Flags);playing=type.GetProperty("isPlaying",Flags);
   seek=type.GetMethod("Seek",Flags,null,new Type[]{typeof(float)},null);pause=type.GetMethod("Pause",Flags);play=type.GetMethod("Play",Flags);interpolate=type.GetMethod("Interpolate",Flags,null,new Type[]{typeof(bool)},null);
   if(seek==null)seek=type.GetMethod("SeekPlaybackTime",Flags,null,new Type[]{typeof(float)},null);
   if(self==null||time==null||duration==null||playing==null||seek==null||pause==null||play==null||interpolate==null)type=null;
  }
  public double Time{get{return Convert.ToDouble(time.GetValue(null,null));}}
  public double Duration{get{return Convert.ToDouble(duration.GetValue(null,null));}}
  public bool Playing{get{return (bool)playing.GetValue(null,null);}}
  public void SetPlaying(bool value){if(Playing!=value)(value?play:pause).Invoke(null,null);}
  public void Evaluate(double value){
   if(!Ready||!Settings.Finite(value)||value<0||value>Duration+0.001)throw new InvalidOperationException("Timeline time is unavailable or outside its duration.");
   object instance=self.GetValue(null);seek.Invoke(seek.IsStatic?null:instance,new object[]{(float)value});
   // Seek may return early at the current time. Always evaluate both passes.
   interpolate.Invoke(instance,new object[]{true});interpolate.Invoke(instance,new object[]{false});
   foreach(ObjectCtrlInfo info in Singleton<Studio.Studio>.Instance.dicObjectCtrl.Values)if(info.guideObject!=null)info.guideObject.ForceUpdate();
   foreach(OCIChar character in Characters())foreach(Animator animator in character.guideObject.transformTarget.GetComponentsInChildren<Animator>(true))if(animator.isActiveAndEnabled)animator.Update(0);
  }
  static IEnumerable<OCIChar> Characters(){foreach(ObjectCtrlInfo info in Singleton<Studio.Studio>.Instance.dicObjectCtrl.Values){OCIChar character=info as OCIChar;if(character!=null)yield return character;}}
 }

 public sealed class ConstraintBridge {
  const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  Type type;object instance;MethodInfo add,remove,apply;FieldInfo list;
  public bool Ready{get{return instance!=null;}}
  public ConstraintBridge(){
   foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()){type=assembly.GetType("NodesConstraints.NodesConstraints");if(type!=null)break;}
   if(type==null)return;FieldInfo self=type.GetField("_self",Flags);instance=self==null?null:self.GetValue(null);
   add=type.GetMethod("AddConstraint",Flags,null,new Type[]{typeof(bool),typeof(Transform),typeof(Transform),typeof(bool),typeof(Vector3),typeof(bool),typeof(Quaternion),typeof(bool),typeof(Vector3),typeof(string)},null);
   remove=type.GetMethod("RemoveConstraintAt",Flags);apply=type.GetMethod("ApplyConstraints",Flags);list=type.GetField("_constraints",Flags);
   if(add==null||remove==null||apply==null||list==null)instance=null;
  }
  static object Field(object row,string name){return row.GetType().GetField(name,Flags).GetValue(row);}
  IList Rows{get{return (IList)list.GetValue(instance);}}
  static bool Owned(object row){string alias=Field(row,"alias") as string;return alias!=null&&alias.StartsWith("GIN Rope This:",StringComparison.Ordinal);}
  public void Apply(){if(Ready)apply.Invoke(instance,null);}
  public string TargetName(Rope rope,bool north){
   if(!Ready)return "free";Transform child=(north?rope.A:rope.B).guideObject.transformTarget;
   foreach(object row in Rows)if((Transform)Field(row,"childTransform")==child&&(bool)Field(row,"enabled")&&(bool)Field(row,"position")){Transform target=(Transform)Field(row,"parentTransform");return Plugin.SceneTargetLabel(target);}
   return "free";
  }
  public void Connect(Rope rope,bool north,Transform target,bool keepPosition){
   if(!Ready)throw new InvalidOperationException("NodesConstraints is unavailable.");
   if(!rope.Valid||target==null)throw new ArgumentException("Choose an existing target.");
   Transform child=(north?rope.A:rope.B).guideObject.transformTarget;
   Plugin.CheckVector(target.position,"Target");Plugin.CheckScale(target.lossyScale);
   if(target.IsChildOf(rope.Item.objectItem.transform))throw new ArgumentException("A rope cannot follow its own item, bones or handles.");
   foreach(object row in Rows)if((Transform)Field(row,"childTransform")==child&&(bool)Field(row,"enabled")&&(bool)Field(row,"position")&&!Owned(row))throw new InvalidOperationException("This handle already has a position constraint. Edit that connection in NodesConstraints first.");
   CheckCycle(child,target);
   List<object> prior=new List<object>();foreach(object row in Rows)if((Transform)Field(row,"childTransform")==child&&Owned(row))prior.Add(row);
   Vector3 offset=keepPosition?target.InverseTransformPoint(child.position):Vector3.zero;
   foreach(object row in prior)if((Transform)Field(row,"parentTransform")==target){
    row.GetType().GetField("positionOffset",Flags).SetValue(row,offset);row.GetType().GetField("enabled",Flags).SetValue(row,true);row.GetType().GetField("position",Flags).SetValue(row,true);
    Apply();rope.NeedsReset=true;return;
   }
   object added=add.Invoke(instance,new object[]{true,target,child,true,offset,false,Quaternion.identity,false,Vector3.one,north?"GIN Rope This: A":"GIN Rope This: B"});
   if(added==null)throw new InvalidOperationException("NodesConstraints could not create this connection.");
   foreach(object row in prior)if(!object.ReferenceEquals(row,added)){int at=Rows.IndexOf(row);if(at>=0)remove.Invoke(instance,new object[]{at});}
   Apply();rope.NeedsReset=true;
  }
  public void Disconnect(Rope rope,bool north){
   if(!Ready)return;OCIFolder handle=north?rope.A:rope.B;Transform child=handle.guideObject.transformTarget;Vector3 position=child.position;
   for(int i=Rows.Count-1;i>=0;i--)if((Transform)Field(Rows[i],"childTransform")==child&&Owned(Rows[i]))remove.Invoke(instance,new object[]{i});
   Transform parent=child.parent;handle.guideObject.changeAmount.pos=parent==null?position:parent.InverseTransformPoint(position);handle.guideObject.ForceUpdate();rope.NeedsReset=true;
  }
  public void ValidateBakeInputs(Rope rope){
   List<Transform> pending=new List<Transform>();HashSet<Transform> seen=new HashSet<Transform>();pending.Add(rope.A.guideObject.transformTarget);pending.Add(rope.B.guideObject.transformTarget);
   for(int at=0;at<pending.Count;at++){
    Transform input=pending[at];if(input==null||!seen.Add(input))continue;
    foreach(Rope other in Plugin.Instance.Ropes)if(other!=rope&&other.Valid&&(!other.UseBake||other.Bake==null))foreach(Transform bone in other.Bones)if(input.IsChildOf(bone))throw new InvalidOperationException("An endpoint follows another live rope. Bake that rope first.");
    if(!Ready)continue;
    foreach(object row in Rows){
     Transform child=(Transform)Field(row,"childTransform"),parent=(Transform)Field(row,"parentTransform");
     if(!(bool)Field(row,"enabled")||child==null||parent==null||!input.IsChildOf(child))continue;
     if(Convert.ToSingle(Field(row,"positionDamp"))>0||Convert.ToSingle(Field(row,"rotationDamp"))>0||Convert.ToSingle(Field(row,"scaleDamp"))>0)throw new InvalidOperationException("Bake requires undamped endpoint constraints. Set their damping to zero first.");
     pending.Add(parent);if(pending.Count>40000)throw new InvalidOperationException("Too many endpoint dependencies to bake safely.");
    }
   }
  }
  void CheckCycle(Transform child,Transform target){
   List<Transform[]> edges=new List<Transform[]>();
   foreach(object row in Rows)if((bool)Field(row,"enabled")){Transform p=(Transform)Field(row,"parentTransform"),c=(Transform)Field(row,"childTransform");if(p!=null&&c!=null)edges.Add(new Transform[]{p,c});}
   foreach(Rope rope in Plugin.Instance.Ropes)if(rope.Valid)foreach(Transform bone in rope.Bones){edges.Add(new Transform[]{rope.A.guideObject.transformTarget,bone});edges.Add(new Transform[]{rope.B.guideObject.transformTarget,bone});}
   if(edges.Count>40000)throw new InvalidOperationException("Too many dependencies to verify this connection safely.");
   Queue<Transform> pending=new Queue<Transform>();HashSet<Transform> seen=new HashSet<Transform>();pending.Enqueue(child);seen.Add(child);
   while(pending.Count>0){Transform node=pending.Dequeue();if(target.IsChildOf(node))throw new ArgumentException("This connection would create a dependency cycle.");foreach(Transform[] edge in edges)if(edge[0].IsChildOf(node)&&seen.Add(edge[1]))pending.Enqueue(edge[1]);}
  }
 }

 public sealed class BakeTask {
  public readonly Rope Rope;
  readonly Plugin plugin;readonly TimelineBridge timeline;readonly ConstraintBridge constraints;
  readonly double returnTime;readonly bool returnPlaying,oldPaused,oldSync,oldUseBake;
  readonly BakeClip clip;int warmup=120,frame;double simulated;
  public bool Finished{get;private set;}
  public float Progress{get{return (float)frame/clip.Frames.Length;}}
  public BakeTask(Plugin owner,Rope rope,double start,double end,int fps){
   plugin=owner;Rope=rope;timeline=owner.Timeline;constraints=owner.Constraints;
   if(!timeline.Ready||!rope.Valid)throw new InvalidOperationException("Select a rope and open Timeline first.");
   constraints.ValidateBakeInputs(rope);
   if(!Settings.Finite(start)||!Settings.Finite(end)||start<0||end<=start||end>timeline.Duration+0.001||(fps!=30&&fps!=60))throw new ArgumentException("Choose a valid Timeline range and 30 or 60 fps.");
   double count=Math.Ceiling((end-start)*fps)+1;if(count>18001||count*rope.Bones.Length*12>8*1024*1024)throw new ArgumentException("Bake range is too large. Shorten the range or use 30 fps.");
   long other=0;foreach(Rope existing in owner.Ropes)if(existing!=rope&&existing.Bake!=null)other+=existing.Bake.Bytes;
   if(other+(long)count*rope.Bones.Length*12>24*1024*1024)throw new ArgumentException("Scene bake storage is full. Clear unused bakes first.");
   clip=new BakeClip{Start=start,End=end,Fps=fps,Nodes=rope.Bones.Length,Frames=new float[(int)count][]};
   returnTime=timeline.Time;returnPlaying=timeline.Playing;oldPaused=rope.Settings.Paused;oldSync=rope.TimelineSync;oldUseBake=rope.UseBake;
   timeline.SetPlaying(false);rope.Settings.Paused=false;rope.UseBake=false;rope.NeedsReset=true;simulated=start;
  }
  public void Step(){
   if(Finished)return;
   if(!Rope.Valid||!timeline.Ready)throw new InvalidOperationException("The rope or Timeline was removed during baking.");
   if(timeline.Playing)throw new InvalidOperationException("Playback started during baking. Bake cancelled.");
   if(warmup>0){Evaluate(clip.Start);for(int i=0;i<8&&warmup>0;i++,warmup--)Rope.BakeStep(1.0/120);return;}
   for(int batch=0;batch<4&&frame<clip.Frames.Length;batch++){
    double target=Math.Min(clip.End,clip.Start+(double)frame/clip.Fps);
    if(frame==0){Evaluate(target);Rope.BakeStep(0);}
    else while(simulated<target-0.000000001){double next=Math.Min(target,simulated+1.0/120);Evaluate(next);Rope.BakeStep(next-simulated);simulated=next;}
    clip.Frames[frame++]=Rope.CaptureBakeFrame();
   }
   if(frame==clip.Frames.Length){clip.Validate();Rope.Bake=clip;Rope.UseBake=true;Rope.TimelineSync=true;Finish(true,true);plugin.Message("Bake complete. Timeline now plays the saved rope motion. Rebake after editing targets or physics.");}
  }
  public void Cancel(bool restoreClock){Finish(false,restoreClock);}
  void Evaluate(double time){timeline.Evaluate(time);foreach(Rope other in plugin.Ropes)if(other!=Rope&&other.Valid&&other.UseBake&&other.Bake!=null)other.PlayBake(time);constraints.Apply();}
  void Finish(bool commit,bool restoreClock){
   if(Finished)return;Finished=true;Rope.Settings.Paused=oldPaused;Rope.NeedsReset=true;Rope.ResetTimelineClock();
   if(!commit){Rope.TimelineSync=oldSync;Rope.UseBake=oldUseBake;}
   if(restoreClock&&timeline.Ready){try{timeline.Evaluate(returnTime);constraints.Apply();timeline.SetPlaying(returnPlaying);}catch(Exception ex){plugin.Message("Bake ended; Timeline restoration failed: "+ex.Message);}}
  }
 }
}
