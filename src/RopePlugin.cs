using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using ExtensibleSaveFormat;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using Studio;
using UnityEngine;
using UnityEngine.UI;
using UObj=UnityEngine.Object;
namespace GINRope {
 [BepInPlugin(Guid,"GIN Rope This","1.1.2")]
 [BepInProcess("CharaStudio")]
 [BepInDependency(KKAPI.KoikatuAPI.GUID,BepInDependency.DependencyFlags.HardDependency)]
 [BepInDependency("com.joan6694.illusionplugins.nodesconstraints",BepInDependency.DependencyFlags.SoftDependency)]
 [DefaultExecutionOrder(1000000)]
 public sealed partial class Plugin:BaseUnityPlugin {
  public const string Guid="local.kkworkshop.0a52b20541f5.gin-rope-this";
  public static Plugin Instance;
  public readonly List<Rope> Ropes=new List<Rope>();
  Rect window=new Rect(80,60,440,620);
  bool shown=false;
  bool autoRigCandidate;
  int autoRigIntermediate=10;
  ConfigEntry<KeyboardShortcut> shortcut;
  OCIItem choosing;
  List<Transform> candidates=new List<Transform>();
  Transform selectedA,selectedB;
  Vector2 scroll;
  string filter="",status="Select a skinned chain item, then Rope This.";
  Rope active;
  KKAPI.Studio.UI.ToolbarButton toolbar;
  Texture2D toolbarTexture;
  float accumulator;
  int simulatedFrame=-1;
  readonly List<PendingLoad> pending=new List<PendingLoad>();
  int loadGeneration;
  public TimelineBridge Timeline;
  public ConstraintBridge Constraints;
  public BakeTask Baking;
  float nextIntegrationCheck;
  sealed class PendingLoad {public Record Record;public Dictionary<int,ObjectCtrlInfo> Objects;public int Generation;public int Frame;}
  void Awake(){
   Instance=this;
   shortcut=Config.Bind<KeyboardShortcut>("Interface","Toggle panel",new KeyboardShortcut(KeyCode.F8),new ConfigDescription("Show or hide GIN Rope This.",null,new object[0]));
   StudioSaveLoadApi.RegisterExtraBehaviour<SceneController>(Guid);
   Camera.onPreCull+=BeforeCamera;
   Timeline=new TimelineBridge();Constraints=new ConstraintBridge();
#if KKS
   Logger.LogInfo("GIN Rope This 1.1.2 loaded (KKS).");
#else
   Logger.LogInfo("GIN Rope This 1.1.2 loaded (KK).");
#endif
  }
  void Start(){StartCoroutine(AddToolbar());}
  IEnumerator AddToolbar(){
   float deadline=Time.realtimeSinceStartup+120;
   while(!KKAPI.Studio.StudioAPI.StudioLoaded&&Time.realtimeSinceStartup<deadline)yield return null;
   if(!KKAPI.Studio.StudioAPI.StudioLoaded){Logger.LogWarning("Studio Option toolbar not ready. Use F8 to open Rope This.");yield break;}
   // Use Studio's Option toolbar registration and layout, including its native
   // visibility/hover/click handling. Never attach controls to the workspace.
   using(System.IO.Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("GINRope.Resources.RopeThis.png")){
    if(stream==null)throw new InvalidOperationException("Rope This button image missing.");
    byte[] pixels=new byte[stream.Length];int offset=0,read;
    while(offset<pixels.Length&&(read=stream.Read(pixels,offset,pixels.Length-offset))>0)offset+=read;
    toolbarTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);
    if(!toolbarTexture.LoadImage(pixels))throw new InvalidOperationException("Rope This button image invalid.");
    toolbarTexture.name="GIN Rope This";toolbarTexture.filterMode=FilterMode.Bilinear;
   }
   toolbar=KKAPI.Studio.UI.CustomToolbarButtons.AddLeftToolbarButton(toolbarTexture,ToolbarClick);
   while(!toolbar.Exists&&Time.realtimeSinceStartup<deadline)yield return null;
   if(toolbar.Exists)toolbar.ControlObject.name="GIN Rope This";
   else Logger.LogWarning("Option button was not created. Use F8 to open Rope This.");
  }
  void SetPanelVisible(bool visible){if(visible&&!shown)RefreshSelectionForOpen();shown=visible;}
  void ToolbarClick(){SetPanelVisible(!shown);}
  void Update(){
   if(shortcut.Value.IsDown())ToolbarClick();
   ProcessPending();
   if(Time.unscaledTime>=nextIntegrationCheck){nextIntegrationCheck=Time.unscaledTime+1;if(!Constraints.Ready)Constraints=new ConstraintBridge();if(!Timeline.Ready)Timeline=new TimelineBridge();}
   UpdateTargetSelection();
   if(Baking!=null){try{Baking.Step();}catch(Exception ex){Baking.Cancel(true);Message("Bake cancelled: "+ex.Message);}if(Baking.Finished)Baking=null;}
  }
  void LateUpdate(){Tick(true);}
  void BeforeCamera(Camera camera){if(camera==Camera.main)Tick(false);}
  void Tick(bool advance){
   if(StudioSaveLoadApi.LoadInProgress||StudioSaveLoadApi.ImportInProgress)return;
   if(Baking!=null)return;
   bool integrate=advance&&simulatedFrame!=Time.frameCount;
   int steps=0;
   if(integrate){
    simulatedFrame=Time.frameCount;float dt=Time.deltaTime;
    if(dt>0.2f){foreach(Rope rope in Ropes)rope.NeedsReset=true;accumulator=0;}
    else accumulator+=Mathf.Clamp(dt,0,0.066667f);
    steps=Math.Min(8,(int)(accumulator/(1f/120f)));accumulator-=steps*(1f/120f);
   }
   for(int i=Ropes.Count-1;i>=0;i--){
    Rope rope=Ropes[i];
    try {
     if(!rope.Valid){rope.Restore();Ropes.RemoveAt(i);if(active==rope)active=null;continue;}
     if(rope.UseBake&&rope.Bake!=null)rope.PlayBake(Timeline.Ready?Timeline.Time:rope.BakePreviewTime);
     else if(rope.TimelineSync&&Timeline.Ready)rope.TickTimeline(Timeline.Time,integrate);else rope.Tick(steps);
    }catch(Exception ex){rope.StopAfterError();status="Rope stopped: "+ex.Message;Logger.LogError(ex);}
   }
  }
  public void Message(string text){status=text;Logger.LogInfo(text);}
  OCIItem SelectedItem(){
   ObjectCtrlInfo[] selected=Studio.Studio.GetSelectObjectCtrl();
   foreach(ObjectCtrlInfo info in selected){OCIItem item=info as OCIItem;if(item!=null)return item;}
   return null;
  }
  Rope SelectedRope(){
   ObjectCtrlInfo[] selected=Studio.Studio.GetSelectObjectCtrl();
   foreach(ObjectCtrlInfo info in selected)foreach(Rope rope in Ropes)if(rope.Item==info||rope.A==info||rope.B==info)return rope;
   return null;
  }
  void BeginSelection(){
   ResetSelectionUi();autoRigCandidate=false;
   pickingRope=null;pickingTarget=null;
   choosing=SelectedItem();active=null;selectedA=null;selectedB=null;candidates.Clear();filter="";
   if(choosing==null){status="Select one Studio item first.";return;}
   try{ValidateItem(choosing);}catch(Exception ex){choosing=null;status=ex.Message;return;}
   foreach(Rope rope in Ropes)if(rope.Item==choosing){active=rope;choosing=null;status="This item is already controlled.";return;}
   HashSet<Transform> unique=new HashSet<Transform>();
   foreach(SkinnedMeshRenderer mesh in choosing.objectItem.GetComponentsInChildren<SkinnedMeshRenderer>(true))
    foreach(Transform bone in mesh.bones)
     if(bone!=null&&bone.IsChildOf(choosing.objectItem.transform)&&unique.Add(bone))candidates.Add(bone);
   candidates.Sort(delegate(Transform a,Transform b){return String.Compare(PathOf(choosing.objectItem.transform,a),PathOf(choosing.objectItem.transform,b),StringComparison.Ordinal);});
   if(candidates.Count<=1||choosing.listBones==null||choosing.listBones.Count<=1){autoRigCandidate=true;status="Generate a rope along the item's length automatically.";return;}
   if(candidates.Count<3){choosing=null;status="Needs at least 3 chain nodes, or an item without an FK chain.";return;}
   SuggestEndpoints();
  }
  void OnGUI(){
   if(!shown||Singleton<Studio.Studio>.Instance==null)return;
   DrawEndpointOverlay();
   window.height=SimpleWindowHeight();
   window.x=Mathf.Clamp(window.x,0,Mathf.Max(0,Screen.width-window.width));window.y=Mathf.Clamp(window.y,0,Mathf.Max(0,Screen.height-window.height));
   window=GUILayout.Window(0x47494E52,window,DrawWindow,"GIN Rope This",new GUILayoutOption[0]);
   IMGUIUtils.EatInputInRect(window);
  }
  void DrawWindow(int id){DrawSimpleWindow(id);}
  static double Slider(string label,double value,float min,float max){
   GUILayout.Label(label+": "+value.ToString("0.00"),new GUILayoutOption[0]);return GUILayout.HorizontalSlider((float)value,min,max,new GUILayoutOption[0]);
  }
  static void Select(ObjectCtrlInfo info){if(info!=null)Singleton<Studio.Studio>.Instance.treeNodeCtrl.SelectSingle(info.treeNodeObject,true);}
  void CreateChosen(){
   OCIFolder a=null,b=null;
   try{
    if(Ropes.Count>=256)throw new InvalidOperationException("The scene already has 256 ropes.");
    Transform[] chain=Chain(selectedA,selectedB);
    ValidateChain(choosing,chain);
    foreach(Rope current in Ropes)foreach(Transform bone in chain)if(Array.IndexOf(current.Bones,bone)>=0)throw new InvalidOperationException("This bone is already controlled by another rope.");
    a=NewHandle(choosing,"GIN Rope A",chain[0].position);
    b=NewHandle(choosing,"GIN Rope B",chain[chain.Length-1].position);
    active=new Rope(choosing,chain,a,b,null,null);Ropes.Add(active);choosing=null;
    status="Ready. Constrain GIN Rope A/B to scene targets, or move them directly.";
   }catch(Exception ex){
    DeleteNewHandle(a);DeleteNewHandle(b);
    status=ex.Message;Logger.LogWarning(ex);
   }
  }
  void CreateAutoRig(){
   AutoRig rig=null;OCIFolder a=null,b=null;
   try{
    if(choosing==null||!autoRigCandidate)throw new InvalidOperationException("Select an item without an FK chain first.");
    ValidateItem(choosing);
    if(Ropes.Count>=256)throw new InvalidOperationException("The scene already has 256 ropes.");
    foreach(Rope current in Ropes)if(current.Item==choosing)throw new InvalidOperationException("This item is already controlled.");
    rig=AutoRig.Build(choosing,autoRigIntermediate);
    a=NewHandle(choosing,"GIN Rope A",rig.Bones[0].position);
    b=NewHandle(choosing,"GIN Rope B",rig.Bones[rig.Bones.Length-1].position);
    Rope created=new Rope(choosing,rig.Bones,a,b,null,null,rig);
    active=created;Ropes.Add(created);choosing=null;autoRigCandidate=false;
    status="Ready. Use NodesConstraints or move GIN Rope A/B directly.";
   }catch(Exception ex){if(rig!=null)rig.Restore();DeleteNewHandle(a);DeleteNewHandle(b);Message("Could not generate rope: "+ex.Message);}
  }
  static void DeleteNewHandle(OCIFolder handle){
   if(handle==null)return;
   try{Singleton<Studio.Studio>.Instance.treeNodeCtrl.DeleteNode(handle.treeNodeObject);}catch(Exception ex){if(Instance!=null)Instance.Logger.LogWarning(ex);}
  }
  public static void ValidateItem(OCIItem item){
   if(item==null||item.objectItem==null)throw new ArgumentException("Select an existing Studio item first.");
   // Thread owns its private mesh and its N/S controls. Never take it over,
   // including before that plugin's delayed scan has registered the item.
   foreach(SkinnedMeshRenderer mesh in item.objectItem.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
    if(mesh.sharedMesh==null)continue;
    string name=mesh.sharedMesh.name;
    if(name.StartsWith("GIN_LQ_single",StringComparison.Ordinal)||name.StartsWith("GIN_LQ_necked",StringComparison.Ordinal))
     throw new InvalidOperationException("GIN Liquid Threads item: use Thread N / Thread S; Rope This cannot take over this item.");
   }
   foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()){
    Type type=assembly.GetType("GINLiquid.LiquidPlugin");if(type==null)continue;
    FieldInfo instanceField=type.GetField("Instance"),threadsField=type.GetField("Threads");
    object instance=instanceField==null?null:instanceField.GetValue(null);
    IDictionary threads=instance==null||threadsField==null?null:threadsField.GetValue(instance) as IDictionary;
    if(threads!=null&&threads.Contains(item.objectInfo.dicKey))throw new InvalidOperationException("This item is controlled by GIN Liquid Threads. Use Thread N / Thread S.");
   }
   foreach(Component component in item.objectItem.GetComponentsInChildren<Component>(true))
    if(component!=null&&component.GetType().Name=="PhysicsChain")throw new InvalidOperationException("Remove PhysicsChain from this item before adding GIN Rope.");
  }
  public static void CheckVector(Vector3 value,string label){
   if(!Settings.Finite(value.x)||!Settings.Finite(value.y)||!Settings.Finite(value.z)||Math.Abs(value.x)>1000000||Math.Abs(value.y)>1000000||Math.Abs(value.z)>1000000)
    throw new ArgumentException(label+" has invalid or excessive coordinates.");
  }
  public static void CheckScale(Vector3 value){
   CheckVector(value,"Scale");if(Math.Abs(value.x)<0.000001f||Math.Abs(value.y)<0.000001f||Math.Abs(value.z)<0.000001f)throw new ArgumentException("Zero or near-zero scale is not supported.");
  }
  public static void CheckRotation(Quaternion value){
   double norm=(double)value.x*value.x+(double)value.y*value.y+(double)value.z*value.z+(double)value.w*value.w;
   if(!Settings.Finite(norm)||norm<0.000000000001||norm>1000000)throw new ArgumentException("Invalid bone rotation.");
  }
  public static void ValidateChain(OCIItem item,Transform[] bones){
   ValidateChain(item,bones,null);
  }
  internal static void ValidateChain(OCIItem item,Transform[] bones,AutoRig generated){
   ValidateItem(item);
   if(bones==null||bones.Length<3||bones.Length>128)throw new ArgumentException("Choose a chain of 3 to 128 nodes.");
   Transform root=item.objectItem.transform;CheckVector(root.position,"Item");CheckScale(root.lossyScale);
   Transform[] actual=Chain(bones[0],bones[bones.Length-1]);
   if(actual.Length!=bones.Length)throw new ArgumentException("Saved bone chain no longer matches the item.");
   HashSet<Transform> skin=new HashSet<Transform>();
   foreach(SkinnedMeshRenderer mesh in item.objectItem.GetComponentsInChildren<SkinnedMeshRenderer>(true))foreach(Transform bone in mesh.bones)if(bone!=null)skin.Add(bone);
   // An owned automatic chain drives separate rendering frames. Authored
   // chains still require genuine skin endpoints; automatic ownership is exact.
   if(generated!=null){if(!generated.Owns(item,bones))throw new ArgumentException("The generated chain does not belong to this item.");}
   else if(!skin.Contains(bones[0])||!skin.Contains(bones[bones.Length-1]))throw new ArgumentException("Both endpoints must be skin bones of the selected item.");
   for(int i=0;i<bones.Length;i++){
    if(bones[i]==null||bones[i]!=actual[i]||!bones[i].IsChildOf(root))throw new ArgumentException("Bones must belong to the selected item's chain.");
    CheckVector(bones[i].position,"Bone");CheckScale(bones[i].lossyScale);CheckRotation(bones[i].rotation);
   }
  }
  public static Transform[] Chain(Transform a,Transform b){
   if(a==null||b==null||a==b)throw new ArgumentException("Choose two different bones.");
   if(!b.IsChildOf(a)){if(a.IsChildOf(b)){Transform swap=a;a=b;b=swap;}else throw new ArgumentException("Endpoints must belong to one ancestor/descendant chain.");}
   List<Transform> path=new List<Transform>();Transform current=b;
   while(current!=null){path.Add(current);if(current==a)break;current=current.parent;}
   path.Reverse();
   if(path.Count<3||path.Count>128)throw new ArgumentException("Choose a chain of 3 to 128 nodes.");
   for(int i=0;i<path.Count-1;i++){
    CheckVector(path[i].position,"Bone");CheckVector(path[i+1].position,"Bone");
    if((path[i+1].position-path[i].position).sqrMagnitude<0.0000000001f)throw new ArgumentException("Adjacent nodes overlap.");
    foreach(Transform child in path[i])if(child!=path[i+1])throw new ArgumentException("Branched bones are not supported in this prototype.");
   }
   return path.ToArray();
  }
  public static OCIFolder NewHandle(OCIItem owner,string name,Vector3 position){
   OCIFolder f=AddObjectFolder.Add();f.name=name;
   try{
    Singleton<Studio.Studio>.Instance.treeNodeCtrl.SetParent(f.treeNodeObject,owner.treeNodeObject);
    f.guideObject.changeAmount.pos=owner.objectItem.transform.InverseTransformPoint(position);
    return f;
   }catch{DeleteNewHandle(f);throw;}
  }
  public static string PathOf(Transform root,Transform node){
   List<string> indices=new List<string>();
   while(node!=root&&node!=null){indices.Add(node.GetSiblingIndex().ToString());node=node.parent;}
   if(node==null)throw new ArgumentException("Bone is outside item hierarchy.");
   indices.Reverse();return String.Join("/",indices.ToArray());
  }
  public static Transform Resolve(Transform root,string path){
   if(path.Length==0)return root;
   foreach(string segment in path.Split('/')){int index;if(!Int32.TryParse(segment,out index)||index<0||index>=root.childCount)return null;root=root.GetChild(index);}
   return root;
  }
  static string DisplayPath(Transform root,Transform node){
   List<string> names=new List<string>();while(node!=null&&node!=root){names.Add(node.name);node=node.parent;}names.Reverse();return String.Join("/",names.ToArray());
  }
  public void ClearRopes(){
   if(Baking!=null){Baking.Cancel(false);Baking=null;}
   pickingRope=null;pickingTarget=null;
   loadGeneration++;pending.Clear();foreach(Rope rope in Ropes)rope.Restore();Ropes.Clear();active=null;choosing=null;accumulator=0;
  }
  public void Queue(List<Record> records,ReadOnlyDictionary<int,ObjectCtrlInfo> loaded){
   Dictionary<int,ObjectCtrlInfo> map=new Dictionary<int,ObjectCtrlInfo>();foreach(KeyValuePair<int,ObjectCtrlInfo> p in loaded)map[p.Key]=p.Value;
   foreach(Record record in records)pending.Add(new PendingLoad{Record=record,Objects=map,Generation=loadGeneration,Frame=Time.frameCount+5});
  }
  void ProcessPending(){
   for(int i=pending.Count-1;i>=0;i--){
    PendingLoad p=pending[i];if(Time.frameCount<p.Frame)continue;pending.RemoveAt(i);
    if(p.Generation!=loadGeneration)continue;
    OCIFolder newA=null,newB=null;AutoRig generated=null;
    try{
     ObjectCtrlInfo info;if(!p.Objects.TryGetValue(p.Record.Item,out info))continue;
     OCIItem item=info as OCIItem;if(item==null)continue;
     bool exists=false;foreach(Rope rope in Ropes)if(rope.Item==item)exists=true;if(exists)continue;
     Transform[] bones=new Transform[p.Record.Paths.Length];bool valid=true;
     if(p.Record.AutoRig){
      ValidateItem(item);
      float[] start=p.Record.AutoRigStart,end=p.Record.AutoRigEnd;
      generated=AutoRig.Build(item,p.Record.AutoRigVertical,p.Record.AutoRigIntermediate,new Vector3(start[0],start[1],start[2]),new Vector3(end[0],end[1],end[2]),p.Record.AutoRigSources);bones=generated.Bones;
     }else for(int j=0;j<bones.Length;j++){bones[j]=Resolve(item.objectItem.transform,p.Record.Paths[j]);if(bones[j]==null)valid=false;}
     if(!valid){Message("Skipped rope: saved bones no longer exist.");continue;}
     ValidateChain(item,bones,generated);Rope.ValidatePoses(p.Record.Poses,bones.Length);p.Record.Settings.Validate();
     if(Ropes.Count>=256)throw new InvalidOperationException("The scene already has 256 ropes.");
     ObjectCtrlInfo foundA,foundB;p.Objects.TryGetValue(p.Record.A,out foundA);p.Objects.TryGetValue(p.Record.B,out foundB);
     OCIFolder a=foundA as OCIFolder,b=foundB as OCIFolder;
     if(a==null)a=newA=NewHandle(item,"GIN Rope A",bones[0].position);
     if(b==null)b=newB=NewHandle(item,"GIN Rope B",bones[bones.Length-1].position);
     Rope restored=new Rope(item,bones,a,b,p.Record.Poses,p.Record.Settings.Copy(),generated);restored.TimelineSync=p.Record.TimelineSync;restored.UseBake=p.Record.UseBake;restored.Bake=p.Record.Bake;
     if(restored.Bake!=null)restored.BakePreviewTime=restored.Bake.Start;Ropes.Add(restored);
    }catch(Exception ex){if(generated!=null)generated.Restore();DeleteNewHandle(newA);DeleteNewHandle(newB);Logger.LogError(ex);status="Could not restore one rope: "+ex.Message;}
   }
  }
  public List<Record> Records(){List<Record> records=new List<Record>();foreach(Rope rope in Ropes)if(rope.Valid)records.Add(rope.Save());return records;}
  void OnDestroy(){
   Camera.onPreCull-=BeforeCamera;ClearRopes();
   if(toolbar!=null){
    foreach(GameObject control in toolbar.ControlObjects)if(control!=null){UnityEngine.UI.Button button=control.GetComponent<UnityEngine.UI.Button>();if(button!=null&&button.image!=null&&button.image.sprite!=null)UObj.Destroy(button.image.sprite);UObj.Destroy(control);}
    toolbar.Dispose();toolbar=null;
   }
   if(toolbarTexture!=null)UObj.Destroy(toolbarTexture);
   if(Instance==this)Instance=null;
  }
 }
 public sealed class Rope {
  public readonly OCIItem Item;
  public readonly OCIFolder A,B;
  public readonly Transform[] Bones;
  public readonly Settings Settings;
  public AutoRig AutoRig;
  public bool NeedsReset=true;
  public bool TimelineSync,UseBake;
  public BakeClip Bake;
  public double BakePreviewTime;
  double lastTimeline=-1,timelineAccumulator;
  V3[] bakePoints;
  public bool Faulted{get;private set;}
  readonly float[][] original;
  readonly Vector3[] restDirection;
  readonly Quaternion[] restRotation;
  readonly List<Behaviour> drivers=new List<Behaviour>();
  readonly List<bool> enabledStates=new List<bool>();
  Solver solver;
  V3 lastA,lastB;
  Vector3 lastScale;
  public bool Valid{get{
   if(Item==null||Item.objectItem==null||A==null||B==null||A.objectItem==null||B.objectItem==null)return false;
   foreach(Transform bone in Bones)if(bone==null)return false;return true;
  }}
  static V3 V(Vector3 v){return new V3(v.x,v.y,v.z);}
  static Vector3 U(V3 v){return new Vector3((float)v.X,(float)v.Y,(float)v.Z);}
  public Rope(OCIItem item,Transform[] bones,OCIFolder a,OCIFolder b,float[][] poses,Settings settings):this(item,bones,a,b,poses,settings,null){}
  public Rope(OCIItem item,Transform[] bones,OCIFolder a,OCIFolder b,float[][] poses,Settings settings,AutoRig generated){
   Plugin.ValidateChain(item,bones,generated);AutoRig=generated;
   if(a==null||b==null||a.objectItem==null||b.objectItem==null)throw new ArgumentException("Both Studio handles are required.");
   Item=item;Bones=bones;A=a;B=b;Settings=settings??new Settings();Settings.Validate();
   float[][] before=Capture(bones);original=poses??before;ValidatePoses(original,bones.Length);
   restDirection=new Vector3[bones.Length-1];restRotation=new Quaternion[bones.Length];
   try{
    RestoreBones();V3[] points=new V3[bones.Length];
    Transform root=item.objectItem.transform;lastScale=root.lossyScale;
    for(int i=0;i<bones.Length;i++){
     Plugin.CheckVector(bones[i].position,"Bone");Plugin.CheckRotation(bones[i].rotation);
     points[i]=V(bones[i].position);restRotation[i]=Quaternion.Inverse(root.rotation)*bones[i].rotation;
     if(i<bones.Length-1)restDirection[i]=root.InverseTransformDirection(bones[i+1].position-bones[i].position);
    }
    solver=new Solver(points);
    HashSet<Transform> foreignItems=new HashSet<Transform>();
    foreach(ObjectCtrlInfo info in Singleton<Studio.Studio>.Instance.dicObjectCtrl.Values){OCIItem other=info as OCIItem;if(other!=null&&other!=item&&other.objectItem!=null&&other.objectItem.transform.IsChildOf(root))foreignItems.Add(other.objectItem.transform);}
    foreach(Behaviour behaviour in item.objectItem.GetComponentsInChildren<Behaviour>(true)){
     if(behaviour==null)continue;
     bool owned=true;for(Transform parent=behaviour.transform;parent!=null&&parent!=root;parent=parent.parent)if(foreignItems.Contains(parent)){owned=false;break;}
     if(!owned)continue;
     string name=behaviour.GetType().Name;
     if(name=="ItemFKCtrl"||name.IndexOf("DynamicBone",StringComparison.Ordinal)>=0||name=="CCDIK"||name=="FABRIK"||behaviour is Animator||behaviour is Animation){
      drivers.Add(behaviour);enabledStates.Add(behaviour.enabled);behaviour.enabled=false;
     }
    }
   }catch{
    ApplyPoses(before);
    for(int i=0;i<drivers.Count;i++)if(drivers[i]!=null)drivers[i].enabled=enabledStates[i];
    throw;
   }
  }
  public int TimelineSteps(double time,bool advance){
   if(!advance)return 0;if(!GINRope.Settings.Finite(time))throw new ArgumentException("Invalid Timeline time.");
   double delta=time-lastTimeline;
   if(lastTimeline<0||delta<-.000001||delta>.2){NeedsReset=true;timelineAccumulator=0;lastTimeline=time;return 0;}
   lastTimeline=time;timelineAccumulator+=Math.Max(0,delta);int steps=Math.Min(24,(int)(timelineAccumulator*120+0.0000001));timelineAccumulator-=steps/120.0;return steps;
  }
  public void ResetTimelineClock(){lastTimeline=-1;timelineAccumulator=0;}
  public void Tick(int steps){TickCore(steps,1.0/120,false);}
  public void TickTimeline(double time,bool advance){TickCore(TimelineSteps(time,advance),1.0/120,true);}
  public void BakeStep(double h){TickCore(h>0?1:0,h>0?h:1.0/120,false);}
  void TickCore(int steps,double h,bool freezeWithoutTime){
   if(Faulted){if(Settings.Paused||!NeedsReset)return;Faulted=false;}
   Settings.Validate();
   Plugin.CheckVector(A.objectItem.transform.position,"Anchor A");Plugin.CheckVector(B.objectItem.transform.position,"Anchor B");
   Plugin.CheckVector(Item.objectItem.transform.position,"Item");Plugin.CheckRotation(Item.objectItem.transform.rotation);
   V3 a=V(A.objectItem.transform.position),b=V(B.objectItem.transform.position);
   if(!a.Finite||!b.Finite)throw new InvalidOperationException("Invalid anchor position.");
   Vector3 scale=Item.objectItem.transform.lossyScale;
   Plugin.CheckScale(scale);
   if((scale-lastScale).sqrMagnitude>0.000001f){RestoreBones();V3[] points=new V3[Bones.Length];for(int i=0;i<Bones.Length;i++)points[i]=V(Bones[i].position);solver=new Solver(points);NeedsReset=true;lastScale=scale;}
   double jump=Math.Max((a-lastA).Length,(b-lastB).Length);
   if(jump>Math.Max(1,solver.AuthoredLength*0.75))NeedsReset=true;
   if(NeedsReset){solver.Reset(a,b,Settings);NeedsReset=false;}
   if(!Settings.Paused){
    for(int i=0;i<steps;i++)solver.Step(h,a,b,Settings);
    if(!freezeWithoutTime||steps>0||(a-lastA).Length>.0000001||(b-lastB).Length>.0000001)solver.Reconcile(a,b,Settings);
   }
   lastA=a;lastB=b;
   ApplyPoints(solver.P);
  }
  void ApplyPoints(V3[] points){
   Transform root=Item.objectItem.transform;Quaternion last=Quaternion.identity;
   for(int i=0;i<Bones.Length;i++)Plugin.CheckVector(U(points[i]),"Simulated bone");
   for(int i=0;i<Bones.Length;i++){
    if(i<Bones.Length-1){
     Vector3 direction=U(points[i+1]-points[i]);
     if(direction.sqrMagnitude>0.0000000001f)last=Quaternion.FromToRotation(root.TransformDirection(restDirection[i]),direction);
    }
    Bones[i].position=U(points[i]);Bones[i].rotation=last*(root.rotation*restRotation[i]);
   }
   if(AutoRig!=null)AutoRig.UpdateBounds();
  }
  public float[] CaptureBakeFrame(){
   float[] frame=new float[Bones.Length*3];Transform root=Item.objectItem.transform;
   for(int i=0;i<Bones.Length;i++){Vector3 value=root.InverseTransformPoint(Bones[i].position);Plugin.CheckVector(value,"Bake");frame[i*3]=value.x;frame[i*3+1]=value.y;frame[i*3+2]=value.z;}return frame;
  }
  public void PlayBake(double time){
   if(Bake==null)return;if(Faulted){if(Settings.Paused)return;Faulted=false;}
   if(Bake.Nodes!=Bones.Length)throw new ArgumentException("Bake does not match this rope.");
   Transform root=Item.objectItem.transform;Plugin.CheckScale(root.lossyScale);Plugin.CheckRotation(root.rotation);
   if(bakePoints==null)bakePoints=new V3[Bones.Length];Bake.Sample(time,bakePoints);
   for(int i=0;i<bakePoints.Length;i++)bakePoints[i]=V(root.TransformPoint(U(bakePoints[i])));ApplyPoints(bakePoints);
  }
  public void StopAfterError(){
   Faulted=true;Settings.Paused=true;NeedsReset=true;RestoreBones();
   // Restore corrected skin matrices as well as transforms. If the item root
   // itself is invalid, keep the previous matrices while reporting the error.
   if(AutoRig!=null)try{AutoRig.UpdateBounds();}catch{}
  }
  public static void ValidatePoses(float[][] poses,int count){
   if(poses==null||poses.Length!=count)throw new ArgumentException("Pose length mismatch.");
   foreach(float[] p in poses){
    if(p==null||p.Length!=10)throw new ArgumentException("Invalid saved bone pose.");
    for(int i=0;i<10;i++)if(!GINRope.Settings.Finite(p[i]))throw new ArgumentException("Invalid saved bone pose.");
    Plugin.CheckVector(new Vector3(p[0],p[1],p[2]),"Saved pose");Plugin.CheckRotation(new Quaternion(p[3],p[4],p[5],p[6]));Plugin.CheckScale(new Vector3(p[7],p[8],p[9]));
   }
  }
  public void Restore(){RestoreBones();for(int i=0;i<drivers.Count;i++)if(drivers[i]!=null)drivers[i].enabled=enabledStates[i];if(AutoRig!=null){AutoRig.Restore();AutoRig=null;}}
  void RestoreBones(){ApplyPoses(original);}
  void ApplyPoses(float[][] poses){
   for(int i=0;i<Bones.Length;i++)if(Bones[i]!=null){float[] p=poses[i];Bones[i].localPosition=new Vector3(p[0],p[1],p[2]);Bones[i].localRotation=new Quaternion(p[3],p[4],p[5],p[6]);Bones[i].localScale=new Vector3(p[7],p[8],p[9]);}
  }
  static float[][] Capture(Transform[] bones){
   float[][] poses=new float[bones.Length][];
   for(int i=0;i<bones.Length;i++){Vector3 p=bones[i].localPosition,s=bones[i].localScale;Quaternion q=bones[i].localRotation;poses[i]=new float[]{p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z};}
   return poses;
  }
  public Record Save(){
   Record record=new Record();record.Item=Item.objectInfo.dicKey;record.A=A.objectInfo.dicKey;record.B=B.objectInfo.dicKey;
   record.Paths=new string[Bones.Length];for(int i=0;i<Bones.Length;i++)record.Paths[i]=Plugin.PathOf(Item.objectItem.transform,Bones[i]);
   record.Poses=original;record.Settings=Settings.Copy();record.TimelineSync=TimelineSync;record.UseBake=UseBake;record.Bake=Bake;
   if(AutoRig!=null){record.AutoRig=true;record.AutoRigVertical=AutoRig.Vertical;record.AutoRigIntermediate=AutoRig.Intermediate;record.AutoRigStart=new float[]{AutoRig.Start.x,AutoRig.Start.y,AutoRig.Start.z};record.AutoRigEnd=new float[]{AutoRig.End.x,AutoRig.End.y,AutoRig.End.z};record.AutoRigSources=AutoRig.SourcePaths;}
   return record;
  }
 }
 public sealed class SceneController:SceneCustomFunctionController {
  protected override void OnSceneSave(){
   if(Plugin.Instance==null)return;
   if(Plugin.Instance.Baking!=null)Plugin.Instance.CancelBake();
   PluginData data=new PluginData();data.version=3;data.data["ropes"]=Codec.Encode(Plugin.Instance.Records());SetExtendedData(data);
  }
  protected override void OnSceneLoad(SceneOperationKind operation,ReadOnlyDictionary<int,ObjectCtrlInfo> loadedObjects){
   if(Plugin.Instance==null)return;
   if(operation!=SceneOperationKind.Import)Plugin.Instance.ClearRopes();
   if(operation==SceneOperationKind.Clear)return;
   PluginData data=GetExtendedData();if(data==null||!data.data.ContainsKey("ropes"))return;
   try{Plugin.Instance.Queue(Codec.Decode(data.data["ropes"] as byte[]),loadedObjects);}catch(Exception ex){Plugin.Instance.Message("Rope scene data could not be read: "+ex.Message);}
  }
  protected override void OnObjectsCopied(ReadOnlyDictionary<int,ObjectCtrlInfo> copiedItems){
   if(Plugin.Instance!=null&&Plugin.Instance.Baking!=null)Plugin.Instance.CancelBake();
   if(Plugin.Instance!=null)Plugin.Instance.Queue(Plugin.Instance.Records(),copiedItems);
  }
  protected override void OnObjectDeleted(ObjectCtrlInfo deletedObject){
   if(Plugin.Instance==null)return;
   for(int i=Plugin.Instance.Ropes.Count-1;i>=0;i--){Rope rope=Plugin.Instance.Ropes[i];if(rope.Item==deletedObject||rope.A==deletedObject||rope.B==deletedObject){rope.Restore();Plugin.Instance.Ropes.RemoveAt(i);}}
  }
 }
}
