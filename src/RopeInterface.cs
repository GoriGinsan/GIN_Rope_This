using System;
using System.Collections.Generic;
using System.Globalization;
using Studio;
using UnityEngine;

namespace GINRope {
 public sealed partial class Plugin {
  Vector2 panelScroll,targetScroll;
  int panelTab,pickingEndpoint=-1;
  Rope pickingRope;
  ObjectCtrlInfo pickingTarget;
  bool keepTargetPosition,showTargetBones;
  string targetFilter="",bakeStart="0",bakeEnd="2";
  int bakeFps=30;
  List<Transform> targetTransforms=new List<Transform>();

  public void SuggestEndpoints(){
   if(choosing==null)return;selectedA=selectedB=null;
   if(candidates.Count>2048){status="Many skin bones. Select endpoints manually.";return;}
   HashSet<Transform> skin=new HashSet<Transform>(candidates),ancestors=new HashSet<Transform>();
   Transform root=choosing.objectItem.transform;
   foreach(Transform bone in candidates){Transform parent=bone.parent;int guard=0;while(parent!=null&&parent!=root&&guard++<512){ancestors.Add(parent);parent=parent.parent;}}
   List<Transform[]> proposals=new List<Transform[]>();
   foreach(Transform end in candidates){
    if(ancestors.Contains(end))continue;Transform first=end,current=end;int guard=0;
    while(current.parent!=null&&current.parent!=root&&current.parent.childCount==1&&guard++<512){current=current.parent;if(skin.Contains(current))first=current;}
    if(first==end)continue;
    try{Transform[] chain=Chain(first,end);ValidateChain(choosing,chain);proposals.Add(chain);}catch(ArgumentException){}
   }
   bool coversAll=proposals.Count==1;if(coversAll){HashSet<Transform> included=new HashSet<Transform>(proposals[0]);foreach(Transform bone in candidates)if(!included.Contains(bone))coversAll=false;}
   if(coversAll){Transform[] chain=proposals[0];selectedA=chain[0];selectedB=chain[chain.Length-1];status="Suggested A/B: "+chain.Length+" nodes. Check markers, then create.";}
   else status=proposals.Count>1?"Several bone chains found. Select A and B manually.":"No unique supported chain. Select A and B manually.";
  }
  public void StartBake(Rope rope,double start,double end,int fps){if(Baking!=null)throw new InvalidOperationException("A bake is already running.");Baking=new BakeTask(this,rope,start,end,fps);}
  public void CancelBake(){if(Baking!=null){Baking.Cancel(true);Baking=null;Message("Bake cancelled. Previous bake kept.");}}
  public void ArmTarget(Rope rope,bool north){pickingRope=rope;pickingEndpoint=north?0:1;pickingTarget=null;targetTransforms.Clear();targetFilter="";showTargetBones=false;status="Select the target in Studio, then attach below.";}
  public void AttachTarget(Transform target){
   if(pickingRope==null||!pickingRope.Valid)throw new InvalidOperationException("Select a rope first.");
   Constraints.Connect(pickingRope,pickingEndpoint==0,target,keepTargetPosition);status="Connected "+(pickingEndpoint==0?"A":"B")+" to "+SceneTargetLabel(target)+".";pickingRope=null;pickingTarget=null;pickingEndpoint=-1;
  }
  void UpdateTargetSelection(){
   if(pickingRope==null)return;if(!pickingRope.Valid){pickingRope=null;pickingTarget=null;return;}
   ObjectCtrlInfo[] selection=Studio.Studio.GetSelectObjectCtrl();if(selection.Length!=1)return;ObjectCtrlInfo target=selection[0];
   if(target==pickingRope.Item||target==pickingRope.A||target==pickingRope.B||target.guideObject==null||target==pickingTarget)return;
   pickingTarget=target;targetTransforms.Clear();Transform root=target.guideObject.transformTarget;
   foreach(Transform transform in root.GetComponentsInChildren<Transform>(true))if(transform!=root&&targetTransforms.Count<4096)targetTransforms.Add(transform);
  }
  void DrawConnections(Rope rope){
   if(rope.UseBake&&rope.Bake!=null)GUILayout.Label("Baked playback: rebake after changing targets.");
   GUILayout.Label("A: "+Constraints.TargetName(rope,true));GUILayout.Label("B: "+Constraints.TargetName(rope,false));
   GUILayout.BeginHorizontal();if(GUILayout.Button("Connect A"))ArmTarget(rope,true);if(GUILayout.Button("Connect B"))ArmTarget(rope,false);GUILayout.EndHorizontal();
   keepTargetPosition=GUILayout.Toggle(keepTargetPosition,"Keep current endpoint position (offset)");
   GUILayout.BeginHorizontal();if(GUILayout.Button("Disconnect A"))TryAction(delegate{Constraints.Disconnect(rope,true);});if(GUILayout.Button("Disconnect B"))TryAction(delegate{Constraints.Disconnect(rope,false);});GUILayout.EndHorizontal();
   if(pickingRope==null)return;
   GUILayout.Space(8);GUILayout.Label("Connecting "+(pickingEndpoint==0?"A":"B")+": select a Studio target.");
   if(pickingTarget!=null&&pickingTarget.guideObject!=null){
    if(GUILayout.Button("Attach to "+pickingTarget.treeNodeObject.textName)){TryAction(delegate{AttachTarget(pickingTarget.guideObject.transformTarget);});if(pickingTarget==null)return;}
    showTargetBones=GUILayout.Toggle(showTargetBones,"Choose a child transform / bone");
    if(showTargetBones){targetFilter=GUILayout.TextField(targetFilter);targetScroll=GUILayout.BeginScrollView(targetScroll,GUILayout.Height(170));foreach(Transform target in targetTransforms){if(target==null||target.name.IndexOf(targetFilter,StringComparison.OrdinalIgnoreCase)<0)continue;Transform captured=target;if(GUILayout.Button(DisplayPath(pickingTarget.guideObject.transformTarget,target)))TryAction(delegate{AttachTarget(captured);});if(pickingTarget==null)break;}GUILayout.EndScrollView();}
   }
   if(GUILayout.Button("Cancel connection")){pickingRope=null;pickingTarget=null;pickingEndpoint=-1;}
  }
  void DrawAnimation(Rope rope){
   bool sync=GUILayout.Toggle(rope.TimelineSync,"Follow Timeline time (live preview)");if(sync!=rope.TimelineSync){rope.TimelineSync=sync;rope.NeedsReset=true;rope.ResetTimelineClock();}
   if(Timeline.Ready)GUILayout.Label("Timeline: "+Timeline.Time.ToString("0.000")+" s");else GUILayout.Label("Timeline unavailable. Live physics still works.");
   GUILayout.Label("Preview resets when seeking. Bake for repeatable motion.");
   GUILayout.BeginHorizontal();GUILayout.Label("Start",GUILayout.Width(40));bakeStart=GUILayout.TextField(bakeStart);GUILayout.Label("End",GUILayout.Width(35));bakeEnd=GUILayout.TextField(bakeEnd);GUILayout.EndHorizontal();
   GUILayout.BeginHorizontal();if(GUILayout.Button("Start = current")&&Timeline.Ready)bakeStart=Timeline.Time.ToString("0.###",CultureInfo.InvariantCulture);if(GUILayout.Button("End = duration")&&Timeline.Ready)bakeEnd=Timeline.Duration.ToString("0.###",CultureInfo.InvariantCulture);GUILayout.EndHorizontal();
   bakeFps=GUILayout.Toolbar(bakeFps==30?0:1,new string[]{"30 fps","60 fps"})==0?30:60;
   GUI.enabled=Timeline.Ready;if(GUILayout.Button(rope.Bake==null?"Bake Timeline range":"Rebake Timeline range"))TryAction(delegate{double start,end;if(!Double.TryParse(bakeStart,NumberStyles.Float,CultureInfo.InvariantCulture,out start)||!Double.TryParse(bakeEnd,NumberStyles.Float,CultureInfo.InvariantCulture,out end))throw new ArgumentException("Enter valid start and end times.");StartBake(rope,start,end,bakeFps);});GUI.enabled=true;
   if(rope.Bake!=null){
    bool useBake=GUILayout.Toggle(rope.UseBake,"Play baked motion");if(useBake!=rope.UseBake){rope.UseBake=useBake;rope.NeedsReset=true;rope.ResetTimelineClock();}
    GUILayout.Label("Saved: "+rope.Bake.Start.ToString("0.###")+" - "+rope.Bake.End.ToString("0.###")+" s / "+rope.Bake.Fps+" fps");
    GUILayout.Label("Target or physics edits require a new bake.");
    if(!Timeline.Ready)rope.BakePreviewTime=GUILayout.HorizontalSlider((float)rope.BakePreviewTime,(float)rope.Bake.Start,(float)rope.Bake.End);
    if(GUILayout.Button("Clear bake")){rope.Bake=null;rope.UseBake=false;rope.NeedsReset=true;rope.ResetTimelineClock();}
   }
  }
  void TryAction(Action action){try{action();}catch(Exception ex){Message(ex.Message);}}
  public static string SceneTargetLabel(Transform target){
   if(target==null)return "missing target";ObjectCtrlInfo best=null;Transform bestRoot=null;int depth=Int32.MaxValue;
   foreach(ObjectCtrlInfo info in Singleton<Studio.Studio>.Instance.dicObjectCtrl.Values){
    if(info.guideObject==null)continue;Transform root=info.guideObject.transformTarget;if(root==null||!target.IsChildOf(root))continue;
    int distance=0;Transform current=target;while(current!=root){distance++;current=current.parent;}
    if(distance<depth){best=info;bestRoot=root;depth=distance;}
   }
   return best==null?target.name:best.treeNodeObject.textName+(depth==0?"":" / "+DisplayPath(bestRoot,target));
  }
  void DrawEndpointOverlay(){
   if(Event.current.type!=EventType.Repaint||Camera.main==null)return;
   Transform a=selectedA,b=selectedB;
   if(choosing==null&&active!=null&&active.Valid){a=active.Bones[0];b=active.Bones[active.Bones.Length-1];}
   if(a!=null)DrawMarker(a.position,"A",Color.cyan);if(b!=null)DrawMarker(b.position,"B",new Color(1,.4f,.85f));
   if(choosing!=null&&a!=null&&b!=null){try{Transform[] chain=Chain(a,b);for(int i=1;i<chain.Length;i++)DrawWorldLine(chain[i-1].position,chain[i].position,new Color(.1f,.9f,1,.7f));}catch(ArgumentException){}}
  }
  static void DrawMarker(Vector3 point,string label,Color color){Vector3 screen=Camera.main.WorldToScreenPoint(point);if(screen.z<=0)return;Color prior=GUI.color;GUI.color=color;GUI.Box(new Rect(screen.x-13,Screen.height-screen.y-13,26,26),label);GUI.color=prior;}
  static void DrawWorldLine(Vector3 a,Vector3 b,Color color){
   Vector3 pa=Camera.main.WorldToScreenPoint(a),pb=Camera.main.WorldToScreenPoint(b);if(pa.z<=0||pb.z<=0)return;
   Vector2 start=new Vector2(pa.x,Screen.height-pa.y),end=new Vector2(pb.x,Screen.height-pb.y),delta=end-start;if(delta.magnitude<.5f)return;
   Matrix4x4 matrix=GUI.matrix;Color prior=GUI.color;GUI.color=color;GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,start);GUI.DrawTexture(new Rect(start.x,start.y,delta.magnitude,1.5f),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=prior;
  }
 }
}
