using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace NsLib.PVS.Test {

    // 摄影机
    [RequireComponent(typeof(Camera))]
    public class PVSCameraTest : MonoBehaviour, IPVSViewer {

        private Action<PVSCell> onChged = null;
        private PVSItemIDS m_Targets = null;

        void Awake() {
            onChged = new Action<PVSCell>(OnViewCellChanged);
            m_Targets = GameObject.FindObjectOfType<PVSItemIDS>();
            InitTestCollisionColors();
        }

        void InitTestCollisionColors() {
            if (m_Targets != null && m_Targets.IsVaild) {
                for (int i = 0; i < m_Targets.m_IDs.Count; ++i) {
                    var iter = m_Targets.m_IDs[i];
                    if (iter != null) {
                        var r = iter.GetComponent<MeshRenderer>();
                        if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null && 
                            string.Compare(r.sharedMaterial.shader.name, "PVS/MaskColor", true) == 0) {
                            r.material.SetColor("_MaskColor", Color.blue);
                        }
                    }
                }
            }
        }


        private void OnViewCellChanged(PVSCell cell) {
            if (m_Targets != null && m_Targets.IsVaild) {
                for (int i = 0; i < m_Targets.m_IDs.Count; ++i) {
                    var tt = m_Targets.m_IDs[i];
                    bool isVisible = cell.IsInstanceIDVisible(tt.GetInstanceID());
                    if (isVisible != tt.gameObject.activeSelf) {
                        tt.gameObject.SetActive(isVisible);
                    }
                }
            }
        }

        // 处理摄影机问题
        private void Update() {
            if (m_Targets != null && m_Targets.IsVaild) {
                PVSManager.GetInstance().UpdateViewer(this, onChged);
            }
        }

        public PVSCell _CurrentCell {
            get;
            set;
        }

        public Vector2 ViewPos {
            get {
                var pt = this.transform.position;
                Vector2 ret = new Vector2(pt.x, pt.z);
                return ret;
            }
        }

    }
}
