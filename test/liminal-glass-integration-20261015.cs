#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace Astra.LiminalTools
{
    public static class GlassIntegration
    {
        static int checks;
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
        static string Output()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-liminalOutput");
            if(index<0||index+1>=args.Length)throw new Exception("Pass -liminalOutput <private-output-folder>.");
            Directory.CreateDirectory(args[index+1]);return args[index+1];
        }
        static void ValidateMesh(Mesh mesh)
        {
            Check(mesh.vertexCount>0,"Empty generated mesh");var p=mesh.vertices;var t=mesh.triangles;
            Check(p.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z)),"Nonfinite vertex");
            Check(t.All(i=>i>=0&&i<p.Length),"Invalid index");
            for(int i=0;i<t.Length;i+=3)Check(Vector3.Cross(p[t[i+1]]-p[t[i]],p[t[i+2]]-p[t[i]]).sqrMagnitude>1e-13f,"Degenerate triangle");
        }
        public static void Run()
        {
            string output=Output();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var glass=LiminalGlassWorkshop.Glass(new Color(0.72f,0.89f,1),0.24f,1.2f,0.65f,1.9f);
            var tint=LiminalGlassWorkshop.Glass(new Color(0.9f,0.73f,1),0.08f,0.16f,0.4f,2.4f);tint.SetFloat("_Cull",0);
            var trim=new Material(Shader.Find("Standard")){color=new Color(1,0.55f,0.75f)};trim.SetFloat("_Metallic",0.72f);trim.SetFloat("_Glossiness",0.85f);
            var filling=new Material(Shader.Find("Standard")){color=new Color(0.42f,0.87f,0.75f)};filling.SetFloat("_Glossiness",0.2f);
            var input=new Mesh();input.vertices=new[]{Vector3.up,Vector3.right,Vector3.forward,Vector3.left,Vector3.back,Vector3.down};
            input.triangles=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1};input.RecalculateNormals();input.RecalculateBounds();
            var original=input.vertices.ToArray();var animal=GlassGeometry.Animal(input,1.3f,true,glass);
            var b=GlassGeometry.BoundsOf(animal);Check(Mathf.Abs(b.size.y-1.3f)<0.0001f,"Incorrect target height");Check(Mathf.Abs(b.min.y)<0.0001f,"Pivot not on floor");
            Check(input.vertices.SequenceEqual(original),"Source geometry was mutated");Check(animal.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount==24,"Faceted normals do not have split vertices");
            var source=new GameObject("source-fixture");var child=GlassGeometry.Add(source,"mirrored",Object.Instantiate(input),glass);child.transform.localScale=new Vector3(-2,3,1);child.transform.localPosition=new Vector3(1,2,0);
            var transformed=GlassGeometry.Animal(source,0.25f,true,glass);Check(Mathf.Abs(GlassGeometry.BoundsOf(transformed).size.y-0.25f)<0.0001f,"Nested transform normalization failed");GlassGeometry.Dispose(transformed);GlassGeometry.Dispose(source);
            bool rejected=false;try{GlassGeometry.Animal(null,1,true,glass);}catch(ArgumentException){rejected=true;}Check(rejected,"Missing source was not rejected");
            GameObject chair=null;
            foreach(float angle in new[]{50f,62f,85f})
            {
                var candidate=GlassGeometry.Chair(2.2f,angle,0.18f,tint,trim,filling);
                foreach(var f in candidate.GetComponentsInChildren<MeshFilter>())ValidateMesh(f.sharedMesh);
                var collider=candidate.GetComponentInChildren<MeshCollider>();Check(!collider.convex,"Shell collider must retain opening");
                Physics.SyncTransforms();
                bool hit=collider.Raycast(new Ray(new Vector3(0,1.33f,3),Vector3.back),out var info,10);
                Check(!hit||info.distance>3,"Front opening blocked by shell collider");
                Check(candidate.transform.Find("seat-anchor-add-vrcstation")!=null,"No station placement anchor");
                if(angle==62)chair=candidate;else GlassGeometry.Dispose(candidate);
            }
            var custom=GlassGeometry.Chair(1.6f,70,0.25f,tint,trim,filling,input);Check(custom.transform.Find("custom-filling")!=null,"Custom filling input failed");GlassGeometry.Dispose(custom);
            var prefab=GlassGeometry.Export(chair,"Assets","bubble-chair");
            var imported=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);Check(imported!=null,"Prefab missing after export");
            foreach(var f in imported.GetComponentsInChildren<MeshFilter>())Check(AssetDatabase.Contains(f.sharedMesh),"Unsaved mesh dependency");
            foreach(var r in imported.GetComponentsInChildren<Renderer>())Check(AssetDatabase.Contains(r.sharedMaterial),"Unsaved material dependency");
            Check(imported.GetComponentInChildren<MeshCollider>().sharedMesh==imported.transform.Find("open-shell").GetComponent<MeshFilter>().sharedMesh,"Collider does not reference exported mesh");
            var animalPath=GlassGeometry.Export(animal,"Assets","prismatic-test-shape");Check(AssetDatabase.LoadAssetAtPath<GameObject>(animalPath)!=null,"Glass export failed");
            animal.hideFlags=HideFlags.None;chair.hideFlags=HideFlags.None;animal.transform.position=new Vector3(-1.6f,0.35f,0);chair.transform.position=new Vector3(1.15f,0,0);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*2;var floorMat=new Material(Shader.Find("Standard")){color=new Color(0.11f,0.07f,0.17f)};floor.GetComponent<Renderer>().sharedMaterial=floorMat;
            var plinth=GameObject.CreatePrimitive(PrimitiveType.Cylinder);plinth.transform.position=new Vector3(-1.6f,0.17f,0);plinth.transform.localScale=new Vector3(1.3f,0.17f,1.3f);plinth.GetComponent<Renderer>().sharedMaterial=trim;
            var light=new GameObject("key-light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(38,-30,0);
            var fillLight=new GameObject("fill-light").AddComponent<Light>();fillLight.type=LightType.Directional;fillLight.intensity=0.5f;fillLight.color=new Color(0.6f,0.75f,1);fillLight.transform.rotation=Quaternion.Euler(25,150,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(0.25f,0.22f,0.3f);
            var cube=new Cubemap(32,TextureFormat.RGBA32,false);
            for(int face=0;face<6;face++){var colors=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float stripe=Mathf.Pow(Mathf.Max(0,Mathf.Sin((x+face*4)*0.18f)),8);colors[y*32+x]=Color.Lerp(new Color(0.03f,0.015f,0.05f),new Color(0.5f,0.65f,0.8f),stripe);}cube.SetPixels(colors,(CubemapFace)face);}cube.Apply();
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflection=cube;
            var camera=new GameObject("preview-camera").AddComponent<Camera>();camera.transform.position=new Vector3(3.5f,2.7f,6);camera.transform.LookAt(new Vector3(0,1.1f,0));camera.fieldOfView=38;camera.backgroundColor=new Color(0.025f,0.015f,0.045f);camera.clearFlags=CameraClearFlags.SolidColor;
            var target=new RenderTexture(1600,1000,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,"liminal-tools-unity-preview-20261015.png"),image.EncodeToPNG());
            RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(target);Object.DestroyImmediate(image);
            Check(!ShaderUtil.ShaderHasError(glass.shader),"Prismatic shader has compilation errors");
            Check(glass.shader.isSupported,"Prismatic shader is not supported on the test renderer");
            AssetDatabase.ExportPackage(new[]{"Assets/liminal-tools/Editor/liminal-glass-workshop-20261015.cs","Assets/liminal-tools/prismatic-glass-20261015.shader"},Path.Combine(output,"liminal-glass-tools-20261015.unitypackage"),ExportPackageOptions.Default);
            File.WriteAllText(Path.Combine(output,"liminal-tools-validation-20261015.json"),"{\"status\":\"passed\",\"unity\":\""+Application.unityVersion+"\",\"renderer\":\""+SystemInfo.graphicsDeviceType+"\",\"assertions\":"+checks+",\"scope\":\"synthetic geometry, source preservation, opening collision, prefab dependency persistence, shader compile, actual Unity preview render\",\"limits\":\"not a VRChat upload, headset test, original asset match, or VRCStation test\"}");
            Debug.Log("LIMINAL_VALIDATION_PASSED "+checks);
        }
    }
}
#endif
