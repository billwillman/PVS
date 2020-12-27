using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.Runtime.InteropServices;
using NsLib.PVS.Editor;

namespace NsLib.PVS {

    internal enum PVSSceneBakerMode {
        CpuThreadMode,
        ComputeShaderMode,
		CameraRTComputeMode,
        CameraRTCpuMode
    }

    internal struct  PVSCameraInfo {
        //public bool isOri;
        public float fieldOfView;
        public float near, far;
        public float aspect;
        public Vector3 lookAt;
        public Vector3 position;
        public Matrix4x4 cullMatrix;

        public float GetD(float height) {
            float halfH = height / 2.0f;
            float a = Mathf.Tan(fieldOfView);
            float ret = halfH / a;
            return ret;
        }

        public Vector3 right {
            get {
                Vector3 ret = Vector3.Cross(Vector3.up, lookAt);
                return ret;
            }
        }

        public Vector3 up {
            get {
                Vector3 ret = Vector3.Cross(this.lookAt, this.right);
                return ret;
            }
        }

        /*
        public float GetCamerWidth(bool isOri) {
            if (isOri)
                return this.CameraWidth;
            return this.nearWidth;
        }

        public float GetCameraHeight(bool isOri) {
            if (isOri)
                return this.CameraHeight;
            return this.nearHeight;
        }
        */

        /* 正交 */
        public float CameraHeight {
            get {
                return fieldOfView * 2.0f;
            }
        }

        public float CameraWidth {
            get {
                float ret = this.aspect * CameraHeight;
                return ret;
            }
        }

        /* 透視 */
        public float nearHeight {
            get {
                float halfAngle = fieldOfView / 2.0f * Mathf.PI / 180.0f; // 弧度制
                float ret = 2.0f * Mathf.Tan(halfAngle) * near;
                return ret;
            }
        }

        public float farHeight {
            get {
                float halfAngle = fieldOfView / 2.0f * Mathf.PI / 180.0f; // 弧度制
                float ret = 2.0f * Mathf.Tan(halfAngle) * far;
                return ret;
            }
        }

        public float nearWidth {
            get {
                float ret = aspect * nearHeight;
                return ret;
            }
        }

        public float farWidth {
            get {
                float ret = aspect * farHeight;
                return ret;
            }
        }
    };
       

    public class PVSSceneBaker : EditorWindow {

        private ComputeShader m_PVSCellShader = null;
        private Transform m_PVSMeshRoot = null;
        private Camera m_TargetCamera = null;
        private PVSCameraInfo m_TargetCamInfo;
		private Vector3[] m_CellArray = null;
		private System.Collections.Generic.HashSet<int> m_CellIngoreArray = new System.Collections.Generic.HashSet<int>();
        private static int m_GlobalNodeId = 0;
		private bool m_IsCamOri = false;
		private List<PVSBakerThread> m_BakerThreads = null;
		private PVSBounds[] m_BoundsArray = null;
        private PVSCell[] m_CellsResult = null;
		private PVSCell[] m_CellsSave = null;
        private bool m_CpuThreadRuning = false;
        private PVSSceneBakerMode m_BakerMode = PVSSceneBakerMode.CameraRTCpuMode; // 采用RenderTexture烘焙查
        private int m_SelCellResult = -1;
        private PVSItemIDS m_PVSItemIDS = null;
        private bool m_ShowCell = false;
		private bool m_ShowCombineCell = false;
        public static int GeneratorNodeId() {
            return ++m_GlobalNodeId;
        }

        private Bounds m_SearchBounds;

        [MenuItem("Tools/PVS场景烘焙")]
        public static void Open() {
            Rect r = new Rect((Screen.width - 400) / 2, (Screen.height - 400) / 2, 400, 400);
            PVSSceneBaker wnd = EditorWindow.GetWindowWithRect<PVSSceneBaker>(r, true, "PVS场景烘焙", true);
            wnd.Init();
            wnd.Show();
        }

        void InitCameraInfo() {
            if (m_TargetCamera != null) {
				var lookAt = m_TargetCamInfo.lookAt;
				m_IsCamOri = m_TargetCamera.orthographic;
                if (m_TargetCamera.orthographic) {
                    m_TargetCamInfo = new PVSCameraInfo();
                    m_TargetCamInfo.far = m_TargetCamera.farClipPlane;
                    m_TargetCamInfo.near = m_TargetCamera.nearClipPlane;
                    m_TargetCamInfo.fieldOfView = m_TargetCamera.orthographicSize;
                } else {
                    m_TargetCamInfo = new PVSCameraInfo();
                    m_TargetCamInfo.far = m_TargetCamera.farClipPlane;
                    m_TargetCamInfo.near = m_TargetCamera.nearClipPlane;
                    m_TargetCamInfo.fieldOfView = m_TargetCamera.fieldOfView;
                }
				m_TargetCamInfo.lookAt = lookAt;
                m_TargetCamInfo.aspect = m_TargetCamera.aspect;
                m_TargetCamInfo.position = m_TargetCamera.transform.position;
                m_TargetCamInfo.cullMatrix = m_TargetCamera.cullingMatrix;
            }
        }

