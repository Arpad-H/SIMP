using UnityEngine;

public class NutSpawnLocation : MonoBehaviour
{
    // Empty marker. Add to each hand-placed empty under the tree.

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.1f);
        Gizmos.DrawRay(transform.position, transform.up * 0.3f);
    }
}