
    using System;
    using UnityEngine;

    public class Test : MonoBehaviour
    {
        public GameObject testObject;
        public OSCReceiver oscReceiver;

        private void Start()
        {
            if (oscReceiver != null)
            {
                oscReceiver.OnRotation += OnPhoneRotation;
            }
        }

        private void OnPhoneRotation(Quaternion obj)
        {
            if (testObject != null)
            {
                testObject.transform.rotation = obj;
            }
        }
    }
