#define REMOTING

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Hololens;
using Base;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine.XR.WSA;
using QRTracking;

    public class CalibrationManagerH : Base.Singleton<CalibrationManagerH>
    {
    //public GameObject qrCodePrefab;
     public GameObject qrCodePrefab;
     public GameObject EditorScenePrefab;

        private System.Collections.Generic.SortedDictionary<System.Guid, GameObject> qrCodesObjectsList;
        private bool clearExisting = false;

        [HideInInspector]
        public bool Calibrated {
            private set;
            get;
        }


        struct ActionData
        {
            public enum Type
            {
                Added,
                Updated,
                Removed
            };
            public Type type;
            public Microsoft.MixedReality.QR.QRCode qrCode;

            public ActionData(Type type, Microsoft.MixedReality.QR.QRCode qRCode) : this()
            {
                this.type = type;
                qrCode = qRCode;
            }
        }

        private System.Collections.Generic.Queue<ActionData> pendingActions = new Queue<ActionData>();
        void Awake()
        {

    #if !UNITY_EDITOR
            /*SETUP QR*/
            GameObject QRInfo = qrCodePrefab.transform.Find("QRInfo").gameObject;
            GameObject Axis = QRInfo.transform.Find("Axis").gameObject;
            GameObject axisY =  Axis.transform.Find("AxisPointerY").gameObject;

            QRInfo.transform.localPosition = new Vector3(0f,0.06f,0f);
            axisY.transform.localEulerAngles = new Vector3(0f,0f,-90f);
    #endif

        }
        void Start()
        {
            Debug.Log("QRCodesVisualizer start");
            qrCodesObjectsList = new SortedDictionary<System.Guid, GameObject>();

            QRCodesManager.Instance.QRCodesTrackingStateChanged += Instance_QRCodesTrackingStateChanged;
            QRCodesManager.Instance.QRCodeAdded += Instance_QRCodeAdded;
            QRCodesManager.Instance.QRCodeUpdated += Instance_QRCodeUpdated;
            QRCodesManager.Instance.QRCodeRemoved += Instance_QRCodeRemoved;

            GameManagerH.Instance.OnCloseScene += SetCalibration;
            GameManagerH.Instance.OnCloseProject += SetCalibration;
            GameManagerH.Instance.OnOpenSceneEditor += StartCalibration;
            GameManagerH.Instance.OnOpenProjectEditor += StartCalibration;
            if (qrCodePrefab == null)
            {
                throw new System.Exception("Prefab not assigned");
            }
        }


         public void StartCalibration(object sender, EventArgs e){
            #if !UNITY_EDITOR || REMOTING
                QRCodesManager.Instance.StartQRTracking();
            #else
           //  GameManagerH.Instance.SceneSetParent( helpPr.transform);
               GameManagerH.Instance.Scene.transform.parent = EditorScenePrefab.transform;
                 GameManagerH.Instance.Scene.transform.localPosition = new Vector3(0f, 0f, 0f);
            #endif

         }
         public void SetCalibration(object sender, EventArgs e){
            Calibrated = false;
            //  isScene = false;
            #if !UNITY_EDITOR || REMOTING
        
            GameManagerH.Instance.SceneSetParent(null);
           
            GameManagerH.Instance.SceneSetActive(false);
            QRCodesManager.Instance.StopQRTracking();
            //  GetComponent<QRCodesManager>().StopQRTracking();
            #endif
          }
        private void Instance_QRCodesTrackingStateChanged(object sender, bool status)
        {
            if (!status)
            {
                clearExisting = true;
            }
        }

        private void Instance_QRCodeAdded(object sender, QRCodeEventArgs<Microsoft.MixedReality.QR.QRCode> e)
        {
            Debug.Log("QRCodesVisualizer Instance_QRCodeAdded");

            lock (pendingActions)
            {
                pendingActions.Enqueue(new ActionData(ActionData.Type.Added, e.Data));
            }
        }

        private void Instance_QRCodeUpdated(object sender, QRCodeEventArgs<Microsoft.MixedReality.QR.QRCode> e)
        {
            Debug.Log("QRCodesVisualizer Instance_QRCodeUpdated");

            lock (pendingActions)
            {
                pendingActions.Enqueue(new ActionData(ActionData.Type.Updated, e.Data));
            }
        }

        private void Instance_QRCodeRemoved(object sender, QRCodeEventArgs<Microsoft.MixedReality.QR.QRCode> e)
        {
            Debug.Log("QRCodesVisualizer Instance_QRCodeRemoved");

            lock (pendingActions)
            {
                pendingActions.Enqueue(new ActionData(ActionData.Type.Removed, e.Data));
            }
        }

        private void HandleEvents()
        {
            lock (pendingActions)
            {
                while (pendingActions.Count > 0)
                {
                    var action = pendingActions.Dequeue();
                    if (action.type == ActionData.Type.Added)
                    {
                        GameObject qrCodeObject = Instantiate(qrCodePrefab, new Vector3(0, 0, 0), Quaternion.identity);               
                        qrCodeObject.GetComponent<SpatialGraphNodeTracker>().Id = action.qrCode.SpatialGraphNodeId;
                        qrCodeObject.GetComponent<QRCode>().qrCode = action.qrCode;
                        qrCodesObjectsList.Add(action.qrCode.Id, qrCodeObject);
                        GameManagerH.Instance.SceneSetParent(qrCodeObject.transform);
                        GameManagerH.Instance.SceneSetActive(true);
                     
                        Calibrated = true;

                    }
                    else if (action.type == ActionData.Type.Updated)
                    {
                        if (!qrCodesObjectsList.ContainsKey(action.qrCode.Id))
                        {
                            GameObject qrCodeObject = Instantiate(qrCodePrefab, new Vector3(0, 0, 0), Quaternion.identity);
                            qrCodeObject.GetComponent<SpatialGraphNodeTracker>().Id = action.qrCode.SpatialGraphNodeId;
                            qrCodeObject.GetComponent<QRCode>().qrCode = action.qrCode;
                            qrCodesObjectsList.Add(action.qrCode.Id, qrCodeObject);
                        }
                    }
                    else if (action.type == ActionData.Type.Removed)
                    {
                        if (qrCodesObjectsList.ContainsKey(action.qrCode.Id))
                        {
                            GameManagerH.Instance.SceneSetParent(null);
                            Destroy(qrCodesObjectsList[action.qrCode.Id]);
                            qrCodesObjectsList.Remove(action.qrCode.Id);
                        }
                    }
                }
            }
            if (clearExisting)
            {
                clearExisting = false;
                GameManagerH.Instance.SceneSetParent(null);
                GameManagerH.Instance.SceneSetActive(false);
                foreach (var obj in qrCodesObjectsList)
                {
                    Destroy(obj.Value);
                }
                qrCodesObjectsList.Clear();

            }
        }

        // Update is called once per frame
        void Update()
        {
            HandleEvents();
        }

}
