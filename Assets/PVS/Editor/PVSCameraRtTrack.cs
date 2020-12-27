using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NsLib.PVS
{

	class MeshOldData
	{
		public Material[] mats;
		public Material mat;
		public string tag;
		public int layer;
		public bool isVisible;
	}

	struct CameraOldData
	{
		private Vector3 forward;
		private Vector3 up;
		private Vector3 position;
		private bool allowMSAA;
		private bool allowHDR;
		private Color backgroundColor;
		private CameraClearFlags clearFlags;
		private RenderTexture rt;
		private int cull;

		public CameraOldData(Camera targetCamera)
		{
			this.forward = targetCamera.transform.forward;
			this.up = targetCamera.transform.up;
			this.position = targetCamera.transform.position;
			this.allowHDR = targetCamera.allowHDR;
			this.allowMSAA = targetCamera.allowMSAA;
			this.backgroundColor = targetCamera.backgroundColor;
			this.clearFlags = targetCamera.clearFlags;
			this.rt = targetCamera.targetTexture;
			this.cull = targetCamera.cullingMask;
		}

		public void AssignTo(Camera targetCamera)
		{
			targetCamera.transform.forward = this.forward;
			targetCamera.transform.up = this.up;
			targetCamera.transform.position = this.position;
			targetCamera.allowHDR = this.allowHDR;
			targetCamera.allowMSAA = this.allowMSAA;
			targetCamera.backgroundColor = this.backgroundColor;
			targetCamera.clearFlags = this.clearFlags;
			targetCamera.targetTexture = this.rt;
			targetCamera.cullingMask = this.cull;
		}
	}

	public class PVSCameraRtTrack
	{
		internal static readonly int RTSize_Width = 512;   // 最小不能小于CS_X
		internal static readonly int RTSize_Height = 256;  // 最小不能小于CS_Y
        internal static readonly int CS_X = 32; 
        internal static readonly int CS_Y = 32;
		internal static readonly string cTrackTag = "EditorOnly";
		internal static readonly int cTrackLayer = 2;
		internal static readonly RenderTextureFormat cRTFormat = RenderTextureFormat.ARGB32;
		internal static readonly TextureFormat cSaveFormat = TextureFormat.ARGB32;
        internal static readonly int cMaskColorStep = 10;
        internal static readonly ColorSpace cColorSpace = ColorSpace.Gamma;

		internal unsafe void Start (Camera targetCamera, PVSCell[] cells, PVSItemIDS items, Vector3 camDir, ComputeShader shader, bool isComputeMode)
		{
			if (targetCamera == null || cells == null || cells.Length <= 0 || !items.IsVaild || shader == null)
				return;
			try {
				CameraOldData camOldData = new CameraOldData(targetCamera);
			
				targetCamera.transform.forward = camDir;
				targetCamera.transform.up = Vector3.up;

				targetCamera.allowMSAA = false;
				targetCamera.allowHDR = false;
				targetCamera.backgroundColor = Color.black;
				targetCamera.clearFlags = CameraClearFlags.SolidColor;
				// 关闭GAMMA
				var oldSpace = UnityEditor.PlayerSettings.colorSpace;
				UnityEditor.PlayerSettings.colorSpace = cColorSpace;

				Material mat = AssetDatabase.LoadAssetAtPath<Material> ("Assets/PVS/Editor/PVS_MaskColor.mat");
				Material[] mats = new Material[1]{ mat };
				// 设置TAG
				List<MeshOldData> oldDatas = new List<MeshOldData> (items.m_IDs.Count);
				List<int> itemColorTags = new List<int> (items.m_IDs.Count);
				int Red = cMaskColorStep;
				for (int i = 0; i < items.m_IDs.Count; ++i) {
					var trans = items.m_IDs [i];
					var oldData = new MeshOldData ();
					oldData.tag = trans.gameObject.tag;
					oldData.layer = trans.gameObject.layer;
					oldData.isVisible = trans.gameObject.activeSelf;
					oldDatas.Add (oldData);

					trans.gameObject.SetActive (true);
					trans.gameObject.layer = cTrackLayer;
					//trans.gameObject.tag = cTrackTag;
					//trans.gameObject.layer = UnityEditor.EditorUserSettings.la
					var ms = trans.GetComponent<MeshRenderer> ();
					if (ms != null) {
						oldData.mats = ms.sharedMaterials;
                        if (oldData.mats != null) {
                            if (oldData.mats.Length <= 0)
                                oldData.mats = null;
                            else {
                                bool isHasMat = false;
                                for (int j = 0; j < oldData.mats.Length; ++j) {
                                    if (oldData.mats[j] != null) {
                                        isHasMat = true;
                                        break;
                                    }
                                }
                                if (!isHasMat) {
                                    oldData.mats = null;
                                }
                            }
                        }
						oldData.mat = ms.sharedMaterial;
						ms.sharedMaterials = mats;
						MaterialPropertyBlock block = new MaterialPropertyBlock ();
						byte r = (byte)(Red & 0xFF);
						byte g = (byte)((Red >> 8) & 0xFF);
                        byte b = (byte)((Red >> 16) & 0xFF);
						Color32 cc = new Color32 (r, g, b, 255);
						block.SetColor ("_MaskColor", cc);
						ms.SetPropertyBlock (block);
						itemColorTags.Add (Red);
                        Red += cMaskColorStep;

                    }
				}

				RenderTexture targetTexture = new RenderTexture (RTSize_Width, RTSize_Height, 16, cRTFormat, RenderTextureReadWrite.sRGB);
				targetTexture.useMipMap = false;
				targetTexture.autoGenerateMips = false;

                targetTexture.filterMode = FilterMode.Point;
                targetTexture.enableRandomWrite = false;
                targetTexture.isPowerOfTwo = false;

                if (!targetTexture.Create ()) {
					Debug.LogError ("创建RT失败");
					return;
				}
					
				targetCamera.targetTexture = targetTexture;
				targetCamera.cullingMask = 1 << cTrackLayer;

                int[] resultBools = new int[items.m_IDs.Count];
                ComputeBuffer resultBuffer = null;
                if (isComputeMode)
                    resultBuffer = new ComputeBuffer(resultBools.Length, System.Runtime.InteropServices.Marshal.SizeOf(typeof(int)));
               // resultBuffer.SetData<int>(resultBoolList);

				for (int i = 0; i < cells.Length; ++i) {
					var cell = cells [i];
					if (cell == null)
						continue;
					float process = ((float)i + 1) / (float)cells.Length;
					EditorUtility.DisplayProgressBar ("RT烘焙处理", string.Format ("格子索引：{0:D}", i), process);
					DoStart (i, targetCamera, cell, items, targetTexture);
                    if (isComputeMode) {
                        DispatchComputeShader(shader, targetTexture, resultBuffer);
                        resultBuffer.GetData(resultBools);
                    } else {

                        System.Array.Clear(resultBools, 0, resultBools.Length);

                        DispatchCpu(targetTexture, resultBools);
                    }
                    
                  //  if (resultBools[0] != -1)
                   //     Debug.Log(resultBools[0].ToString());
                    for (int j = 0; j < resultBools.Length; ++j) {
                        bool isVisible = resultBools[j] != 0;
                        cell.SetVisible(j + 1, isVisible);
                    }
                }

                if (isComputeMode && resultBuffer != null) {
                    resultBuffer.Release();
                    resultBuffer.Dispose();
                }


                targetCamera.targetTexture = null;
                targetTexture.Release();
                GameObject.DestroyImmediate (targetTexture);

				for (int i = 0; i < items.m_IDs.Count; ++i) {
					try {
						var trans = items.m_IDs [i];
						trans.tag = oldDatas [i].tag;
						trans.gameObject.SetActive (oldDatas [i].isVisible);
						trans.gameObject.layer = oldDatas [i].layer;
						var ms = trans.GetComponent<MeshRenderer> ();
						if (ms != null) {
                            
							if (oldDatas [i].mats != null)
								ms.sharedMaterials = oldDatas [i].mats;
							else {
                                if (oldDatas[i].mat != null)
                                    ms.sharedMaterial = oldDatas[i].mat;
							}

						}
					} catch {
					}
				}

				UnityEditor.PlayerSettings.colorSpace = oldSpace;

				camOldData.AssignTo(targetCamera);
			} finally {
				EditorUtility.ClearProgressBar ();
			}
		}

        private void DispatchComputeShader(ComputeShader shader, RenderTexture target, ComputeBuffer result) {
            int main = shader.FindKernel("PVSRtBaker");
            int groupX = target.width / CS_X;
            int groupY = target.height / CS_Y;

            shader.SetTexture(main, "PVSRt", target);
            shader.SetBuffer(main, "PVSRtResult", result);
            shader.SetInt("perValue", cMaskColorStep);
            shader.Dispatch(main, groupX, groupY, 1);
           // shader.SetBuffer
        }

        private void DispatchCpu(RenderTexture target, int[] resultBools) {
            var oldActive = RenderTexture.active;
            RenderTexture.active = target;

            Texture2D png = new Texture2D(target.width, target.height, cSaveFormat, false);
            png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);

            var colors = png.GetPixels32();
            for (int i = 0; i < colors.Length; ++i) {
                 var color = colors[i];
                 int idx = ((int)(color.r) | (((int)(color.g)) << 8) | (((int)(color.b)) << 16))/ cMaskColorStep;
                --idx;
                if (idx >= 0 && idx < resultBools.Length)
                    resultBools[idx] = 1;
            }

            RenderTexture.active = oldActive;
            GameObject.DestroyImmediate(png);
        }

		private static void SaveRtToFile (string fileName, RenderTexture target)
		{
			var oldActive = RenderTexture.active;
			RenderTexture.active = target;

			Texture2D png = new Texture2D (target.width, target.height, cSaveFormat, false, false);
			png.ReadPixels (new Rect (0, 0, target.width, target.height), 0, 0);
			byte[] bytes = png.EncodeToPNG ();

			System.IO.FileStream stream = new System.IO.FileStream (fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write);
			try {
				stream.Write (bytes, 0, bytes.Length);
			} finally {
				stream.Dispose ();
			}

			RenderTexture.active = oldActive;
			GameObject.DestroyImmediate (png);
		}

		private void DoStart (int cellIdx, Camera targetCamera, PVSCell cell, PVSItemIDS items, RenderTexture targetTexture)
		{
			// 1. 设置Camera的位置
			var camTrans = targetCamera.transform;
			camTrans.position = cell.position;
			// 2.设置RT
			// 3.渲染Items
			//int faceMask = 1 << (int)CubemapFace.PositiveZ;
			//targetCamera.RenderToCubemap(targetTexture, faceMask);
			targetCamera.Render ();

			//SaveCellRtToFileName (cellIdx, targetTexture);
		}

		private void SaveCellRtToFileName (int cellIdx, RenderTexture targetTexture)
		{
			if (!System.IO.Directory.Exists ("RT"))
				System.IO.Directory.CreateDirectory ("RT");
			string fileName = string.Format ("RT/RT_{0:D}.png", cellIdx);
			SaveRtToFile (fileName, targetTexture);
		}
	}
}