        void Init() {

            if (m_PVSCellShader == null) {
                m_PVSCellShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/PVS/Editor/PVSBaker.compute");
            }

            var openScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (openScene.isLoaded && openScene.IsValid()) {
                m_SelectScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(openScene.path);
            }

            if (m_TargetCamera == null) {
                var cam = Camera.main;
                if (cam != null)
                    m_TargetCamera = cam;
                else {
                    var cam1 = Camera.FindObjectOfType<Camera>();
                    m_TargetCamera = cam1;
                }
            }

            InitCameraInfo();
			m_TargetCamInfo.lookAt = new Vector3 (0, 0, 1);



			ProcessCurrentScene (true);

            if (m_PVSItemIDS != null && m_PVSItemIDS.m_BakerRoot != null) {
                m_PVSMeshRoot = m_PVSItemIDS.m_BakerRoot;
            }

            UnityEditor.SceneView.onSceneGUIDelegate += OnSceneGUI;
            Debug.Log("注册SceneView事件成功");

            RefreshCurrSceneView();
        }

        void RefreshCurrSceneView() {
            SceneView.RepaintAll();
        }

        void DrawMouseSelection() {
            var selTrans = UnityEditor.Selection.activeTransform;
            if (selTrans != null) {
                var ms = selTrans.GetComponent<MeshRenderer>();
                if (ms != null) {
                    var b = ms.bounds;
                    Handles.color = Color.cyan;
                    Handles.DrawWireCube(b.center, b.size);
                }
            }
        }

        void OnSceneGUI(SceneView sceneView) {
            DrawBounds();
			DrawCells ();
			DrawCombineCells ();
            // 看射线
            DrawSelCellLines();
            DrawMouseSelection();
        }

		void DrawCombineCells()
		{
			if (!m_ShowCombineCell || m_CellsSave == null || m_CellsSave.Length <= 0)
				return;
			var oldColor = Handles.color;
			Handles.color = Color.blue;
			var camY = this.CamHeight;
			for (int i = 0; i < m_CellsSave.Length; ++i) {
				var cell = m_CellsSave [i];
				var b = cell.GetBounds (camY);
				Handles.DrawWireCube(b.center, b.size);
			}
			Handles.color = oldColor;
		}

        void DrawSelCellLines() {
            if (m_SelCellResult < 0 || m_CellsResult == null || m_CellsResult.Length <= 0 || m_SelCellResult >= m_CellsResult.Length)
                return;

            if (m_BakerMode == PVSSceneBakerMode.CameraRTComputeMode || m_BakerMode == PVSSceneBakerMode.CameraRTCpuMode)
                return;

            var cell = m_CellsResult[m_SelCellResult];
            if (cell == null)
                return;

            Vector3 right = m_TargetCamInfo.right;
            Vector3 up = m_TargetCamInfo.up;
            Vector3 lookAt = m_TargetCamInfo.lookAt;

            Handles.color = Color.blue;

            if (m_IsCamOri) {
                var width = m_TargetCamInfo.CameraWidth;
                var height = m_TargetCamInfo.CameraHeight;
                float perX = PVSBakerThread.GetPerX(width);
                float perY = PVSBakerThread.GetPerY(height);
                Vector3 halfV = (right * width / 2.0f + up * height / 2.0f);
                Vector3 minVec = cell.position - halfV;

               
                float stepY = 0;

                while (stepY <= height) {
                    Vector3 yy = stepY * up;
                    float stepX = 0;
                    while (stepX <= width) {

                        Vector3 startPt = minVec + stepX * right + yy + lookAt * m_TargetCamInfo.near;
                        Ray ray = new Ray(startPt, lookAt);
                        Handles.DrawLine(startPt, startPt + lookAt * (m_TargetCamInfo.far - m_TargetCamInfo.near));

                        stepX += perX;
                    }
                    stepY += perY;
                }

            } else {
                var width = m_TargetCamInfo.farWidth;
                var height = m_TargetCamInfo.farHeight;
                float perX = PVSBakerThread.GetPerX(width);
                float perY = PVSBakerThread.GetPerY(height);
                Vector3 halfV = (right * width / 2.0f + up * height / 2.0f);
                Vector3 minVec = cell.position - halfV;
                float d = m_TargetCamInfo.far;

                float stepY = 0;

                while (stepY < height) {
                    Vector3 yy = stepY * up;
                    float stepX = 0;
                    while (stepX < width) {

                        Vector3 endPt = minVec + stepX * right + yy + lookAt * d;
                        Vector3 dir = (endPt - m_TargetCamInfo.position).normalized;
                        Ray ray = new Ray(m_TargetCamInfo.position, dir);
                        Handles.DrawLine(ray.origin, ray.origin + dir * m_TargetCamInfo.far);

                        stepX += perX;
                    }
                    stepY += perY;
                }
            }
        }

