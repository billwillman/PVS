#define _EnabledFrumCull

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NsLib.Utils;

namespace NsLib.PVS
{

    internal struct PVSBounds {
        public int id;
        //public int instanceID;

        public Vector3 min;
        public Vector3 max;
        public Vector3 forward;
        public Vector3 up;

        public BoundingSphere boudingSpere {
            get {
                Bounds b = new Bounds(this.center, this.size);
                BoundingSphere ret = KdTreeCameraClipper.GetBoundingSphere(b);
                return ret;
            }
        }

        public Vector3 center {
            get {
                Vector3 ret = (min + max) / 2.0f;
                return ret;
            }
        }

        public Vector3 size {
            get {
                Vector3 ret = max - min;
                ret.x = Mathf.Abs(ret.x);
                ret.y = Mathf.Abs(ret.y);
                ret.z = Mathf.Abs(ret.z);
                return ret;
            }
        }
    };

    // 不支持CS的情况采用多线程处理
    internal class PVSBakerThread: CustomThread
	{
        // 最大綫程數量
        internal static readonly int MaxThreadCount = 100;
        internal static readonly int MaxLineRayCount = 15;

        private Vector3[] m_CellArray;
        private PVSBounds[] m_Bounds;
        private PVSCell[] m_Results;
        private int m_StartIdx;
        private int m_EndIdx;
        private PVSCameraInfo m_CamInfo;
        private bool m_IsOriCam = false;
       
    
		public PVSBakerThread(Vector3[] cellArray, PVSCell[] results, PVSBounds[] bounds, int startIdx, int endIdx, PVSCameraInfo camInfo, bool isOriCam)
		{
            m_CellArray = cellArray;
            m_Results = results;
            m_Bounds = bounds;
            m_StartIdx = startIdx;
            m_EndIdx = endIdx;
            m_CamInfo = camInfo;
            m_IsOriCam = isOriCam;

        }

		protected override void OnFree (bool isManual)
		{
			base.OnFree (isManual);
		}

        internal static float GetPerX(float width) {
            float perX = Mathf.Max(0.001f, (width / (float)MaxLineRayCount));
            return perX;
        }

        internal static float GetPerY(float height) {
            float perY = Mathf.Max(0.001f, (height / (float)MaxLineRayCount));
            return perY;
        }

        private void CellRays(int cellIdx, Vector3 cellPos, PVSBounds[] bounds, PVSCameraInfo camInfo, bool isOriCam) {
            var cell = m_Results[cellIdx];
            cell.ResetVisible();

            Vector3 right = camInfo.right;
            Vector3 up = camInfo.up;
            Vector3 lookAt = camInfo.lookAt;
            if (isOriCam) {
                // 正交攝影機
                var width = camInfo.CameraWidth;
                var height = camInfo.CameraHeight;

                float perX = GetPerX(width);
                float perY = GetPerY(height);
                Vector3 halfV = (right * width / 2.0f + up * height / 2.0f);
                Vector3 minVec = cellPos - halfV;

                float stepY = 0;

                while (stepY <= height) {
                    float stepX = 0;
                    Vector3 yy = stepY * up;
                    while (stepX <= width) {

                        Vector3 startPt = minVec + stepX * right + yy + lookAt * camInfo.near;
                        Ray ray = new Ray(startPt, lookAt);
                        float tutDistance = 0;
                        while (true) {
                            int collisionIdx = PVSRayTrack.sdPVSCells(ref ray, bounds, ref tutDistance);
                            if (collisionIdx >= 0) {
                                var b = m_Bounds[collisionIdx];
                                cell.SetVisible(b.id, true);
                                break;
                            }
                            if (tutDistance > (camInfo.far - camInfo.near))
                                break;
                        }

                        stepX += perX;
                    }
                    stepY += perY;
                }

                lock (cell) {
                    cell.isEditorDone = true;
                }

            } else {
                // 透視攝影機
                var width = camInfo.farWidth;
                var height = camInfo.farHeight;
                float perX = GetPerX(width);
                float perY = GetPerY(height);
                Vector3 halfV = (right * width / 2.0f + up * height / 2.0f);
                Vector3 minVec = cellPos - halfV;
                float d = camInfo.far;

                float stepY = 0;

                while (stepY < height) {
                    float stepX = 0;
                    Vector3 yy = stepY * up;
                    while (stepX < width) {

                        Vector3 endPt = minVec + stepX * right + yy + lookAt * d;
                        Vector3 dir = (endPt - camInfo.position).normalized;
                        Ray ray = new Ray(camInfo.position, dir);
                        float tutDistance = 0;
                        while (true) {
                            int collisionIdx = PVSRayTrack.sdPVSCells(ref ray, bounds, ref tutDistance);
                            if (collisionIdx >= 0) {
                                var b = m_Bounds[collisionIdx];
                                cell.SetVisible(b.id, true);
                                break;
                            }
                            if (tutDistance > camInfo.far)
                                break;
                        }

                        stepX += perX;
                    }
                    stepY += perY;
                }

                lock (cell) {
                    cell.isEditorDone = true;
                }
            }
        }

        protected override void Execute() {
            PVSBounds[] bbs = m_Bounds;
			#if _EnabledFrumCull
            if (!m_IsOriCam) {
                List<PVSBounds> fruBounds = new List<PVSBounds>();

                KdCameraInfo kdCamInfo = new KdCameraInfo();
                kdCamInfo.far = m_CamInfo.far;
                kdCamInfo.position = m_CamInfo.position;
                kdCamInfo.cullMatrix = m_CamInfo.cullMatrix;
                var panels = kdCamInfo.CalcPanels();

                for (int i = 0; i < m_Bounds.Length; ++i) {
                    var b = m_Bounds[i];
                    var boundingSpere = b.boudingSpere;
                    if (kdCamInfo.IsBoundingSphereIn(boundingSpere, panels)) {
                        fruBounds.Add(b);
                    }
                }

                bbs = fruBounds.ToArray();
            }
			#endif
            if (bbs.Length > 0) {
                for (int i = m_StartIdx; i <= m_EndIdx; ++i) {
                    Vector3 cellPt = m_CellArray[i];
                    CellRays(i, cellPt, bbs, m_CamInfo, m_IsOriCam);
                }
            }

            Dispose();
		}
	}
}
