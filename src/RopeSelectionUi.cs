using System;
using Studio;
using UnityEngine;

namespace GINRope {
 public sealed partial class Plugin {
  bool manualBoneOptions;
  int manualBoneEndpoint;
  GUIStyle leftBoneButton;

  internal void ResetSelectionUi(){
   manualBoneOptions=false;manualBoneEndpoint=0;filter="";
   scroll=Vector2.zero;panelScroll=Vector2.zero;
  }

  internal void RefreshSelectionForOpen(){
   Rope selected=SelectedRope();
   if(selected!=null){active=selected;choosing=null;autoRigCandidate=false;ResetSelectionUi();return;}
   OCIItem item=SelectedItem();
   if(item!=null&&item!=choosing){BeginSelection();return;}
   if(item==null&&choosing==null&&(active==null||!Ropes.Contains(active)))status="Select one Studio item first.";
  }

  float SimplePanelContentHeight(){
   float height=100;
   if(Baking!=null)height=100;
   else if(SelectedRope()!=null||(active!=null&&Ropes.Contains(active)))height=450;
   else if(choosing!=null)height=autoRigCandidate?180:manualBoneOptions?480:210;
   return Mathf.Clamp(height,100,Mathf.Max(100,Screen.height-150));
  }

  internal float SimpleWindowHeight(){return SimplePanelContentHeight()+85;}

  void DrawSimpleWindow(int id){
   if(leftBoneButton==null)leftBoneButton=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,wordWrap=true};
   if(Baking!=null){
    GUILayout.Label("Baking: "+(Baking.Progress*100).ToString("0")+"%");
    if(GUILayout.Button("Cancel bake"))CancelBake();
    GUI.DragWindow(new Rect(0,0,10000,20));return;
   }
   panelScroll=GUILayout.BeginScrollView(panelScroll,GUILayout.Height(SimplePanelContentHeight()));
   Rope selected=SelectedRope();if(selected!=null){active=selected;choosing=null;autoRigCandidate=false;}
   OCIItem selectedItem=SelectedItem();
   if(selectedItem!=null&&selectedItem!=choosing&&(active==null||selectedItem!=active.Item)){
    if(GUILayout.Button("Use selected item"))BeginSelection();
   }
   GUILayout.Label(status);
   DrawSimpleSelection();
   if(active!=null&&Ropes.Contains(active))DrawSimplePhysics(active);
   GUILayout.EndScrollView();
   GUILayout.Space(5);GUILayout.Label("Active ropes: "+Ropes.Count+"  |  F8: hide/show");
   if(GUILayout.Button("Close panel"))SetPanelVisible(false);
   GUI.DragWindow(new Rect(0,0,10000,20));
  }

  void DrawSimpleSelection(){
   if(choosing==null)return;
   GUILayout.Label("Item: "+choosing.treeNodeObject.textName);
   if(autoRigCandidate){
    GUILayout.Label("Intermediate bones: "+autoRigIntermediate);
    autoRigIntermediate=Mathf.Clamp(Mathf.RoundToInt(GUILayout.HorizontalSlider(autoRigIntermediate,3,30)),3,30);
    if(GUILayout.Button("Rope This(Vertical)"))CreateAutoRig(true);
    if(choosing!=null&&GUILayout.Button("Rope This(Horizontal)"))CreateAutoRig(false);
    return;
   }
   if(GUILayout.Button("A: "+(selectedA==null?"choose in Option":selectedA.name),leftBoneButton))manualBoneEndpoint=0;
   if(GUILayout.Button("B: "+(selectedB==null?"choose in Option":selectedB.name),leftBoneButton))manualBoneEndpoint=1;
   bool enabled=GUI.enabled;GUI.enabled=enabled&&selectedA!=null&&selectedB!=null;
   if(GUILayout.Button("Rope This"))CreateChosen();
   GUI.enabled=enabled;
   if(choosing==null)return;
   if(GUILayout.Button(manualBoneOptions?"Option (hide bone list)":"Option"))manualBoneOptions=!manualBoneOptions;
   if(!manualBoneOptions)return;
   GUILayout.Label("Choose the part of the chain to simulate.");
   GUILayout.BeginHorizontal();
   bool pickA=GUILayout.Toggle(manualBoneEndpoint==0,"Choose A",GUI.skin.button);
   bool pickB=GUILayout.Toggle(manualBoneEndpoint==1,"Choose B",GUI.skin.button);
   if(pickA&&manualBoneEndpoint!=0)manualBoneEndpoint=0;
   else if(pickB&&manualBoneEndpoint!=1)manualBoneEndpoint=1;
   GUILayout.EndHorizontal();
   if(GUILayout.Button("Restore suggested endpoints"))SuggestEndpoints();
   GUILayout.Label("Filter bones");filter=GUILayout.TextField(filter);
   scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(200));
   foreach(Transform bone in candidates){
    if(bone==null||bone.name.IndexOf(filter,StringComparison.OrdinalIgnoreCase)<0)continue;
    string prefix=bone==selectedA?"A: ":bone==selectedB?"B: ":"";
    if(GUILayout.Button(prefix+bone.name,leftBoneButton)){
     if(manualBoneEndpoint==0){selectedA=bone;manualBoneEndpoint=1;}else selectedB=bone;
    }
   }
   GUILayout.EndScrollView();
  }

  void DrawSimplePhysics(Rope rope){
   GUILayout.Space(8);GUILayout.Label("Selected rope: "+rope.Item.treeNodeObject.textName);
   if(rope.UseBake&&rope.Bake!=null)GUILayout.Label("Saved baked motion");
   bool paused=GUILayout.Toggle(rope.Settings.Paused,"Pause physics");
   if(paused!=rope.Settings.Paused){rope.Settings.Paused=paused;if(!paused)rope.NeedsReset=true;}
   double oldLength=rope.Settings.Length;
   rope.Settings.Length=Slider("Length",rope.Settings.Length,.25f,4f);
   rope.Settings.Gravity=Slider("Gravity",rope.Settings.Gravity,-30,30);
   rope.Settings.Damping=Slider("Damping",rope.Settings.Damping,0,20);
   rope.Settings.Bend=Slider("Bend stiffness",rope.Settings.Bend,0,1);
   rope.Settings.Iterations=(int)Slider("Quality",rope.Settings.Iterations,4,64);
   if(Math.Abs(oldLength-rope.Settings.Length)>.0001)rope.NeedsReset=true;
   GUILayout.Label("Handles: GIN Rope A / GIN Rope B");
   GUILayout.Label("Use NodesConstraints to link each handle's Position to a target.");
   GUILayout.BeginHorizontal();
   if(GUILayout.Button("Select A"))Select(rope.A);
   if(GUILayout.Button("Select B"))Select(rope.B);
   if(GUILayout.Button("Reset motion"))rope.NeedsReset=true;
   GUILayout.EndHorizontal();
   if(GUILayout.Button("Remove rope (restore item; keep handles)")){
    rope.Restore();Ropes.Remove(rope);active=null;status="Rope removed. Handles remain for your constraints.";
   }
  }
 }
}
