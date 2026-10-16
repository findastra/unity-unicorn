// Made by Astra. Prepared with Codex (GPT-6), 2026-10-15.
// Install this file in an Editor folder; install the shader outside that folder.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Astra.LiminalTools
{
    public static class GlassGeometry
    {
        public static Mesh CopyMesh(Mesh input, Matrix4x4 transform, bool faceted)
        {
            if (!input || !input.isReadable) throw new ArgumentException("The source mesh must be readable. Enable Read/Write on the model importer.");
            if (input.triangles.Length > 450000) throw new ArgumentException("Use a source below 150,000 triangles; simplify the supplied model first.");
            var points=input.vertices; var indices=input.triangles;
            var normals=input.normals; var uv=input.uv;
            var normalMatrix=transform.inverse.transpose;
            for (int i=0;i<points.Length;i++) points[i]=transform.MultiplyPoint3x4(points[i]);
            for (int i=0;i<normals.Length;i++) normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;
            if (transform.determinant<0) for(int i=0;i<indices.Length;i+=3) { int t=indices[i+1]; indices[i+1]=indices[i+2]; indices[i+2]=t; }
            var mesh=new Mesh { name="prismatic-mesh", indexFormat=IndexFormat.UInt32 };
            if(faceted)
            {
                var vertices=new Vector3[indices.Length]; var tex=new Vector2[indices.Length]; var triangles=new int[indices.Length];
                for(int i=0;i<indices.Length;i++) { vertices[i]=points[indices[i]]; triangles[i]=i; if(uv.Length==points.Length) tex[i]=uv[indices[i]]; }
                mesh.vertices=vertices; mesh.triangles=triangles; mesh.uv=tex; mesh.RecalculateNormals();
            }
            else { mesh.vertices=points; mesh.triangles=indices; if(uv.Length==points.Length) mesh.uv=uv; if(normals.Length==points.Length) mesh.normals=normals; else mesh.RecalculateNormals(); }
            mesh.RecalculateBounds(); return mesh;
        }

        public static GameObject Animal(Object source,float height,bool faceted,Material material)
        {
            if(!source) throw new ArgumentException("Drag an existing mesh, model or prefab into Source shape.");
            var result=new GameObject("prismatic-animal"); result.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                if(source is Mesh single) Add(result,"shape",CopyMesh(single,Matrix4x4.identity,faceted),material);
                else if(source is GameObject model)
                {
                    var rootInverse=model.transform.worldToLocalMatrix;
                    foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
                        if(filter.sharedMesh) Add(result,filter.name,CopyMesh(filter.sharedMesh,rootInverse*filter.transform.localToWorldMatrix,faceted),material);
                    foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if(!skin.sharedMesh) continue;
                        var baked=new Mesh();
                        try { skin.BakeMesh(baked); Add(result,skin.name,CopyMesh(baked,rootInverse*skin.transform.localToWorldMatrix,faceted),material); }
                        finally { Object.DestroyImmediate(baked); }
                    }
                }
                else throw new ArgumentException("Source must be a Mesh or a model/prefab GameObject.");
                var bounds=BoundsOf(result);
                if(bounds.size.y<0.0001f) throw new ArgumentException("The source needs a non-zero vertical size.");
                float scale=height/bounds.size.y;
                foreach(var filter in result.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh; var points=mesh.vertices;
                    for(int i=0;i<points.Length;i++) points[i]=(points[i]-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))*scale;
                    mesh.vertices=points; mesh.RecalculateBounds();
                }
                return result;
            }
            catch { Dispose(result); throw; }
        }

        public static Bounds BoundsOf(GameObject root)
        {
            var filters=root.GetComponentsInChildren<MeshFilter>();
            if(filters.Length==0) throw new ArgumentException("No mesh geometry was found in the source.");
            var b=new Bounds(); bool first=true;
            foreach(var f in filters) if(f.sharedMesh) foreach(var p in f.sharedMesh.vertices)
            { var v=root.transform.InverseTransformPoint(f.transform.TransformPoint(p)); if(first){b=new Bounds(v,Vector3.zero);first=false;} else b.Encapsulate(v); }
            return b;
        }

        public static GameObject Add(GameObject root,string name,Mesh mesh,Material material)
        {
            var child=new GameObject(name); child.transform.SetParent(root.transform,false);
            child.AddComponent<MeshFilter>().sharedMesh=mesh; child.AddComponent<MeshRenderer>().sharedMaterial=material;
            return child;
        }

        public static Mesh Shell(float radius,float openingDegrees,int rings=28,int segments=64)
        {
            var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var triangles=new List<int>();
            float start=openingDegrees*Mathf.Deg2Rad;
            for(int row=0;row<=rings;row++)
            {
                float theta=Mathf.Lerp(start,Mathf.PI,(float)row/rings);
                for(int col=0;col<=segments;col++)
                {
                    float phi=(float)col/segments*Mathf.PI*2;
                    var n=new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi),Mathf.Sin(theta)*Mathf.Sin(phi),Mathf.Cos(theta));
                    vertices.Add(n*radius); normals.Add(n);
                }
            }
            for(int row=0;row<rings;row++) for(int col=0;col<segments;col++)
            {
                int a=row*(segments+1)+col,b=a+1,d=a+segments+1,e=d+1;
                if(Vector3.Cross(vertices[d]-vertices[a],vertices[b]-vertices[a]).sqrMagnitude>1e-12f)triangles.AddRange(new[]{a,d,b});
                if(Vector3.Cross(vertices[d]-vertices[b],vertices[e]-vertices[b]).sqrMagnitude>1e-12f)triangles.AddRange(new[]{b,d,e});
            }
            var mesh=new Mesh {name="open-bubble-shell"}; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();return mesh;
        }

        public static Mesh Ring(float radius,float thickness,int segments=64,int sides=10)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<=segments;i++) for(int j=0;j<=sides;j++)
            {
                float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/sides;
                vertices.Add(new Vector3(Mathf.Cos(a)*(radius+thickness*Mathf.Cos(b)),Mathf.Sin(a)*(radius+thickness*Mathf.Cos(b)),thickness*Mathf.Sin(b)));
            }
            for(int i=0;i<segments;i++)for(int j=0;j<sides;j++) {int a=i*(sides+1)+j,b=a+1,d=a+sides+1,e=d+1;triangles.AddRange(new[]{a,b,d,b,e,d});}
            var mesh=new Mesh{name="bubble-rim"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        public static Mesh Cushion(Vector3 size)
        {
            // A complete rounded mesh, flattened and lightly tufted at the seat center.
            var mesh=Shell(1,0,20,40); var points=mesh.vertices;
            for(int i=0;i<points.Length;i++)
            {
                var p=points[i]; p=Vector3.Scale(p,size)*0.5f;
                if(p.y>0) p.y-=size.y*0.09f*Mathf.Exp(-(p.x*p.x+p.z*p.z)*8/Mathf.Max(size.x*size.x,0.01f))*Mathf.Clamp01(p.y/(size.y*0.5f));
                points[i]=p;
            }
            mesh.vertices=points;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.name="rounded-cushion";return mesh;
        }

        public static GameObject Chair(float diameter,float opening,float filling,Material shell,Material trim,Material cushion,Object customFill=null)
        {
            float r=diameter*0.5f,center=r+0.23f;
            var root=new GameObject("bubble-chair");root.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var body=Add(root,"open-shell",Shell(r,opening),shell);body.transform.localPosition=new Vector3(0,center,0);
                body.AddComponent<MeshCollider>().sharedMesh=body.GetComponent<MeshFilter>().sharedMesh;
                var rim=Add(root,"front-rim",Ring(r*Mathf.Sin(opening*Mathf.Deg2Rad),0.018f*diameter),trim);
                rim.transform.localPosition=new Vector3(0,center,r*Mathf.Cos(opening*Mathf.Deg2Rad));
                var foot=Add(root,"base-ring",Ring(r*0.43f,0.035f),trim);foot.transform.localRotation=Quaternion.Euler(90,0,0);foot.transform.localPosition=Vector3.up*0.055f;
                var support=Add(root,"support",Cushion(new Vector3(diameter*0.35f,0.25f,diameter*0.35f)),trim);support.transform.localPosition=Vector3.up*0.15f;
                float seatY=center-r*0.55f;
                if(customFill)
                {
                    var custom=Animal(customFill,diameter*filling,false,cushion);custom.name="custom-filling";
                    var bounds=BoundsOf(custom);float maxWidth=diameter*0.72f;
                    float fit=Mathf.Min(1,maxWidth/Mathf.Max(bounds.size.x,bounds.size.z));custom.transform.SetParent(root.transform,false);
                    custom.transform.localScale=Vector3.one*fit;custom.transform.localPosition=new Vector3(0,seatY-diameter*filling*0.5f,-r*0.1f);
                }
                else
                {
                    var bottom=Add(root,"seat-cushion",Cushion(new Vector3(diameter*0.74f,diameter*filling,diameter*0.63f)),cushion);
                    bottom.transform.localPosition=new Vector3(0,seatY,-r*0.08f);
                    var back=Add(root,"back-cushion",Cushion(new Vector3(diameter*0.68f,diameter*0.47f,diameter*0.2f)),cushion);
                    back.transform.localPosition=new Vector3(0,center-r*0.08f,-r*0.57f);back.transform.localRotation=Quaternion.Euler(-12,0,0);
                }
                var seat=new GameObject("seat-anchor-add-vrcstation");seat.transform.SetParent(root.transform,false);seat.transform.localPosition=new Vector3(0,seatY+diameter*filling*0.46f,0);
                var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,seatY,0);collider.size=new Vector3(diameter*0.6f,diameter*filling*0.8f,diameter*0.52f);
                return root;
            }
            catch {Dispose(root);throw;}
        }

        public static void Dispose(GameObject root)
        {
            if(!root)return;
            var unique=new HashSet<Mesh>();foreach(var filter in root.GetComponentsInChildren<MeshFilter>()) if(filter.sharedMesh)unique.Add(filter.sharedMesh);
            Object.DestroyImmediate(root);
            foreach(var mesh in unique)if(mesh&&!AssetDatabase.Contains(mesh))Object.DestroyImmediate(mesh);
        }

        public static string Export(GameObject source,string assetFolder,string stem)
        {
            if(!source)throw new ArgumentException("Build a preview before exporting.");
            if(!AssetDatabase.IsValidFolder(assetFolder))throw new ArgumentException("Choose an existing folder inside Assets.");
            string date=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(Application.platform==RuntimePlatform.WindowsEditor?"Mountain Standard Time":"America/Denver")).ToString("yyyyMMdd-HHmmss");
            string folder=assetFolder+"/"+stem+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+"-"+date;
            AssetDatabase.CreateFolder(assetFolder,Path.GetFileName(folder));
            var clone=Object.Instantiate(source);clone.name=stem;
            try
            {
                foreach(var t in clone.GetComponentsInChildren<Transform>(true))t.gameObject.hideFlags=HideFlags.None;
                var meshes=new Dictionary<Mesh,Mesh>();var materials=new Dictionary<Material,Material>();int meshIndex=0,matIndex=0;
                foreach(var f in clone.GetComponentsInChildren<MeshFilter>())
                {
                    if(!meshes.TryGetValue(f.sharedMesh,out var saved)){saved=Object.Instantiate(f.sharedMesh);saved.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(saved,folder+"/mesh-"+(++meshIndex)+"-"+date+".asset");meshes.Add(f.sharedMesh,saved);}
                    f.sharedMesh=saved;
                }
                foreach(var col in clone.GetComponentsInChildren<MeshCollider>())if(col.sharedMesh&&meshes.TryGetValue(col.sharedMesh,out var saved))col.sharedMesh=saved;
                foreach(var r in clone.GetComponentsInChildren<Renderer>())
                {
                    var sourceMats=r.sharedMaterials;
                    for(int i=0;i<sourceMats.Length;i++)
                    {
                        if(!sourceMats[i])continue;
                        if(!materials.TryGetValue(sourceMats[i],out var saved)){saved=new Material(sourceMats[i]);saved.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(saved,folder+"/material-"+(++matIndex)+"-"+date+".mat");materials.Add(sourceMats[i],saved);}sourceMats[i]=saved;
                    }
                    r.sharedMaterials=sourceMats;
                }
                string path=folder+"/"+stem+"-"+date+".prefab";
                PrefabUtility.SaveAsPrefabAsset(clone,path,out bool success);if(!success)throw new IOException("Unity did not save the prefab.");AssetDatabase.SaveAssets();return path;
            }
            finally{Object.DestroyImmediate(clone);}
        }
    }

    public class LiminalGlassWorkshop : EditorWindow
    {
        int tab; Object shape,customFill;float height=0.3f,diameter=2.2f,opening=62,filling=0.18f,opacity=0.22f,rainbow=1,reflection=1,contrast=2.4f;
        Color tint=new Color(0.7f,0.9f,1),seatColor=new Color(0.53f,0.9f,0.8f),rimColor=new Color(1,0.55f,0.75f);
        bool faceted=true;GameObject root;Material glass,trim,cushion;PreviewRenderUtility preview;Vector2 orbit=new Vector2(25,-12);string status="Drag a source shape, or open Bubble chair to build a chair.";
        [MenuItem("Tools/Unity Unicorn/Glass Animal Tool")]
        public static void OpenAnimal(){var w=GetWindow<LiminalGlassWorkshop>();w.tab=0;w.titleContent=new GUIContent("Prism Workshop");w.minSize=new Vector2(560,720);w.Show();}
        [MenuItem("Tools/Unity Unicorn/Bubble Chair Blower")]
        public static void OpenChair(){var w=GetWindow<LiminalGlassWorkshop>();w.tab=1;w.titleContent=new GUIContent("Prism Workshop");w.minSize=new Vector2(560,720);w.Rebuild();w.Show();}
        void OnDisable(){Clear();}
        void Clear(){GlassGeometry.Dispose(root);root=null;if(preview!=null){preview.Cleanup();preview=null;}foreach(var m in new[]{glass,trim,cushion})if(m)DestroyImmediate(m);glass=trim=cushion=null;}
        public static Material Glass(Color tint,float opacity,float rainbow,float reflection,float contrast)
        {
            var shader=Shader.Find("Astra/Prismatic Glass");if(!shader)throw new InvalidOperationException("Install prismatic-glass-20261015.shader outside the Editor folder.");
            var m=new Material(shader){hideFlags=HideFlags.HideAndDontSave};m.SetColor("_Tint",tint);m.SetFloat("_Opacity",opacity);m.SetFloat("_Rainbow",rainbow);m.SetFloat("_Reflection",reflection);m.SetFloat("_Contrast",contrast);return m;
        }
        void Rebuild()
        {
            Clear();try
            {
                glass=Glass(tint,tab==1?0.09f:opacity,tab==1?0.22f:rainbow,reflection,contrast);
                if(tab==0)root=GlassGeometry.Animal(shape,height,faceted,glass);
                else
                {
                    glass.SetFloat("_Cull",0);trim=new Material(Shader.Find("Standard")){color=rimColor,hideFlags=HideFlags.HideAndDontSave};trim.SetFloat("_Metallic",0.7f);trim.SetFloat("_Glossiness",0.9f);
                    cushion=new Material(Shader.Find("Standard")){color=seatColor,hideFlags=HideFlags.HideAndDontSave};cushion.SetFloat("_Glossiness",0.25f);
                    root=GlassGeometry.Chair(diameter,opening,filling,glass,trim,cushion,customFill);
                }
                preview=new PreviewRenderUtility();preview.AddSingleGO(root);preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(0.055f,0.035f,0.09f);
                preview.ambientColor=new Color(0.32f,0.32f,0.38f);
                preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(35,35,0);preview.lights[1].intensity=0.9f;preview.lights[1].transform.rotation=Quaternion.Euler(340,210,0);
                int triangles=0;foreach(var f in root.GetComponentsInChildren<MeshFilter>())triangles+=f.sharedMesh.triangles.Length/3;
                status=triangles.ToString("N0")+" triangles | Drag the preview to orbit. Export creates new assets.";
            }
            catch(Exception e){status=e.Message;}
            Repaint();
        }
        void OnGUI()
        {
            GUILayout.Label("PRISM WORKSHOP",EditorStyles.boldLabel);
            GUILayout.Label("Prismatic animals and open-front bubble chairs",EditorStyles.wordWrappedLabel);
            int next=GUILayout.Toolbar(tab,new[]{"Glass animal","Bubble chair"});if(next!=tab){tab=next;Rebuild();}
            EditorGUI.BeginChangeCheck();
            if(tab==0)
            {
                shape=EditorGUILayout.ObjectField("Source shape",shape,typeof(Object),true);
                height=EditorGUILayout.Slider("Height (metres)",height,0.03f,5);faceted=EditorGUILayout.Toggle("Flat triangular facets",faceted);
                opacity=EditorGUILayout.Slider("Body opacity",opacity,0.02f,0.9f);rainbow=EditorGUILayout.Slider("Rainbow flashes",rainbow,0,2);contrast=EditorGUILayout.Slider("Facet contrast",contrast,0.2f,6);
            }
            else
            {
                diameter=EditorGUILayout.Slider("Diameter (metres)",diameter,1.4f,4);opening=EditorGUILayout.Slider("Opening angle",opening,50,85);filling=EditorGUILayout.Slider("Filling thickness",filling,0.08f,0.3f);
                customFill=EditorGUILayout.ObjectField("Custom filling model",customFill,typeof(Object),false);seatColor=EditorGUILayout.ColorField("Filling color",seatColor);rimColor=EditorGUILayout.ColorField("Rim color",rimColor);
            }
            tint=EditorGUILayout.ColorField("Glass tint",tint);reflection=EditorGUILayout.Slider("Reflection strength",reflection,0,2);
            bool changed=EditorGUI.EndChangeCheck();
            EditorGUILayout.HelpBox(tab==0?"Uses your supplied geometry. Prismatic style approximation; no original world mesh or shader is included. Skinned inputs become static sculptures.":"Shell has an actual open front and a static mesh collider. Seat anchor is provided; add and test a VRCStation in your VRChat project.",MessageType.Info);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Build / refresh preview"))Rebuild();
                using(new EditorGUI.DisabledScope(!root))if(GUILayout.Button("Export new prefab"))
                {
                    string selected=EditorUtility.OpenFolderPanel("Choose an output folder inside Assets",Application.dataPath,"");
                    if(!string.IsNullOrEmpty(selected))try
                    {string relative=FileUtil.GetProjectRelativePath(selected);status="Saved: "+GlassGeometry.Export(root,relative,tab==0?"prismatic-animal":"bubble-chair");}
                    catch(Exception e){status=e.Message;}
                }
            }
            if(changed&&root)Rebuild();
            EditorGUILayout.LabelField(status,EditorStyles.wordWrappedLabel);
            var rect=GUILayoutUtility.GetRect(200,200,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            if(root&&preview!=null)
            {
                var ev=Event.current;if(ev.type==EventType.MouseDrag&&rect.Contains(ev.mousePosition)){orbit+=ev.delta*0.5f;orbit.y=Mathf.Clamp(orbit.y,-70,70);ev.Use();Repaint();}
                if(ev.type==EventType.Repaint)
                {
                    var bounds=GlassGeometry.BoundsOf(root);float distance=bounds.size.magnitude*1.3f;
                    var rotation=Quaternion.Euler(-orbit.y,orbit.x,0);preview.camera.transform.position=bounds.center+rotation*new Vector3(0,0,distance);preview.camera.transform.LookAt(bounds.center);preview.camera.nearClipPlane=0.005f;preview.camera.farClipPlane=100;preview.camera.fieldOfView=35;
                    preview.BeginPreview(rect,GUIStyle.none);preview.Render();GUI.DrawTexture(rect,preview.EndPreview(),ScaleMode.StretchToFill,false);
                }
            }
        }
    }
}
#endif