        private void OnDestroy() {
            Clear();
            if (m_PVSCellShader != null) {
                m_PVSCellShader = null;
            }
            UnityEditor.SceneView.onSceneGUIDelegate -= OnSceneGUI;
            Debug.Log("注销SceneView事件成功");

            RefreshCurrSceneView();
        }

		void CreateThreadMode()
		{
            if (m_CellArray == null || m_CellArray.Length <= 0 || m_BoundsArray == null || m_BoundsArray.Length <= 0 || m_CellsResult == null || m_CellsResult.Length <= 0) {
                Debug.LogError("数据不足");
                return;
            }
			int itemCount = Mathf.CeilToInt (((float)m_CellArray.Length) / ((float)PVSBakerThread.MaxThreadCount));
			int idx = 0;
            m_CpuThreadRuning = true;
            while (idx < m_CellArray.Length) {
				int cnt = System.Math.Min(itemCount, (m_CellArray.Length - idx));
				var thread = new PVSBakerThread (m_CellArray, m_CellsResult, m_BoundsArray, idx, idx + cnt - 1, m_TargetCamInfo, m_IsCamOri);
				if (m_BakerThreads == null)
					m_BakerThreads = new List<PVSBakerThread> ();
				m_BakerThreads.Add (thread);
				thread.Start ();
				idx += itemCount;
			}
        }

        private float CamHeight {
            get {
                InitCameraInfo();

                float camY = PVSCell.CellSize;
                if (m_TargetCamera != null) {
                    if (m_IsCamOri)
                        camY = m_TargetCamInfo.CameraHeight;
                    else {
                        //camY = m_TargetCamInfo.nearHeight;
                        camY = ((float)Screen.height)/100.0f;
                    }
                }

                return camY;
            }
        }

