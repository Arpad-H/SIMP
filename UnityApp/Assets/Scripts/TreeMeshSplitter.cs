using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class TreeMeshSplitter
{
    const int trunkSubmeshIndex = 0; // trunk/branch material slot (matches WoodColliderBaker)
    const string outputFolder = "Assets/TreeMeshes";

    [MenuItem("Tools/Split Tree Mesh (Selected)")]
    static void SplitSelected()
    {
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        int pieces = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            // handle the object and all its children (covers prefab variants / nested meshes)
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = mf.sharedMesh;
                if (source == null) continue;
                if (source.subMeshCount < 2) continue; // nothing to split

                MeshRenderer srcRenderer = mf.GetComponent<MeshRenderer>();
                Material[] srcMaterials = srcRenderer != null ? srcRenderer.sharedMaterials : null;

                for (int s = 0; s < source.subMeshCount; s++)
                {
                    Mesh piece = ExtractSubmesh(source, s);

                    Material mat = (srcMaterials != null && s < srcMaterials.Length) ? srcMaterials[s] : null;
                    string suffix = mat != null ? mat.name : $"Submesh_{s}";
                    piece.name = $"{source.name}_{suffix}";

                    string path = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{piece.name}.asset");
                    AssetDatabase.CreateAsset(piece, path);

                    // child GameObject carrying this piece, in the source mesh's local space
                    GameObject child = new GameObject(piece.name);
                    Undo.RegisterCreatedObjectUndo(child, "Split Tree Mesh");
                    child.transform.SetParent(mf.transform, false);

                    child.AddComponent<MeshFilter>().sharedMesh = piece;
                    MeshRenderer childRenderer = child.AddComponent<MeshRenderer>();
                    if (mat != null) childRenderer.sharedMaterial = mat;

                    if (s == trunkSubmeshIndex)
                    {
                        MeshCollider mc = child.AddComponent<MeshCollider>();
                        mc.sharedMesh = piece;
                        mc.convex = false; // static trees; flip to true only if needed
                    }

                    pieces++;
                }

                // stop the original combined mesh from double-rendering on top of the pieces
                if (srcRenderer != null)
                {
                    Undo.RecordObject(srcRenderer, "Split Tree Mesh");
                    srcRenderer.enabled = false;
                }
                EditorUtility.SetDirty(mf.gameObject);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Split into {pieces} mesh piece(s) under {outputFolder}");
    }

    // Builds a new mesh from one submesh, keeping only the vertices it references
    // and carrying normals / uv / tangents / colors along with them.
    static Mesh ExtractSubmesh(Mesh source, int submesh)
    {
        int[] srcTris = source.GetTriangles(submesh);

        var oldToNew = new Dictionary<int, int>(srcTris.Length);
        var newToOld = new List<int>();
        int[] newTris = new int[srcTris.Length];

        for (int i = 0; i < srcTris.Length; i++)
        {
            int oldIdx = srcTris[i];
            if (!oldToNew.TryGetValue(oldIdx, out int newIdx))
            {
                newIdx = newToOld.Count;
                oldToNew[oldIdx] = newIdx;
                newToOld.Add(oldIdx);
            }
            newTris[i] = newIdx;
        }

        Vector3[] srcVerts = source.vertices;
        Vector3[] srcNormals = source.normals;
        Vector2[] srcUV = source.uv;
        Vector4[] srcTangents = source.tangents;
        Color[] srcColors = source.colors;

        int n = newToOld.Count;
        var verts = new Vector3[n];
        var normals = srcNormals.Length == srcVerts.Length ? new Vector3[n] : null;
        var uv = srcUV.Length == srcVerts.Length ? new Vector2[n] : null;
        var tangents = srcTangents.Length == srcVerts.Length ? new Vector4[n] : null;
        var colors = srcColors.Length == srcVerts.Length ? new Color[n] : null;

        for (int i = 0; i < n; i++)
        {
            int old = newToOld[i];
            verts[i] = srcVerts[old];
            if (normals != null) normals[i] = srcNormals[old];
            if (uv != null) uv[i] = srcUV[old];
            if (tangents != null) tangents[i] = srcTangents[old];
            if (colors != null) colors[i] = srcColors[old];
        }

        Mesh mesh = new Mesh();
        if (n > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.vertices = verts;
        if (normals != null) mesh.normals = normals;
        if (uv != null) mesh.uv = uv;
        if (tangents != null) mesh.tangents = tangents;
        if (colors != null) mesh.colors = colors;
        mesh.triangles = newTris;

        if (normals == null) mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
