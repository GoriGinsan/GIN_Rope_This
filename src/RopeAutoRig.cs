using System;
using System.Collections.Generic;
using Studio;
using UnityEngine;
using UObj=UnityEngine.Object;
namespace GINRope {
 // Runtime copies only: the asset mesh, authored rig and original renderers
 // remain available for an exact rollback when this rope is removed.
 public sealed class AutoRig {
  public const string RootName="__GIN_Rope_AutoRig";
  const int MaxVertices=500000,MaxRenderers=64;
  public readonly Transform[] Bones;
  // Legacy axis hint. Saved local endpoints define the actual rest line.
  public readonly bool Vertical;
  public readonly int Intermediate;
  // Item-local rest endpoints, independent of the current simulated pose.
  public readonly Vector3 Start,End;
  public readonly string[] SourcePaths;
  readonly OCIItem owner;
  readonly Transform itemRoot;
  readonly List<Source> sources;
  readonly List<Suppressed> suppressed=new List<Suppressed>();
  readonly bool exactSources;
  readonly Vector3[] restPoints;
  readonly Transform[] skinFrames;
  readonly float[] skinSamples;
  readonly Quaternion[] nodeFrames;
  readonly Matrix4x4[] correctedBindposes;
  readonly Renderer[] originalRendererArray;
  Renderer[] installedRendererArray;
  GameObject generatedRoot;
  bool restored;
  float boundsPadding;
  sealed class Source {
   public string Key;
   public Renderer Original;
   public bool Enabled;
   public Mesh Mesh;
   public Vector3[] World;
   public SkinnedMeshRenderer Generated;
  }
  sealed class Suppressed {public Renderer Renderer;public bool Enabled;}
  AutoRig(OCIItem item,bool vertical,int intermediate,Vector3 start,Vector3 end,List<Source> prepared,bool savedSources){
   owner=item;itemRoot=item.objectItem.transform;originalRendererArray=item.arrayRender;
   Vertical=vertical;Intermediate=intermediate;Start=start;End=end;exactSources=savedSources;
   Bones=new Transform[intermediate+2];sources=prepared;
   skinSamples=SkinSamples(itemRoot.TransformPoint(start),itemRoot.TransformPoint(end),prepared,(Bones.Length-1)*4);
   skinFrames=new Transform[skinSamples.Length];nodeFrames=new Quaternion[Bones.Length];
   restPoints=new Vector3[skinFrames.Length];correctedBindposes=new Matrix4x4[skinFrames.Length];
   for(int i=0;i<skinFrames.Length;i++)restPoints[i]=Vector3.Lerp(start,end,skinSamples[i]);
   SourcePaths=new string[prepared.Count];for(int i=0;i<prepared.Count;i++)SourcePaths[i]=prepared[i].Key;
  }
  static float[] SkinSamples(Vector3 start,Vector3 end,List<Source> prepared,int segments){
   Vector3 axis=end-start;float square=axis.sqrMagnitude;
   SortedDictionary<int,float> samples=new SortedDictionary<int,float>();samples.Add(0,0);samples.Add(1000000,1);
   foreach(Source source in prepared)foreach(Vector3 point in source.World){
    float t=Mathf.Clamp01(Vector3.Dot(point-start,axis)/square);int key=Mathf.RoundToInt(t*1000000);
    if(!samples.ContainsKey(key))samples.Add(key,t);
    if(samples.Count>128){
     // Arbitrary dense meshes use a bounded grid; ordinary cylinders retain
     // their actual cross-section stations as well as every added slice.
     float[] grid=new float[segments+1];for(int i=0;i<grid.Length;i++)grid[i]=(float)i/segments;return grid;
    }
   }
   float[] result=new float[samples.Count];samples.Values.CopyTo(result,0);return result;
  }
  int NearestSkinSample(float t){
   int index=Array.BinarySearch<float>(skinSamples,t);if(index>=0)return index;
   index=~index;if(index==0)return 0;if(index>=skinSamples.Length)return skinSamples.Length-1;
   return t-skinSamples[index-1]<=skinSamples[index]-t?index-1:index;
  }
  internal bool Owns(OCIItem item,Transform[] chain){
   if(restored||item!=owner||!System.Object.ReferenceEquals(chain,Bones)||generatedRoot==null||generatedRoot.transform.parent!=itemRoot)return false;
   foreach(Transform bone in chain)if(bone==null||!bone.IsChildOf(generatedRoot.transform))return false;
   return true;
  }
  public static bool Eligible(OCIItem item){
   if(item==null||item.objectItem==null)return false;
   // FK control count is the user's visible distinction. An item can have an
   // internal rig without offering FK; its current pose can still be baked.
   if(item.listBones==null||item.listBones.Count<=1)return true;
   HashSet<Transform> meaningful=new HashSet<Transform>();
   try{
    foreach(SkinnedMeshRenderer skin in item.objectItem.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
     if(!skin.enabled||!skin.gameObject.activeInHierarchy||skin.sharedMesh==null)continue;
     if(!skin.sharedMesh.isReadable)return false;
     Transform[] bones=skin.bones;
     foreach(BoneWeight weight in skin.sharedMesh.boneWeights){
      AddWeightedBone(meaningful,bones,weight.boneIndex0,weight.weight0);
      AddWeightedBone(meaningful,bones,weight.boneIndex1,weight.weight1);
      AddWeightedBone(meaningful,bones,weight.boneIndex2,weight.weight2);
      AddWeightedBone(meaningful,bones,weight.boneIndex3,weight.weight3);
      if(meaningful.Count>1)return false;
     }
    }
   }catch{return false;}
   return true;
  }
  static void AddWeightedBone(HashSet<Transform> set,Transform[] bones,int index,float weight){
   if(weight>0&&bones!=null&&index>=0&&index<bones.Length&&bones[index]!=null)set.Add(bones[index]);
  }
  public static AutoRig Build(OCIItem item,int intermediates){
   return BuildInternal(item,false,intermediates,false,Vector3.zero,Vector3.zero,null,true);
  }
  public static AutoRig Build(OCIItem item,bool vertical,int intermediates){
   return BuildInternal(item,vertical,intermediates,false,Vector3.zero,Vector3.zero,null,false);
  }
  public static AutoRig Build(OCIItem item,bool vertical,int intermediates,Vector3 localStart,Vector3 localEnd){
   return BuildInternal(item,vertical,intermediates,true,localStart,localEnd,null,false);
  }
  public static AutoRig Build(OCIItem item,bool vertical,int intermediates,Vector3 localStart,Vector3 localEnd,string[] sourcePaths){
   return BuildInternal(item,vertical,intermediates,true,localStart,localEnd,sourcePaths,false);
  }
  static AutoRig BuildInternal(OCIItem item,bool vertical,int intermediates,bool savedLine,Vector3 localStart,Vector3 localEnd,string[] sourcePaths,bool automatic){
   Plugin.ValidateItem(item);
   if(!Eligible(item))throw new ArgumentException("Automatic bones require an item with no FK bones or one FK bone.");
   if(intermediates<3||intermediates>30)throw new ArgumentException("Choose 3 to 30 intermediate bones.");
   Transform root=item.objectItem.transform;
   Plugin.CheckVector(root.position,"Item");Plugin.CheckScale(root.lossyScale);Plugin.CheckRotation(root.rotation);
   if(root.Find(RootName)!=null)throw new InvalidOperationException("This item already has generated Rope This bones.");
   List<Source> prepared=new List<Source>();AutoRig rig=null;
   try{
    HashSet<Transform> foreign=ForeignItemRoots(item);
    HashSet<string> allowed=null;
    if(sourcePaths!=null&&sourcePaths.Length>0){
     if(sourcePaths.Length>MaxRenderers)throw new ArgumentException("Too many saved automatic mesh sources.");
     allowed=new HashSet<string>(StringComparer.Ordinal);
     foreach(string path in sourcePaths)if(String.IsNullOrEmpty(path)||path.Length>1024||!allowed.Add(path))throw new ArgumentException("Invalid saved automatic mesh source.");
    }
    HashSet<Renderer> hiddenCached=new HashSet<Renderer>();
    // Studio hides an item by disabling every entry in its native renderer
    // cache. A saved automatic rig must still rebuild those meshes while the
    // scene is hidden, without treating them as permanently disabled parts.
    if(savedLine&&!item.visible&&item.arrayRender!=null)foreach(Renderer renderer in item.arrayRender)if(renderer!=null)hiddenCached.Add(renderer);
    int total=0;Vector3 min=Vector3.zero,max=Vector3.zero;bool haveVertex=false;
    foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>(true)){
     if(renderer==null||InsideForeignItem(renderer.transform,root,foreign))continue;
     string key=RendererKey(root,renderer);bool selectedBySave=allowed!=null&&allowed.Contains(key);
     if(allowed!=null&&!selectedBySave)continue;
     if((!renderer.enabled&&!hiddenCached.Contains(renderer)&&!selectedBySave)||!renderer.gameObject.activeInHierarchy)continue;
     SkinnedMeshRenderer skin=renderer as SkinnedMeshRenderer;
     MeshRenderer solid=renderer as MeshRenderer;
     Mesh mesh=null;
     if(skin!=null)mesh=skin.sharedMesh;
     else if(solid!=null){MeshFilter filter=solid.GetComponent<MeshFilter>();if(filter!=null)mesh=filter.sharedMesh;}
     else throw new ArgumentException("This item contains a visible renderer that cannot receive automatic bones: "+renderer.GetType().Name+".");
     if(mesh==null||mesh.vertexCount==0)continue;
     if(prepared.Count>=MaxRenderers)throw new ArgumentException("Automatic bones support up to 64 visible mesh renderers.");
     if(mesh.vertexCount>MaxVertices-total)throw new ArgumentException("Automatic bones support up to 500000 vertices per item.");
     if(!mesh.isReadable)throw new ArgumentException("Mesh is not readable; automatic bones cannot safely copy it: "+mesh.name+".");
     Plugin.CheckScale(renderer.transform.lossyScale);Plugin.CheckRotation(renderer.transform.rotation);
     Source source=Prepare(renderer,mesh,root);source.Key=key;if(hiddenCached.Contains(renderer)||selectedBySave)source.Enabled=true;
     prepared.Add(source);total+=mesh.vertexCount;
     foreach(Vector3 position in source.World){
      if(!haveVertex){min=max=position;haveVertex=true;}
      else{min=Vector3.Min(min,position);max=Vector3.Max(max,position);}
     }
    }
    if(allowed!=null&&prepared.Count!=allowed.Count)throw new ArgumentException("Saved automatic mesh sources no longer match this item.");
    if(!haveVertex)throw new ArgumentException("The selected item has no visible readable mesh to rig.");
    Vector3 start,end;
    if(savedLine){
     Plugin.CheckVector(localStart,"Saved automatic start");Plugin.CheckVector(localEnd,"Saved automatic end");
     start=root.TransformPoint(localStart);end=root.TransformPoint(localEnd);
    }else if(automatic){
     FindLongitudinalLine(root,prepared,out start,out end);
     localStart=root.InverseTransformPoint(start);localEnd=root.InverseTransformPoint(end);
    }else{
     Vector3 center=(min+max)*.5f;
     start=center;end=center;
     if(vertical){start.y=max.y;end.y=min.y;}else{start.x=max.x;end.x=min.x;}
     localStart=root.InverseTransformPoint(start);localEnd=root.InverseTransformPoint(end);
    }
    Plugin.CheckVector(start,"Automatic start");Plugin.CheckVector(end,"Automatic end");
    Plugin.CheckVector(localStart,"Automatic local start");Plugin.CheckVector(localEnd,"Automatic local end");
    float length=(end-start).magnitude;
    if(!Settings.Finite(length)||length<.000011f*(intermediates+1))throw new ArgumentException("The item has no usable length for automatic bones.");
    // A sparse cylinder can have only end rings along the selected axis.
    // Split its private surface before weighting so faces follow the chain
    // instead of bridging bends. Source assets remain untouched for removal.
    foreach(Source source in prepared){
     int previous=source.World.Length;
     int limit=MaxVertices-(total-previous);
     source.World=LongitudinalMesh.Subdivide(source.Mesh,source.World,start,end,(intermediates+1)*4,limit);
     total+=source.World.Length-previous;
    }
    rig=new AutoRig(item,vertical,intermediates,localStart,localEnd,prepared,allowed!=null);
    // Every mesh, axis and coordinate has been checked before touching a
    // source renderer. Any later failure also restores all source states.
    rig.Install(start,end);
    return rig;
   }catch{
    if(rig!=null)rig.Restore();else foreach(Source source in prepared)if(source.Mesh!=null)UObj.Destroy(source.Mesh);
    throw;
   }
  }
  static void FindLongitudinalLine(Transform root,List<Source> prepared,out Vector3 start,out Vector3 end){
   // Remove only the item's rigid rotation, retaining the displayed scale and
   // posed geometry. This makes the fitted line follow arbitrary item rotation.
   Vector3 origin=root.position;Quaternion rotation=root.rotation,inverse=Quaternion.Inverse(rotation);
   LongAxis fit=new LongAxis();
   foreach(Source source in prepared)foreach(Vector3 point in source.World){
    Vector3 frame=inverse*(point-origin);fit.Add(new V3(frame.x,frame.y,frame.z));
   }
   // Unity vertices and transforms are floats. Treat nearly equal dimensions
   // as a tie so round-trip rotation noise cannot spin a symmetric item's rig.
   V3 principal=fit.PrincipalDirection(1e-5);
   Vector3 axis=new Vector3((float)principal.X,(float)principal.Y,(float)principal.Z);
   Vector3 reference=Math.Abs(axis.y)<.9f?Vector3.up:Vector3.right;
   Vector3 side=Vector3.Cross(axis,reference).normalized,other=Vector3.Cross(axis,side).normalized;
   V3 average=fit.Center;Vector3 center=new Vector3((float)average.X,(float)average.Y,(float)average.Z);
   Vector3 min=Vector3.one*Single.PositiveInfinity,max=Vector3.one*Single.NegativeInfinity;
   foreach(Source source in prepared)foreach(Vector3 point in source.World){
    Vector3 delta=inverse*(point-origin)-center;
    Vector3 projection=new Vector3(Vector3.Dot(delta,axis),Vector3.Dot(delta,side),Vector3.Dot(delta,other));
    min=Vector3.Min(min,projection);max=Vector3.Max(max,projection);
   }
   // Transverse bounds, rather than vertex density, determine the line's center.
   center+=side*((min.y+max.y)*.5f)+other*((min.z+max.z)*.5f);
   start=origin+rotation*(center+axis*max.x);
   end=origin+rotation*(center+axis*min.x);
  }
  static HashSet<Transform> ForeignItemRoots(OCIItem owner){
   HashSet<Transform> roots=new HashSet<Transform>();
   if(Singleton<Studio.Studio>.Instance==null)return roots;
   foreach(ObjectCtrlInfo info in Singleton<Studio.Studio>.Instance.dicObjectCtrl.Values){
    OCIItem other=info as OCIItem;
    if(other!=null&&other!=owner&&other.objectItem!=null&&other.objectItem.transform.IsChildOf(owner.objectItem.transform))roots.Add(other.objectItem.transform);
   }
   return roots;
  }
  static bool InsideForeignItem(Transform node,Transform ownRoot,HashSet<Transform> foreign){
   while(node!=null&&node!=ownRoot){if(foreign.Contains(node))return true;node=node.parent;}return false;
  }
  static string RendererKey(Transform root,Renderer renderer){
   Renderer[] components=renderer.GetComponents<Renderer>();
   for(int i=0;i<components.Length;i++)if(components[i]==renderer)return Plugin.PathOf(root,renderer.transform)+"|"+i;
   throw new ArgumentException("The selected renderer is no longer attached to its item.");
  }
  static Source Prepare(Renderer renderer,Mesh original,Transform root){
   Mesh copy=null;
   try{
    copy=UObj.Instantiate(original) as Mesh;
    if(copy==null)throw new InvalidOperationException("Could not copy the selected item's mesh.");
    copy.name="GIN Rope runtime: "+original.name;
    SkinnedMeshRenderer skin=renderer as SkinnedMeshRenderer;
#if KKS
    if(skin!=null){
     // KKS meshes may use the newer variable-length weight format. Refuse
     // unsupported influences before converting through the four-weight API.
     var counts=original.GetBonesPerVertex();
     for(int i=0;i<counts.Length;i++)if(counts[i]>4)
      throw new ArgumentException("Automatic bones support up to four skin influences per vertex.");
    }
#endif
    Vector3[] vertices=original.vertices;
    if(vertices.Length!=original.vertexCount)throw new ArgumentException("The selected mesh has inconsistent vertex data.");
    Vector3[] normals=original.normals;Vector4[] tangents=original.tangents;
    if(normals.Length!=0&&normals.Length!=vertices.Length||tangents.Length!=0&&tangents.Length!=vertices.Length)
     throw new ArgumentException("The selected mesh has inconsistent surface data.");
    if(skin!=null)ApplyBlendShapes(skin,original,vertices,normals,tangents);
    Matrix4x4 toItem=root.worldToLocalMatrix*renderer.localToWorldMatrix,normalMatrix=toItem.inverse.transpose;
    PoseMatrix[] matrices=null;BoneWeight[] weights=null;int influences=4;
    if(skin!=null){
     Transform[] bones=skin.bones;Matrix4x4[] bindposes=original.bindposes;weights=original.boneWeights;
     if(weights.Length!=0&&weights.Length!=vertices.Length)throw new ArgumentException("The selected mesh has inconsistent skin weights.");
     if(bones.Length>4096)throw new ArgumentException("The selected mesh has too many internal skin bones.");
     if(bones.Length>0&&weights.Length>0){
      matrices=new PoseMatrix[bones.Length];
      for(int i=0;i<bones.Length;i++)if(bones[i]!=null&&i<bindposes.Length){
       Matrix4x4 matrix=root.worldToLocalMatrix*bones[i].localToWorldMatrix*bindposes[i];
       matrices[i]=new PoseMatrix{Matrix=matrix,Normal=matrix.inverse.transpose,Valid=true};
      }
      influences=(int)skin.quality;
#if KKS
      if(influences==0)influences=(int)QualitySettings.skinWeights;
#else
      if(influences==0)influences=(int)QualitySettings.blendWeights;
#endif
      if(influences!=1&&influences!=2&&influences!=4)influences=4;
     }
    }
    Vector3[] world=new Vector3[vertices.Length];
    float handedness=Vector3.Dot(toItem.MultiplyVector(Vector3.right),Vector3.Cross(toItem.MultiplyVector(Vector3.up),toItem.MultiplyVector(Vector3.forward)))<0?-1:1;
    for(int i=0;i<vertices.Length;i++){
     Plugin.CheckVector(vertices[i],"Mesh vertex");
     Vector3 normal=normals.Length==0?Vector3.zero:normals[i];Plugin.CheckVector(normal,"Mesh normal");
     Vector4 tangent=tangents.Length==0?Vector4.zero:tangents[i];Vector3 direction=new Vector3(tangent.x,tangent.y,tangent.z);
     Plugin.CheckVector(direction,"Mesh tangent");if(!Settings.Finite(tangent.w))throw new ArgumentException("The selected mesh has an invalid tangent.");
     Vector3 point,posedNormal,posedTangent;float tangentSign=handedness;
     if(matrices!=null){
      // Direct bind-pose skinning avoids Unity 5.6 BakeMesh's implicit scale,
      // which would otherwise apply the renderer's lossy scale twice.
      point=Vector3.zero;posedNormal=Vector3.zero;posedTangent=Vector3.zero;
      Vector3 right=Vector3.zero,up=Vector3.zero,forward=Vector3.zero;float sum=0;BoneWeight weight=weights[i];
      Accumulate(matrices,weight.boneIndex0,weight.weight0,vertices[i],normal,direction,ref point,ref posedNormal,ref posedTangent,ref right,ref up,ref forward,ref sum);
      if(influences>=2)Accumulate(matrices,weight.boneIndex1,weight.weight1,vertices[i],normal,direction,ref point,ref posedNormal,ref posedTangent,ref right,ref up,ref forward,ref sum);
      if(influences>=4){
       Accumulate(matrices,weight.boneIndex2,weight.weight2,vertices[i],normal,direction,ref point,ref posedNormal,ref posedTangent,ref right,ref up,ref forward,ref sum);
       Accumulate(matrices,weight.boneIndex3,weight.weight3,vertices[i],normal,direction,ref point,ref posedNormal,ref posedTangent,ref right,ref up,ref forward,ref sum);
      }
      if(sum<.000001f)throw new ArgumentException("The selected mesh contains an unweighted vertex.");
      point/=sum;posedNormal/=sum;posedTangent/=sum;
      tangentSign=Vector3.Dot(right,Vector3.Cross(up,forward))<0?-1:1;
     }else{point=toItem.MultiplyPoint3x4(vertices[i]);posedNormal=normalMatrix.MultiplyVector(normal);posedTangent=toItem.MultiplyVector(direction);}
     vertices[i]=point;Plugin.CheckVector(point,"Mesh local vertex");
     world[i]=root.TransformPoint(point);Plugin.CheckVector(world[i],"Mesh world vertex");
     if(normals.Length!=0){normals[i]=posedNormal.normalized;Plugin.CheckVector(normals[i],"Generated normal");}
     if(tangents.Length!=0){posedTangent.Normalize();Plugin.CheckVector(posedTangent,"Generated tangent");tangents[i]=new Vector4(posedTangent.x,posedTangent.y,posedTangent.z,tangent.w*tangentSign);}
    }
    // Instantiate retains topology, all UV channels, colors and blend-shape
    // data. A skinned source's current shape/rig pose becomes this copy's base;
    // generated shape weights stay zero. The authored renderer is untouched.
    copy.vertices=vertices;if(normals.Length!=0)copy.normals=normals;if(tangents.Length!=0)copy.tangents=tangents;
    copy.RecalculateBounds();
    return new Source{Original=renderer,Enabled=renderer.enabled,Mesh=copy,World=world};
   }catch{if(copy!=null)UObj.Destroy(copy);throw;}
  }
  struct PoseMatrix {public Matrix4x4 Matrix,Normal;public bool Valid;}
  static void Accumulate(PoseMatrix[] poses,int index,float weight,Vector3 vertex,Vector3 normal,Vector3 tangent,ref Vector3 point,ref Vector3 posedNormal,ref Vector3 posedTangent,ref Vector3 right,ref Vector3 up,ref Vector3 forward,ref float sum){
   if(!Settings.Finite(weight)||weight<0||weight>1)throw new ArgumentException("The selected mesh has an invalid skin weight.");
   if(weight==0)return;
   if(index<0||index>=poses.Length||!poses[index].Valid)throw new ArgumentException("The selected mesh references a missing skin bone or bind pose.");
   Matrix4x4 matrix=poses[index].Matrix;
   point+=matrix.MultiplyPoint3x4(vertex)*weight;posedNormal+=poses[index].Normal.MultiplyVector(normal)*weight;posedTangent+=matrix.MultiplyVector(tangent)*weight;
   right+=matrix.MultiplyVector(Vector3.right)*weight;up+=matrix.MultiplyVector(Vector3.up)*weight;forward+=matrix.MultiplyVector(Vector3.forward)*weight;sum+=weight;
  }
  static void ApplyBlendShapes(SkinnedMeshRenderer skin,Mesh mesh,Vector3[] vertices,Vector3[] normals,Vector4[] tangents){
   Vector3[] delta=null,deltaNormal=null,deltaTangent=null;long work=0;
   for(int shape=0;shape<mesh.blendShapeCount;shape++){
    float weight=skin.GetBlendShapeWeight(shape);
    if(!Settings.Finite(weight)||Math.Abs(weight)>1000000)throw new ArgumentException("The selected mesh has an invalid blend-shape weight.");
    if(weight==0)continue;
    int frames=mesh.GetBlendShapeFrameCount(shape);if(frames==0)continue;
    List<float> knots=new List<float>();List<int> indices=new List<int>();bool zero=false;float previous=Single.NegativeInfinity;
    for(int frame=0;frame<frames;frame++){
     float knot=mesh.GetBlendShapeFrameWeight(shape,frame);
     if(!Settings.Finite(knot)||knot<=previous)throw new ArgumentException("The selected mesh has invalid blend-shape frames.");
     knots.Add(knot);indices.Add(frame);if(knot==0)zero=true;previous=knot;
    }
    if(!zero){int at=0;while(at<knots.Count&&knots[at]<0)at++;knots.Insert(at,0);indices.Insert(at,-1);}
    if(knots.Count<2)continue;
    int left=0;while(left<knots.Count-2&&weight>knots[left+1])left++;
    float amount=(weight-knots[left])/(knots[left+1]-knots[left]);
    if(delta==null){delta=new Vector3[vertices.Length];deltaNormal=new Vector3[vertices.Length];deltaTangent=new Vector3[vertices.Length];}
    for(int side=0;side<2;side++){
     int frame=indices[left+side];float factor=side==0?1-amount:amount;if(frame<0||factor==0)continue;
     work+=vertices.Length;if(work>10000000)throw new ArgumentException("The selected mesh has too much active blend-shape data for safe automatic bones.");
     mesh.GetBlendShapeFrameVertices(shape,frame,delta,deltaNormal,deltaTangent);
     for(int i=0;i<vertices.Length;i++){
      vertices[i]+=delta[i]*factor;if(normals.Length!=0)normals[i]+=deltaNormal[i]*factor;
      if(tangents.Length!=0){Vector4 tangent=tangents[i];Vector3 change=deltaTangent[i]*factor;tangents[i]=new Vector4(tangent.x+change.x,tangent.y+change.y,tangent.z+change.z,tangent.w);}
     }
    }
   }
  }
  void Install(Vector3 start,Vector3 end){
   generatedRoot=new GameObject(RootName);generatedRoot.SetActive(false);
   generatedRoot.transform.SetParent(itemRoot,false);
   generatedRoot.transform.localPosition=Vector3.zero;generatedRoot.transform.localRotation=Quaternion.identity;generatedRoot.transform.localScale=Vector3.one;
   Transform parent=generatedRoot.transform;
   for(int i=0;i<Bones.Length;i++){
    GameObject node=new GameObject("Node"+i.ToString("000"));node.layer=itemRoot.gameObject.layer;
    Bones[i]=node.transform;Bones[i].SetParent(parent,false);Bones[i].position=Vector3.Lerp(start,end,(float)i/(Bones.Length-1));
    Bones[i].localRotation=Quaternion.identity;Bones[i].localScale=Vector3.one;parent=Bones[i];
   }
   // Rendering frames sample the physics chain without adding simulated nodes.
   // Rigid per-frame weights avoid the volume collapse of blended rotations.
   for(int i=0;i<skinFrames.Length;i++){
    Transform frame=new GameObject("SkinFrame"+i.ToString("000")).transform;frame.gameObject.layer=itemRoot.gameObject.layer;
    frame.SetParent(generatedRoot.transform,false);frame.position=Vector3.Lerp(start,end,skinSamples[i]);
    frame.localRotation=Quaternion.identity;frame.localScale=Vector3.one;skinFrames[i]=frame;
   }
   Vector3 axis=end-start;float square=axis.sqrMagnitude;
   Vector3 localAxis=End-Start;float localSquare=localAxis.sqrMagnitude,localSpacing=localAxis.magnitude/(Bones.Length-1);
   foreach(Source source in sources){
    GameObject obj=new GameObject("Mesh"+sources.IndexOf(source).ToString("000"));obj.layer=source.Original.gameObject.layer;
    obj.transform.SetParent(generatedRoot.transform,false);
    SkinnedMeshRenderer target=obj.AddComponent<SkinnedMeshRenderer>();source.Generated=target;
    BoneWeight[] weights=new BoneWeight[source.World.Length];
    for(int i=0;i<weights.Length;i++){
     float t=Mathf.Clamp01(Vector3.Dot(source.World[i]-start,axis)/square);
     Vector3 localVertex=itemRoot.InverseTransformPoint(source.World[i]);
     float projection=Mathf.Clamp01(Vector3.Dot(localVertex-Start,localAxis)/localSquare);
     float offset=(localVertex-(Start+localAxis*projection)).magnitude;
     boundsPadding=Mathf.Max(boundsPadding,offset+localSpacing);
     weights[i].boneIndex0=NearestSkinSample(t);weights[i].weight0=1;
    }
    source.World=null;
    Matrix4x4[] bindposes=new Matrix4x4[skinFrames.Length];
    for(int i=0;i<skinFrames.Length;i++)bindposes[i]=skinFrames[i].worldToLocalMatrix*obj.transform.localToWorldMatrix;
    source.Mesh.boneWeights=weights;source.Mesh.bindposes=bindposes;
    target.sharedMesh=source.Mesh;target.bones=skinFrames;target.rootBone=skinFrames[0];target.quality=SkinQuality.Bone1;
    CopyRendererSettings(source.Original,target);target.enabled=source.Enabled&&owner.visible;
    boundsPadding=Mathf.Max(boundsPadding,source.Mesh.bounds.extents.magnitude);
   }
   // OCIItem.visible operates on this cached array rather than activating the
   // item root. Replace entries instead of adding skins beside the originals,
   // so hide/show cannot reveal both renderers or leave a hidden rope visible.
   List<Renderer> nativeRenderers=new List<Renderer>();
   HashSet<Transform> foreign=ForeignItemRoots(owner);
   if(originalRendererArray!=null)foreach(Renderer renderer in originalRendererArray){
    if(renderer==null)continue;
    Renderer replacement=renderer;
    bool replaced=false;
    foreach(Source source in sources)if(renderer==source.Original){replacement=source.Generated;replaced=true;break;}
    bool owned=renderer.transform.IsChildOf(itemRoot)&&!InsideForeignItem(renderer.transform,itemRoot,foreign);
    if(!replaced&&owned&&(renderer is MeshRenderer||renderer is SkinnedMeshRenderer)){
     // An exact persisted source list also excludes authored disabled parts
     // that native scene loading may have indiscriminately enabled again.
     suppressed.Add(new Suppressed{Renderer=renderer,Enabled=exactSources&&renderer.gameObject.activeInHierarchy?false:renderer.enabled});continue;
    }
    // An authored disabled renderer was not converted. Keep it out of the
    // active visibility cache, otherwise a hide/show round trip would enable
    // the unconverted geometry beside the generated skin. The original array
    // (including these entries) is retained intact for removal.
    if(!replaced&&!renderer.enabled&&owned)continue;
    nativeRenderers.Add(replacement);
   }
   foreach(Source source in sources)if(!nativeRenderers.Contains(source.Generated))nativeRenderers.Add(source.Generated);
   installedRendererArray=nativeRenderers.ToArray();owner.arrayRender=installedRendererArray;
   foreach(Suppressed renderer in suppressed)if(renderer.Renderer!=null)renderer.Renderer.enabled=false;
   foreach(Source source in sources)source.Original.enabled=false;
   generatedRoot.SetActive(true);
   // Unity 5.6 can crash in updateWhenOffscreen on a newly created inactive
   // skin. The solver already updates bones while culled; current manual
   // bounds make that optional native setting unnecessary.
   UpdateBounds();
  }
  static void CopyRendererSettings(Renderer source,Renderer target){
   target.sharedMaterials=source.sharedMaterials;target.shadowCastingMode=source.shadowCastingMode;target.receiveShadows=source.receiveShadows;
   target.lightProbeUsage=source.lightProbeUsage;target.reflectionProbeUsage=source.reflectionProbeUsage;
   target.probeAnchor=source.probeAnchor;target.lightProbeProxyVolumeOverride=source.lightProbeProxyVolumeOverride;
   target.motionVectorGenerationMode=source.motionVectorGenerationMode;target.sortingLayerID=source.sortingLayerID;target.sortingOrder=source.sortingOrder;
   target.lightmapIndex=source.lightmapIndex;target.lightmapScaleOffset=source.lightmapScaleOffset;
   target.realtimeLightmapIndex=source.realtimeLightmapIndex;target.realtimeLightmapScaleOffset=source.realtimeLightmapScaleOffset;
   MaterialPropertyBlock block=new MaterialPropertyBlock();source.GetPropertyBlock(block);target.SetPropertyBlock(block);
  }
  // Call after the simulation moves the bones so a long animated rope cannot
  // disappear because of the mesh's original static culling box.
  public void UpdateBounds(){
   if(restored||itemRoot==null||generatedRoot==null||!generatedRoot.activeInHierarchy||Bones.Length==0||Bones[0]==null)return;
   bool anyVisible=false;foreach(Source source in sources)if(source.Generated!=null&&source.Generated.enabled){anyVisible=true;break;}
   if(!anyVisible)return;
   UpdateSkin();
   Bounds bounds=new Bounds(itemRoot.InverseTransformPoint(Bones[0].position),Vector3.zero);
   foreach(Transform bone in Bones){if(bone==null)return;Vector3 point=itemRoot.InverseTransformPoint(bone.position);Plugin.CheckVector(point,"Automatic bounds");bounds.Encapsulate(point);}
   Matrix4x4 linear=itemRoot.localToWorldMatrix,inverse=itemRoot.worldToLocalMatrix;double norm=0,inverseNorm=0;
   for(int row=0;row<3;row++)for(int column=0;column<3;column++){double a=linear[row,column],b=inverse[row,column];norm+=a*a;inverseNorm+=b*b;}
   // World-space rigid rotations can enlarge a cross-section when expressed
   // back in the item's nonuniformly scaled local axes. This matrix-norm
   // bound contains every such rotation without touching mesh vertices.
   double expansion=Math.Max(.01,boundsPadding*2.0*Math.Max(1,Math.Sqrt(norm*inverseNorm)));
   if(!Settings.Finite(expansion)||expansion>1.0e18)throw new ArgumentException("Automatic rope bounds became too large.");
   bounds.Expand((float)expansion);
   foreach(Source source in sources)if(source.Generated!=null&&source.Generated.enabled)source.Generated.localBounds=bounds;
  }
  void UpdateSkin(){
   // A rotated bone beneath a nonuniformly scaled item inherits shear. Static
   // bind poses would turn that shear into an exaggerated cross-section when
   // the rope bends. Cancel the inherited matrix and apply a rigid world-space
   // rotation delta to the item's already scaled original geometry instead.
   Matrix4x4 linear=itemRoot.localToWorldMatrix;linear.m03=0;linear.m13=0;linear.m23=0;
   Vector3 previous=(itemRoot.TransformPoint(End)-itemRoot.TransformPoint(Start)).normalized;
   Vector3 reference=itemRoot.rotation*Vector3.up;
   if(Math.Abs(Vector3.Dot(previous,reference))>.9f)reference=itemRoot.rotation*Vector3.right;
   Vector3 normal=Vector3.Cross(previous,reference).normalized;
   Quaternion transported=Quaternion.identity;
   for(int i=0;i<Bones.Length;i++){
    if(Bones[i]==null)throw new ArgumentException("An automatic rope bone is missing.");
    Plugin.CheckVector(Bones[i].position,"Automatic bone");
    Vector3 incoming=i==0?Vector3.zero:(Bones[i].position-Bones[i-1].position).normalized;
    Vector3 outgoing=i==Bones.Length-1?Vector3.zero:(Bones[i+1].position-Bones[i].position).normalized;
    Vector3 tangent=incoming+outgoing;
    if(tangent.sqrMagnitude<1e-10f)tangent=outgoing.sqrMagnitude>1e-10f?outgoing:previous;
    tangent.Normalize();Quaternion turn;
    if(Vector3.Dot(previous,tangent)<-.9999f){
     Quaternion half=Quaternion.AngleAxis(180,transported*normal);
     turn=Quaternion.FromToRotation(-previous,tangent)*half;
    }else turn=Quaternion.FromToRotation(previous,tangent);
    transported=turn*transported;
    nodeFrames[i]=transported;previous=tangent;
   }
   for(int i=0;i<skinFrames.Length;i++){
    float t=skinSamples[i]*(Bones.Length-1);int first=Math.Min((int)t,Bones.Length-2);float fraction=t-first;
    Vector3 position=Vector3.Lerp(Bones[first].position,Bones[first+1].position,fraction);
    Quaternion delta=Quaternion.Slerp(nodeFrames[first],nodeFrames[first+1],fraction);
    Transform frame=skinFrames[i];if(frame==null)throw new ArgumentException("An automatic skin frame is missing.");
    frame.position=position;frame.rotation=delta*itemRoot.rotation;
    Plugin.CheckScale(frame.lossyScale);Plugin.CheckVector(position,"Automatic skin frame");Plugin.CheckRotation(frame.rotation);
    Matrix4x4 desired=Matrix4x4.TRS(position,delta,Vector3.one)*linear*Matrix4x4.TRS(-restPoints[i],Quaternion.identity,Vector3.one);
    Matrix4x4 bind=frame.worldToLocalMatrix*desired;
    for(int row=0;row<4;row++)for(int column=0;column<4;column++)if(!Settings.Finite(bind[row,column])||Math.Abs(bind[row,column])>1000000000000)
     throw new ArgumentException("The automatic rope skin has an invalid transform.");
    correctedBindposes[i]=bind;
   }
   foreach(Source source in sources)if(source.Mesh!=null)source.Mesh.bindposes=correctedBindposes;
  }
  public void Restore(){
   if(restored)return;restored=true;
   if(generatedRoot!=null){generatedRoot.SetActive(false);generatedRoot.name=RootName+"_Disposed";UObj.Destroy(generatedRoot);generatedRoot=null;}
   RestoreRendererArray();
   foreach(Suppressed renderer in suppressed)if(renderer.Renderer!=null)renderer.Renderer.enabled=renderer.Enabled&&owner.visible;
   foreach(Source source in sources){if(source.Original!=null)source.Original.enabled=source.Enabled&&owner.visible;if(source.Mesh!=null){UObj.Destroy(source.Mesh);source.Mesh=null;}}
  }
  void RestoreRendererArray(){
   if(installedRendererArray==null)return;
   if(System.Object.ReferenceEquals(owner.arrayRender,installedRendererArray))owner.arrayRender=originalRendererArray;
   else{
    // Keep unrelated additions/replacements made by another plugin while this
    // rope was active; exchange only our own cached renderer entries.
    List<Renderer> merged=new List<Renderer>();
    if(owner.arrayRender!=null)foreach(Renderer renderer in owner.arrayRender){
     Renderer replacement=renderer;
     foreach(Source source in sources)if(source.Generated!=null&&renderer==source.Generated){replacement=source.Original;break;}
     if(!merged.Contains(replacement))merged.Add(replacement);
    }
    if(originalRendererArray!=null)foreach(Renderer original in originalRendererArray){
     foreach(Source source in sources)if(original==source.Original&&!merged.Contains(original)){merged.Add(original);break;}
    }
    owner.arrayRender=merged.ToArray();
   }
   installedRendererArray=null;
  }
 }
}