        private ComputeBuffer m_CheckCellBuffer = null;
        private ComputeBuffer m_CameraBuffer = null;
		private void ProcessCurrentScene(bool isOnlyInit = false) {
            // 获得当前场景NavMesh
			if (isOnlyInit) {
				var tris = UnityEngine.AI.NavMesh.CalculateTriangulation ();
				if (tris.vertices == null || tris.indices == null || tris.indices.Length <= 0 || tris.vertices.Length <= 0) {
					Debug.LogError ("当前场景没有NavMesh");
					return;
				}

                m_PVSItemIDS = GameObject.FindObjectOfType<PVSItemIDS>();
                if (m_PVSItemIDS == null) {
                    GameObject obj = new GameObject("PVSItemIDS", typeof(PVSItemIDS));
                    m_PVSItemIDS = obj.GetComponent<PVSItemIDS>();
                }

                Vector3 minVec = Vector3.zero;
				Vector3 maxVec = Vector3.zero;
				for (int i = 0; i < tris.vertices.Length; ++i) {
					var vec = tris.vertices [i];
					if (i == 0) {
						minVec = vec;
						maxVec = vec;
					} else {
						minVec.x = Mathf.Min (minVec.x, vec.x);
						minVec.y = Mathf.Min (minVec.y, vec.y);
						minVec.z = Mathf.Min (minVec.z, vec.z);

						maxVec.x = Mathf.Max (maxVec.x, vec.x);
						maxVec.y = Mathf.Max (maxVec.y, vec.y);
						maxVec.z = Mathf.Max (maxVec.z, vec.z);
					}
				}

                float camY = this.CamHeight;

                //  处理区域
                Vector3 center = (maxVec + minVec) / 2.0f;
				Vector3 size = (maxVec - minVec);
				size.x = Mathf.Abs (size.x);
				size.y = Mathf.Abs (size.y);
				size.z = Mathf.Abs (size.z);

				m_SearchBounds = new Bounds (center, size);

				int col = Mathf.CeilToInt (size.x / PVSCell.CellSize);
				int row = Mathf.CeilToInt (size.z / PVSCell.CellSize);

                

				Vector3 halfSize = new Vector3 (PVSCell.CellSize, 0, PVSCell.CellSize) / 2.0f;
				Vector3[] arr = new Vector3[col * row];
				for (int r = 0; r < row; ++r) {
					for (int c = 0; c < col; ++c) {
						int idx = c + r * col;
						Vector3 pt = new Vector3 (c * PVSCell.CellSize + minVec.x, center.y, r * PVSCell.CellSize + minVec.z) + halfSize;
						UnityEngine.AI.NavMeshHit hit;
						if (UnityEngine.AI.NavMesh.SamplePosition (pt, out hit, PVSCell.CellSize, int.MaxValue)) {
							pt.y = hit.position.y + camY / 2.0f;
						} else {
                            // 再搜索四个角，看是否有效，否则直接SIZE为0
                            Vector3 c1 = pt - halfSize;
                            Vector3 c2 = pt - new Vector3(halfSize.x, 0, -halfSize.z);
                            Vector3 c3 = pt + halfSize;
                            Vector3 c4 = pt + new Vector3(halfSize.x, 0, -halfSize.z);

                            if ((!UnityEngine.AI.NavMesh.SamplePosition(c1, out hit, PVSCell.CellSize, int.MaxValue)) &&
                                (!UnityEngine.AI.NavMesh.SamplePosition(c2, out hit, PVSCell.CellSize, int.MaxValue)) &&
                                (!UnityEngine.AI.NavMesh.SamplePosition(c3, out hit, PVSCell.CellSize, int.MaxValue)) &&
                                (!UnityEngine.AI.NavMesh.SamplePosition(c4, out hit, PVSCell.CellSize, int.MaxValue))
                               ) 
                            {
								pt = Vector3.zero;
								m_CellIngoreArray.Add (idx);
                            }
                        }
						
						arr [idx] = pt;
					}
				}

				m_CellArray = arr;
               


                return;
			}

			var size1 = m_SearchBounds.size;
			int col1 = Mathf.CeilToInt (size1.x / PVSCell.CellSize);
            int row1 = Mathf.CeilToInt(size1.z / PVSCell.CellSize);

			InitPVSMeshes ();
            InitCellResults();


            ClearThreads ();
			if (m_CheckCellBuffer != null) {
				m_CheckCellBuffer.Dispose();
				m_CheckCellBuffer = null;
			}

			if (m_CameraBuffer != null) {
				m_CameraBuffer.Dispose ();
				m_CameraBuffer = null;
			}

			if (m_BakerMode == PVSSceneBakerMode.CameraRTCpuMode || m_BakerMode == PVSSceneBakerMode.CameraRTComputeMode) {

                if (m_PVSCellShader == null) {
                    Debug.LogError("PVS烘焙ComputeShader找不到");
                    return;
                }

				PVSCameraRtTrack camRtTrack = new PVSCameraRtTrack ();
				camRtTrack.Start (m_TargetCamera, m_CellsResult, m_PVSItemIDS, m_TargetCamInfo.lookAt, m_PVSCellShader, m_BakerMode == PVSSceneBakerMode.CameraRTComputeMode);
                OnCpuThreadDone();
                return;
			}

			if (!SystemInfo.supportsComputeShaders || m_BakerMode == PVSSceneBakerMode.CpuThreadMode) {
                if (m_BakerMode == PVSSceneBakerMode.ComputeShaderMode)
				    Debug.LogError ("设备不支持ComputeShader");
				// 开启线程模式
				CreateThreadMode();
				return;
			}


			m_CheckCellBuffer = new ComputeBuffer(m_CellArray.Length, Marshal.SizeOf(typeof(Vector3)));
			m_CheckCellBuffer.SetData(m_CellArray);

            m_CameraBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(PVSCameraInfo)));
            PVSCameraInfo[] camArr = new PVSCameraInfo[1];
            camArr[0] = m_TargetCamInfo;
            m_CameraBuffer.SetData(camArr);

