using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using UnityEngine.XR;



public class PlayerManager : MonoBehaviour
{
    [Header("recenter")] [Tooltip("Center Eye Anchor des OVRCameraRig (Headset-Position)")] [SerializeField]
    public Transform Head;

    [SerializeField] private GameObject[] rayInteractor;
    [SerializeField] private GameObject[] teleportInteractor;
    [SerializeField] private GameObject PlayerHead;
   
    [SerializeField] public OVRCameraRig cameraRig;
    
    
    private OVRManager _ovrManager;

private bool gameOver ;
    private Vignette vignette;
    
   
    private void Start()
    {
        
        //DeactivateTeleportInteractor();
        ActivateTeleportInteractor();
        DeactivateRayInteractor();
        

gameOver = false;
        _ovrManager = GetComponent<OVRManager>();
     

        //_introducingTV = Resources.Load<AudioClip>("Audio/voice/Line1fin.mp3");
        //_audioSource.clip = _introducingTV;
    }
    

 

    private void DeactivateRayInteractor()
    {
        foreach (GameObject gameObject in rayInteractor)
        {
            gameObject.SetActive(false);
        }
    }

    private void ActivateRayInteractor()
    {
        foreach (GameObject gameObject in rayInteractor)
        {
            gameObject.SetActive(true);
        }
    }

    private void DeactivateTeleportInteractor()
    {
        foreach (GameObject gameObject in teleportInteractor)
        {
            gameObject.SetActive(false);
        }
    }

    private void ActivateTeleportInteractor()
    {
        foreach (GameObject gameObject in teleportInteractor)
        {
            gameObject.SetActive(true);
        }
    }

    private void AcivatePassthrough()
    {
        //_ovrManager.isInsightPassthroughEnabled = true;
    }

    private void DeactivatePassthrough()
    {
        // _ovrManager.isInsightPassthroughEnabled = false;
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("GameBorder"))
        {
            Debug.Log($"[PlayerManager]: Entering collision with {other.gameObject.name}");
            AcivatePassthrough();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("GameBorder"))
        {
            Debug.Log($"[PlayerManager]: Exiting collision with {other.gameObject.name}");
            DeactivatePassthrough();
        }
    }

    //
    // /*
    //  * Quelle
    //  * https://www.youtube.com/watch?v=NOCXB_ETKrM
    //  */
    // public void recenterPlayer(Transform target)
    // {
    //     Vector3 offset = Head.position - origin.position;
    //     offset.y = 0; // keep the same height
    //     origin.position = target.position - offset;
    //
    //     // rotate
    //     Vector3 targetForward = target.forward;
    //     targetForward.y = 0;
    //     Vector3 cameraForward = Head.forward;
    //     cameraForward.y = 0;
    //
    //     float angle = Vector3.SignedAngle(cameraForward, targetForward, Vector3.up);
    //
    //     origin.RotateAround(target.position, Vector3.up, angle);
    // }
}