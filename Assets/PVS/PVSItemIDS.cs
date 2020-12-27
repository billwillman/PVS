using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NsLib.PVS {
    public class PVSItemIDS : MonoBehaviour {

        public List<Transform> m_IDs = null;
        public Transform m_BakerRoot = null;
        public bool m_LoadTestData = false;

        private static readonly string _cTestFileName = "PVS.bytes";

        private void Awake() {
            LoadTestData();
        }

        void LoadIDs() {
            if (m_IDs != null) {
                for (int i = 0; i < m_IDs.Count; ++i) {
                    int id = i + 1;
                    Transform target = m_IDs[i];
                    if (target == null)
                        continue;
                    PVSManager.GetInstance().RegisterID(id, target);
                }
            }
        }

        public void LoadFromStream(Stream stream) {
            if (stream == null)
                return;
            PVSManager.GetInstance().Clear();
            LoadIDs();
            PVSManager.GetInstance().LoadFromStream(stream);
        }

        void LoadTestData() {
            if (!m_LoadTestData)
                return;
            // 读取测试数据
            if (!File.Exists(_cTestFileName)) {
                Debug.LogErrorFormat("not found: {0}", _cTestFileName);
                return;
            }
            FileStream stream = new FileStream(_cTestFileName, FileMode.Open, FileAccess.Read);
            try {
                LoadFromStream(stream);
            } finally {
                stream.Dispose();
            }
        }

        public void Clear() {
            if (m_IDs != null)
                m_IDs.Clear();
        }

        public bool IsVaild {
            get {
                return (m_IDs != null) && (m_IDs.Count > 0);
            }
        }

        public void AddIDs(int id, Transform obj) {
            if (m_IDs == null)
                m_IDs = new List<Transform>();
            int srcId = m_IDs.Count;
            if (id - 1 > m_IDs.Count || srcId != id - 1)
                Debug.LogError("PVSItemIDS ID ERROR~!");
            m_IDs.Add(obj);
        }
    }
}
