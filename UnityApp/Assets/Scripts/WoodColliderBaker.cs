using UnityEngine;
using UnityEditor;
using System.IO;

public class WoodColliderBaker
{
    const int woodSubmeshIndex = 0; // trunk/branch material slot
    const string outputFolder = "Assets/WoodColliders";

    [MenuItem("Tools/Bake Wood Colliders (Selected)")]
    static void BakeSelected()
    {
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        int count = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            // handle the object and all its children (covers prefab variants / nested meshes)
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = mf.sharedMesh;
                if (source == null) continue;
                if (woodSubmeshIndex >= source.subMeshCount) continue;

                Mesh colliderMesh = new Mesh();
                colliderMesh.name = source.name + "_WoodCollider";
                colliderMesh.vertices = source.vertices;
                colliderMesh.SetTriangles(source.GetTriangles(woodSubmeshIndex), 0);
                colliderMesh.RecalculateBounds();

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{outputFolder}/{colliderMesh.name}.asset");
                AssetDatabase.CreateAsset(colliderMesh, path);

                GameObject target = mf.gameObject;
                MeshCollider mc = target.GetComponent<MeshCollider>();
                if (mc == null) mc = target.AddComponent<MeshCollider>();
                mc.sharedMesh = colliderMesh;
                mc.convex = false; // static trees; flip to true only if needed

                EditorUtility.SetDirty(target);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Baked {count} wood collider(s) into {outputFolder}");
    }
}