            if (m_PVSCellShader != null) {
                int main = m_PVSCellShader.FindKernel("PVSBaker");
                m_PVSCellShader.SetBuffer(main, "pvsBakerPosArray", m_CheckCellBuffer);
                m_PVSCellShader.SetBuffer(main, "camera", m_CameraBuffer);
                m_PVSCellShader.Dispatch(main, col1, row1, 1);
            } else {
                Debug.LogError("PVS烘焙ComputeShader找不到");
            }
        }

        void InitCellResults() {
            if (m_CellArray == null || m_CellArray.Length <= 0 || m_BoundsArray == null || m_BoundsArray.Length <= 0)
                return;
            int cnt = Mathf.CeilToInt(m_GlobalNodeId / 8.0f);
            m_CellsResult = new PVSCell[m_CellArray.Length];
            for (int i = 0; i < m_CellsResult.Length; ++i) {
                var pt = m_CellArray[i];
                if (!m_CellIngoreArray.Contains(i) && pt.magnitude > Vector3.kEpsilon) {
                    PVSCell cell = new PVSCell();
                    m_CellsResult[i] = cell;
                    cell.visibleBits = new byte[cnt];
                    cell.position = pt;
                } else {
                    m_CellsResult[i] = null;
                }
            }
        }

		void ClearThreads()
		{
			if (m_BakerThreads != null) {
				for (int i = 0; i < m_BakerThreads.Count; ++i) {
					var thread = m_BakerThreads [i];
					if (thread == null)
						continue;
					thread.Dispose ();
				}
				m_BakerThreads.Clear ();
			}
		}
			

        void Clear() {
			m_SelSaveMode = 0;
            m_SearchBounds = new Bounds();
            m_GlobalNodeId = 0;
			m_CellArray = null;
			m_CellIngoreArray.Clear ();
            m_CellsResult = null;
			m_CellsSave = null;
            m_BoundsArray = null;
            m_PVSItemIDS = null;
            m_SelCellResult = -1;
            if (m_CheckCellBuffer != null) {
                m_CheckCellBuffer.Dispose();
                m_CheckCellBuffer = null;
            }
            if (m_CameraBuffer != null) {
                m_CameraBuffer.Dispose();
                m_CameraBuffer = null;
            }
			ClearThreads ();
        }

        void DrawBounds() {
            Handles.color = Color.red;
            Handles.DrawWireCube(m_SearchBounds.center, m_SearchBounds.size);
        }

        void OnCellResultSelectChanged() {
            if (m_CellsResult == null || m_CellsResult.Length <= 0 || m_PVSItemIDS == null || m_PVSItemIDS.m_IDs == null || m_PVSItemIDS.m_IDs.Count <= 0)
                return;
            if (m_SelCellResult < 0 || m_SelCellResult >= m_CellsResult.Length)
                return;
            var cell = m_CellsResult[m_SelCellResult];
            if (cell == null)
                return;

            if (m_TargetCamera != null) {
                m_TargetCamera.transform.position = cell.position;
            }

            bool[] bList = new bool[m_PVSItemIDS.m_IDs.Count];

            if (cell.visibleBits != null && cell.visibleBits.Length > 0) {
				for (int i = 0; i < bList.Length; ++i) {
					int id = i + 1;
					bList [i] = cell.IsVisible (id);
				}
            }

            for (int i = 0; i < bList.Length; ++i) {
                var trans = m_PVSItemIDS.m_IDs[i];
                if (trans.gameObject.activeSelf != bList[i])
                    trans.gameObject.SetActive(bList[i]);
            }


            RefreshCurrSceneView();
        }

        void DrawCells() {
            if (!m_ShowCell || m_CellArray == null || m_CellArray.Length <= 0)
                return;
            var camY = this.CamHeight;
            var cellSize = new Vector3(PVSCell.CellSize, camY, PVSCell.CellSize);
            Handles.color = Color.yellow;
            var size = m_SearchBounds.size;
            int col = Mathf.CeilToInt(size.x / PVSCell.CellSize);
            int row = Mathf.CeilToInt(size.z / PVSCell.CellSize);
			float zero = (new Vector3 (float.MinValue, float.MinValue, float.MinValue)).sqrMagnitude;
            for (int r = 0; r < row; ++r) {
                for (int c = 0; c < col; ++c) {
                    int idx = r * col + c;
                    if (idx >= m_CellArray.Length)
                        return;
					if (m_CellIngoreArray.Contains (idx))
						continue;
                    var pt = m_CellArray[idx];
					//if (Mathf.Abs (pt.sqrMagnitude - zero) <= Vector3.kEpsilon)
					//	continue;


                    bool isSelected = idx == m_SelCellResult;

                    var oldColor = Handles.color;
                    if (isSelected) {
                        Handles.color = Color.green;
                    }
                    Handles.DrawWireCube(pt, cellSize);
                    if (isSelected)
                        Handles.color = oldColor;
                }
            }
        }

        private Vector2 m_CellResultScrollPos = Vector2.zero;
        void DrawCellResults() {
            if (m_CellsResult == null || m_CellsResult.Length <= 0)
                return;

            if (m_PVSItemIDS != null && m_PVSItemIDS.m_IDs != null && m_PVSItemIDS.m_IDs.Count > 0 && GUILayout.Button("显示所有遮挡物体")) {
                for (int i = 0; i < m_PVSItemIDS.m_IDs.Count; ++i) {
                    var trans = m_PVSItemIDS.m_IDs[i];
                    if (!trans.gameObject.activeSelf)
                        trans.gameObject.SetActive(true);
                }
            }

            var size = m_SearchBounds.size;
            int col = Mathf.CeilToInt(size.x / PVSCell.CellSize);
            int row = Mathf.CeilToInt(size.z / PVSCell.CellSize);

           // EditorGUILayout.BeginToggleGroup("格子结果", true);
            m_CellResultScrollPos = EditorGUILayout.BeginScrollView(m_CellResultScrollPos);

            string[] grids = new string[m_CellsResult.Length];
            for (int i = 0; i <grids.Length; ++i) {
                var cell = m_CellsResult[i];
                if (cell != null)
                    grids[i] = i.ToString();
                else
                    grids[i] = "(空)";
            }

            int newSelect = GUILayout.SelectionGrid(m_SelCellResult, grids, col);
            if (m_SelCellResult != newSelect) {
                m_SelCellResult = newSelect;
                OnCellResultSelectChanged();
            }


            EditorGUILayout.EndScrollView();
           // EditorGUILayout.EndToggleGroup();
        }

		void InitPVSMeshes()
		{
            m_PVSItemIDS.Clear();

            m_GlobalNodeId = 0;
			m_BoundsArray = null;
			if (m_PVSMeshRoot != null) {
				MeshRenderer[] mss = m_PVSMeshRoot.GetComponentsInChildren<MeshRenderer> (true);
				if (mss != null) {
					System.Array.Sort (mss, (MeshRenderer m1, MeshRenderer m2) => {
						var p1 = m1.transform.position;
						var p2 = m2.transform.position;
						float vv = p1.z - p2.z;
						if (Mathf.Abs(vv) > float.Epsilon)
						{
							if (p1.z < p2.z)
								return -1;
							return 1;
						}
						vv = p1.x - p2.x;
						if (Mathf.Abs(vv) > float.Epsilon)
						{
							if (p1.x < p2.x)
								return -1;
							return 1;
						}
						return 0;
					}
					);
					m_BoundsArray = new PVSBounds[mss.Length];
					for (int i = 0; i < mss.Length; ++i) {
						var ms = mss [i];
						PVSBounds bounds = new PVSBounds ();
						var b = ms.bounds;
                        bounds.min = ms.bounds.min;
                        bounds.max = ms.bounds.max;
                        bounds.forward = ms.transform.forward;
                        bounds.up = ms.transform.up;
                        bounds.id = GeneratorNodeId();
                        m_BoundsArray [i] = bounds;
                        m_PVSItemIDS.AddIDs(bounds.id, ms.transform);

                    }
				} 
			}

            m_PVSItemIDS.m_BakerRoot = m_PVSMeshRoot;

        }

        private void OnCpuThreadDone() {
            EditorUtility.ClearProgressBar();
            if (m_CellsResult != null) {

                string log = string.Empty;
                for (int i = 0; i < m_CellsResult.Length; ++i) {
                    var cell = m_CellsResult[i];
                    if (cell == null)
                        continue;
                    System.Text.StringBuilder builder = new System.Text.StringBuilder();
                    if (cell.visibleBits != null && cell.visibleBits.Length > 0) {
                        for (int j = 0; j < cell.visibleBits.Length; ++j) {
                            var b = cell.visibleBits[j];
                            if (builder.Length > 0)
                                builder.Append('|');
                            builder.Append(b);
                        }
                    }

                    if (builder.Length > 0) {
                        builder.Insert(0, string.Format("Cell: {0:D}=>", i)).AppendLine();
                        log += builder.ToString();
                    }
                }

                System.IO.FileStream stream = new System.IO.FileStream("PVS_Result.txt", System.IO.FileMode.Create, System.IO.FileAccess.Write);
                try {
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(log);
                    if (buffer != null && buffer.Length > 0) {
                        stream.Write(buffer, 0, buffer.Length);
                    }
                } finally {
                    stream.Dispose();
                }

                stream = new System.IO.FileStream("PVS.bytes", System.IO.FileMode.Create, System.IO.FileAccess.Write);
                try {

                    int col = Mathf.CeilToInt(m_SearchBounds.size.x / PVSCell.CellSize);
                    int row = Mathf.CeilToInt(m_SearchBounds.size.z / PVSCell.CellSize);

                    DoCombineResults();

                    Vector2 sz = new Vector2(col * PVSCell.CellSize, row * PVSCell.CellSize);
                    Vector2 center = new Vector2(m_SearchBounds.min.x + sz.x/2.0f, m_SearchBounds.min.z + sz.y/2.0f);

                    Rect quadTreeRect = new Rect();
                    quadTreeRect.size = sz;
                    quadTreeRect.center = center;
					PVSManager.SaveToStream(stream, m_CellsSave, quadTreeRect, m_SelSaveMode - 1);
                } finally {
                    stream.Dispose();
                }

                UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }
        }

        private void CheckCpuThreadRun() {

            bool isOldCpuThreadRuning = m_CpuThreadRuning;
            if (m_BakerThreads != null && m_BakerThreads.Count > 0) {
                for (int i = m_BakerThreads.Count - 1; i >= 0; --i) {
                    var thread = m_BakerThreads[i];
                    if (thread != null && !thread.IsThreadRuning) {
                        m_BakerThreads.RemoveAt(i);
                        thread.Dispose();
                    } else if (thread == null) {
                        m_BakerThreads.RemoveAt(i);
                    }
                }
            }

            if (m_BakerThreads == null || m_BakerThreads.Count <= 0) {
                m_CpuThreadRuning = false;
                if (isOldCpuThreadRuning != m_CpuThreadRuning) {
                    OnCpuThreadDone();
                }
                return;
            }

            m_CpuThreadRuning = true;
        }

        private void CheckCellsEditorDone() {
            if (m_CpuThreadRuning) {
                int doneCnt = 0;
                for (int i = 0; i < m_CellsResult.Length; ++i) {
                    var cell = m_CellsResult[i];
                    if (cell == null)
                        continue;
                    lock(cell) {
                        if (cell.isEditorDone)
                            ++doneCnt;
                    }
                }

                EditorUtility.DisplayProgressBar("Cell扫描中...", "Cell扫描中...", (float)doneCnt / (float)m_CellsResult.Length);
            }
        }

        void CheckInputMgr() {

            if (m_CellsResult == null || m_CellsResult.Length <= 0 || m_SelCellResult < 0)
                return;

            bool isChg = false;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) {
                if (m_SelCellResult <= 0)
                    return;
                --m_SelCellResult;
                isChg = true;
            }  else if (Input.GetKeyDown(KeyCode.RightArrow)) {
                if (m_SelCellResult + 1 >= m_CellsResult.Length)
                    return;
                ++m_SelCellResult;
                isChg = true;
            }

            if (isChg)
                RefreshCurrSceneView();
        }

        private SceneAsset m_SelectScene = null;
		private int m_SelSaveMode = 0;
		private static readonly string[] m_SaveModes = new string[]{"自动选择", "合并bits数组", "Cell单独存储"};
        private void OnGUI() {

            CheckCpuThreadRun();
            CheckCellsEditorDone();

            EditorGUILayout.ObjectField("PVS烘焙Shader", m_PVSCellShader, typeof(ComputeShader), false);

            var newSelectScene = EditorGUILayout.ObjectField("选择场景", m_SelectScene, typeof(SceneAsset), false) as SceneAsset;
            if (newSelectScene != null) {
                bool isChg = m_SelectScene != newSelectScene;
                if (isChg)
                    Clear();
                m_SelectScene = newSelectScene;

                var activityScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                if (isChg && activityScene.isLoaded && activityScene.IsValid()) {
                    string selPath = AssetDatabase.GetAssetPath(newSelectScene);
                    if (string.Compare(activityScene.path, selPath, true) == 0) {
						ProcessCurrentScene (true);
                    }
                }

				var newPVSMeshRoot = EditorGUILayout.ObjectField("选择建筑物根节点", m_PVSMeshRoot, typeof(Transform), true) as Transform;
				if (m_PVSMeshRoot != newPVSMeshRoot) {
					m_PVSMeshRoot = newPVSMeshRoot;
					//InitPVSMeshes ();
				}

                // 摄像机烘焙参数
                var newTargetCamera = EditorGUILayout.ObjectField("烘焙相机", m_TargetCamera, typeof(Camera), true) as Camera;
                if (newTargetCamera != m_TargetCamera) {
                    m_TargetCamera = newTargetCamera;
                    //InitCameraInfo();
                }

				m_TargetCamInfo.lookAt = EditorGUILayout.Vector3Field ("相机烘焙方向", m_TargetCamInfo.lookAt);
                m_BakerMode = (PVSSceneBakerMode)EditorGUILayout.EnumPopup("烘培模式", m_BakerMode);
                if (m_CpuThreadRuning) {
                    EditorGUILayout.LabelField("当前CPU线程正在运行...");
                }


				EditorGUILayout.BeginHorizontal ();
                bool newShowCell = GUILayout.Toggle(m_ShowCell, "显示格子");
                if (m_ShowCell != newShowCell) {
                    m_ShowCell = newShowCell;
					if (m_ShowCell)
						m_ShowCombineCell = false;
                    RefreshCurrSceneView();
                }

				if (m_CellsSave != null && m_CellsSave.Length > 0) {
					newShowCell = GUILayout.Toggle (m_ShowCombineCell, "显示存储格子");
					if (m_ShowCombineCell != newShowCell) {
						m_ShowCombineCell = newShowCell;
						if (m_ShowCombineCell)
							m_ShowCell = false;
						RefreshCurrSceneView ();
					}
				}
				EditorGUILayout.EndHorizontal ();

				m_SelSaveMode = EditorGUILayout.Popup (m_SelSaveMode, m_SaveModes);

                if (GUILayout.Button("开始烘焙PVS")) {
					
                    string scenePath = AssetDatabase.GetAssetPath(m_SelectScene);
                    if (string.IsNullOrEmpty(scenePath))
                        return;
					if (string.Compare (activityScene.path, scenePath, true) != 0) {
						var openScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene (scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
						if (openScene.isLoaded) {
							ProcessCurrentScene ();
						} else {
							Debug.LogErrorFormat ("场景打开失败：{0}", m_SelectScene.name);
						}
					} else
						ProcessCurrentScene ();
                    
                }

              //  CheckInputMgr();
				DrawCombineCellsCtl();
                DrawCellResults();

                DrawSelectMeshObjLineCheck();
            }

        }

         static PVSCell[] CombinePVSCells(int rowCnt, int colCnt, PVSCell[] cells, Rect allBounds) {

            PVSCell[] ret = null;

            QuadGroupTree<PVSCell> quadTree = new QuadGroupTree<PVSCell>(rowCnt, colCnt, cells);
		
			int oldCnt = quadTree.LeafCount;

			System.Func<PVSCell, PVSCell, PVSCell, PVSCell, bool> onCompare =
				(PVSCell c1, PVSCell c2, PVSCell c3, PVSCell c4) => {
				bool r1 = (c1.lod == c2.lod && c2.lod == c3.lod && c3.lod == c4.lod) &&
					c1.CompareVisibleBits(c2) && c2.CompareVisibleBits(c3) && c3.CompareVisibleBits(c4);
				if (r1 && c1.lod >= 255)
					return false;
				return r1;
				};

			System.Func<PVSCell, PVSCell, PVSCell, PVSCell, PVSCell> onCreate =
				(PVSCell c1, PVSCell c2, PVSCell c3, PVSCell c4) => {
				PVSCell r2 = new PVSCell();
				int newLod = c1.lod + 1;
				r2.lod = (byte)newLod;
				r2.position = (c1.position + c2.position + c3.position + c4.position)/4.0f;
				r2.visibleBits = c1.visibleBits;
				return r2;
				};

            int inputCnt = quadTree.LeafCount;
			while (quadTree.Combine (onCompare, onCreate)) {
			}

			List<PVSCell> cellList = quadTree.ToList ();
			if (cellList != null && cellList.Count > 0) {
				ret = cellList.ToArray ();
			}

            int resultCnt = quadTree.LeafCount;

            Debug.LogFormat("Input Cell Count: {0:D} result Cell Count: {1:D}", inputCnt, resultCnt);

			quadTree.Dispose ();

            return ret;
        }

        void DoCombineResults() {
            if (m_CellsResult != null && m_CellsResult.Length > 0) {
                Rect allBounds = new Rect();
                var center = m_SearchBounds.min;

                int col = Mathf.CeilToInt(m_SearchBounds.size.x / PVSCell.CellSize);
                int row = Mathf.CeilToInt(m_SearchBounds.size.z / PVSCell.CellSize);
                var sz = new Vector3(col * PVSCell.CellSize, 0, row * PVSCell.CellSize);

                center += sz / 2.0f;

                allBounds.size = new Vector2(sz.x, sz.z);
                allBounds.center = new Vector2(center.x, center.z);
                m_CellsSave = CombinePVSCells(row, col, m_CellsResult, allBounds);
            }
        }

        void DrawCombineCellsCtl() {
			if (m_CellsResult != null && m_CellsResult.Length > 0) {
				if (GUILayout.Button ("生成存储结果")) {
                    DoCombineResults();
                }
			}
        }

        void DrawSelectMeshObjLineCheck() {
            if (m_TargetCamera == null)
                return;

            if (m_BakerMode == PVSSceneBakerMode.CameraRTComputeMode || m_BakerMode == PVSSceneBakerMode.CameraRTCpuMode)
                return;

            var selTrans = Selection.activeTransform;
            if (selTrans != null) {
                var ms = selTrans.GetComponent<MeshRenderer>();
                if (ms != null) {
                    var b = ms.bounds;
                    Handles.color = Color.black;
                    Handles.DrawLine(m_TargetCamInfo.position, b.center);

                    if (GUILayout.Button("检测是否选中碰撞")) {
                        Vector3 dir = b.center - m_TargetCamInfo.position;
                        dir = dir.normalized;
                        Ray ray = new Ray(m_TargetCamInfo.position, dir);
                        PVSBounds bound = new PVSBounds();
                        bound.min = b.min;
                        bound.max = b.max;
                        bound.up = ms.transform.up;
                        bound.forward = ms.transform.forward;

                        float tutDistance = 0f;
                        while(true) {
                            if (PVSRayTrack.sdPVSCell(ref ray, bound, ref tutDistance)) {
                                Debug.Log("有碰撞");
                                break;
                            }
                            if (tutDistance > m_TargetCamInfo.far) {
                                Debug.Log("无碰撞");
                                break;
                            }

                        }
                    }
                  
                }
            }
        }
    }

}
