 using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
//using MOON;
using NsLib.Utils;

public struct GPUInstancingObj
{
	// Mesh
	public Mesh mesh;
    public ShadowCastingMode shadowCastingMode;
    public bool receiveShadow;
    public int layer;
    public MaterialPropertyBlock propBlock;
	public Material[] sharedMaterials;
	private List<Matrix4x4> matrixList;
	//private List<Vector4> scaleList;
	private List<int> matrixInstanceIdList;
	private Dictionary<int, int> matrixIdxMap;
	//public CommandBufferNode CmdBuffer;
	private static int _globalScaleNameID = Shader.PropertyToID("_Scale");

    public void SetRendererVisible(bool isVisible, IKdTreeCameraClipper clipper) {
        if (matrixInstanceIdList == null || clipper == null)
            return;
        for (int i = 0; i < matrixInstanceIdList.Count; ++i) {
            var instanceID = matrixInstanceIdList[i];
            var r = clipper.GetRendererByInstanceID(instanceID);
            if (r != null && r.gameObject.activeSelf != isVisible)
                r.gameObject.SetActive(isVisible);
        }
    }


    public MaterialPropertyBlock PropertyBlock
	{
		get {
			if (propBlock == null)
				propBlock = new MaterialPropertyBlock ();
			return propBlock;
		}
	}

	public bool IsVaild()
	{
		return (mesh != null) && (sharedMaterials != null) && (sharedMaterials.Length > 0) && (matrixList != null) && (matrixList.Count > 0);
	}

	public void CustomRender(Camera cam)
	{
		if (cam == null || mesh == null || mesh.subMeshCount <= 0 || sharedMaterials == null || sharedMaterials.Length <= 0 || matrixList == null || matrixList.Count <= 0)
			return;
		bool isSingleMesh = mesh.subMeshCount == 1;
		if (!isSingleMesh)
		{
            if (sharedMaterials.Length != mesh.subMeshCount) {
                Debug.LogErrorFormat("[mesh: {0}] materials is not equal subMeshCount~!", mesh.name);
                return;
            }
		}
		#if !UNITY_5_3
		for (int i = 0; i < sharedMaterials.Length; ++i)
		{
			var sharedMaterial = sharedMaterials[i];
			if (sharedMaterial == null)
				continue;
            if (!sharedMaterial.enableInstancing)
                sharedMaterial.enableInstancing = true;
            try {
                if (isSingleMesh)
                    Graphics.DrawMeshInstanced(mesh, 0, sharedMaterial, matrixList, propBlock, shadowCastingMode, receiveShadow, layer, cam) ;
                else
                    Graphics.DrawMeshInstanced(mesh, i, sharedMaterial, matrixList, propBlock, shadowCastingMode, receiveShadow, layer, cam);
            } catch(Exception e) {
                if (sharedMaterial.shader != null)
                    Debug.LogErrorFormat("[mesh: {0} shader: {1}]{2}", mesh.name, sharedMaterial.shader.name, e.ToString());
            }
		}
		#else
		for (int i = 0; i < matrixList.Count; ++i)
		{
			Graphics.DrawMesh(mesh, matrixList[i], sharedMaterial, 0);
		}
		#endif
	}

	private bool GetRendererMatrix(Renderer r, out Matrix4x4 mat)
	{
		if (r == null) {
			mat = Matrix4x4.identity;
			return false;
		}

		var trans = r.transform;
		mat = Matrix4x4.TRS (trans.position, trans.rotation, trans.lossyScale);
		//mat = Matrix4x4.TRS (trans.position, trans.rotation, Vector3.one);
		//mat = trans.localToWorldMatrix;
		return true;
	}

	public void AddMatrix(MeshRenderer renderer)
	{
		if (renderer == null /*|| sharedMaterials != renderer.sharedMaterials*/)
			return;

		int instanceId = renderer.GetInstanceID ();
		if (matrixIdxMap != null && matrixIdxMap.ContainsKey (instanceId))
			return;

		Matrix4x4 mat;
		if (!GetRendererMatrix (renderer, out mat))
			return;



		if (matrixList == null)
			matrixList = new List<Matrix4x4> ();
		if (matrixInstanceIdList == null)
			matrixInstanceIdList = new List<int> ();
		//if (scaleList == null)
		//	scaleList = new List<Vector4> ();
		int idx = matrixList.Count;
		matrixList.Add (mat);
		matrixInstanceIdList.Add (instanceId);
		//Vector4 scale = renderer.transform.lossyScale;
		//scale.w = 1.0f;
		//scaleList.Add (scale);

		if (matrixIdxMap == null)
			matrixIdxMap = new Dictionary<int, int> ();
		matrixIdxMap [instanceId] = idx;

		//RefreshScalePropBlock ();

		InitCommandBuffer ();
	}



	private void InitCommandBuffer()
	{
		/*
		if (CmdBuffer == null) {
			CmdBuffer = new CommandBufferNode ();
		}
		CmdBuffer.InitCommandBuffer (this);
		*/
	}

	/*
	void RefreshScalePropBlock()
	{
		if (scaleList == null)
			return;
		
		if (propBlock == null)
			propBlock = new MaterialPropertyBlock ();
		propBlock.SetVectorArray (_globalScaleNameID, scaleList);
	}
	*/
	public void RemoveMatrix(MeshRenderer renderer)
	{
		if (renderer == null)
			return;

		if (matrixList == null || matrixList.Count <= 0 || matrixIdxMap == null || matrixIdxMap.Count <= 0 || matrixInstanceIdList == null || matrixInstanceIdList.Count <= 0)
			return;

		int instanceId = renderer.GetInstanceID ();
		int idx;
		if (!matrixIdxMap.TryGetValue (instanceId, out idx))
			return;
		matrixIdxMap.Remove (instanceId);
		if (idx < 0 || idx >= matrixList.Count || idx >= matrixInstanceIdList.Count)
			return;

		if (matrixList.Count <= 1) {
			matrixList.Clear ();
			matrixIdxMap.Clear ();
			matrixInstanceIdList.Clear ();
			//scaleList.Clear ();
		} else {
			int removeIdx = matrixList.Count - 1;
            if (idx != removeIdx) {
                Matrix4x4 mat = matrixList[removeIdx];
                matrixList[idx] = mat;
                matrixList.RemoveAt(removeIdx);
                int moveInstanceId = matrixInstanceIdList[removeIdx];
                matrixInstanceIdList[idx] = moveInstanceId;
                matrixInstanceIdList.RemoveAt(removeIdx);
                matrixIdxMap[moveInstanceId] = idx;

			//	Vector4 moveScale = scaleList [removeIdx];
			//	scaleList [idx] = moveScale;
			//	scaleList.RemoveAt (removeIdx);
            } else {
                matrixList.RemoveAt(removeIdx);
                matrixInstanceIdList.RemoveAt(removeIdx);
			//	scaleList.RemoveAt (removeIdx);
            }
		}

		//RefreshScalePropBlock ();

		InitCommandBuffer ();
	}

	/*
	public void Free()
	{
		if (CmdBuffer != null) {
			CmdBuffer.Free();
			CmdBuffer = null;
		}
	}
	*/
}

public class CommandBufferNode
{
	private CommandBuffer m_CmdBuffer = null;
	private bool m_DoCamera = false;

	public CommandBuffer CmdBuffer
	{
		get
		{
			return m_CmdBuffer;
		}
	}

	public void Free()
	{
		RemoveFromCurrCamera ();
		if (m_CmdBuffer != null) {
			m_CmdBuffer.Dispose ();
			m_CmdBuffer = null;
		}
	}

	private void ReomveFromCamera(Camera cam)
	{
		if (cam != null && m_DoCamera)
			cam.RemoveCommandBuffer (CameraEvent.AfterForwardOpaque, m_CmdBuffer);
		m_DoCamera = false;
		if (m_CmdBuffer != null)
			m_CmdBuffer.Clear();
	}

	public void InitCommandBuffer(GPUInstancingObj obj)
	{
		if (KdTreeCameraClipper.IsAppQuit || !obj.IsVaild())
		{
			RemoveFromCurrCamera ();
			return;
		}

		if (m_CmdBuffer == null)
			m_CmdBuffer = new CommandBuffer ();
		else
			m_CmdBuffer.Clear ();

		Camera cam = this.CurrCamera;
		if (cam == null)
			return;
		// 有点问题
		//m_CmdBuffer.DrawMeshInstanced (obj.mesh, 0, obj.sharedMaterial, 0, obj.matrixList);
		cam.AddCommandBuffer (CameraEvent.AfterForwardOpaque, m_CmdBuffer);
		m_DoCamera = true;
	}

	protected Camera CurrCamera
	{
		get
		{
			var clipper = ObjectGPUInstancingMgr.GetInstance ().Clipper;
			if (clipper == null)
				return null;
			return clipper.CurrCamera;
			
		}
	}

	public void RemoveFromCurrCamera()
	{
		var cam = this.CurrCamera;
		ReomveFromCamera (cam);
	}
}

public class GPUInstancingKeyComparser : IEqualityComparer<GPUInstancingKey> {
    public static GPUInstancingKeyComparser Default = new GPUInstancingKeyComparser();

    public bool Equals(GPUInstancingKey x, GPUInstancingKey y) {
        return x.Equals(y);
    }

    public int GetHashCode(GPUInstancingKey obj) {
        return obj.GetHashCode();
    }
}

public struct GPUInstancingKey : IEquatable<GPUInstancingKey> {
    public int meshInstanceID;
    public int SharedMaterials;

    public bool Equals(GPUInstancingKey other) {
        return this == other;
    }

    public override bool Equals(object obj) {
        if (obj == null)
            return false;
        if (GetType() != obj.GetType())
            return false;
        if (obj is GPUInstancingKey) {
            GPUInstancingKey other = (GPUInstancingKey)obj;
            return Equals(other);
        } else
            return false;
    }

    public override int GetHashCode() {
        return meshInstanceID;
    }

    public static bool operator ==(GPUInstancingKey a, GPUInstancingKey b) {
        return (a.meshInstanceID == b.meshInstanceID) && (a.SharedMaterials == b.SharedMaterials);
    }

    public static bool operator !=(GPUInstancingKey a, GPUInstancingKey b) {
        return !(a == b);
    }
}

// 带KdTree的Clipper裁剪(只考虑透视摄像机)
[RequireComponent(typeof(Camera))]
public class KdTreeCameraClipper : MonoBehaviour, IKdTreeCameraClipper {
	private bool m_IsInited = false;
	private Dictionary<int, Renderer> m_ObjectsMap = null;
	private List<int> m_ObjectsList = null;
	//private Transform m_Trans = null;
	private Camera m_Cam = null;
	//局部坐标系下的
	private VisibleNode m_ChgQueue = null;
	private Dictionary<GPUInstancingKey, GPUInstancingObj> m_GPUInstancingMap = new Dictionary<GPUInstancingKey, GPUInstancingObj>(GPUInstancingKeyComparser.Default);
	private kdTreeCameraSelector m_Selector = null;

	public Renderer m_TemplateObj = null;
	[Range(0, 1023)]
	public int m_CreatorObjCount = 0;
	private List<Renderer> m_TargetObjects = null;

	public bool m_UseThread = false;
	//public float m_Radius = 10f;
	#if UNITY_EDITOR
	public int VisibleCount = 0;
    public int KdTreeVisibleCount = 0;
	public int VisibleNodePoolCount = 0;
	public int GPUInstancingGroupCount = 0;
	#endif
	public int PerMaxVisbileCount = -1;
	public bool AutoSetVisible = true;
	public bool OpenGPUInstancing = false;
    public float m_AspectScale = 1.0f;
    public bool ShowBoudingBox = false;

	public static bool IsAppQuit = false;

	public Camera CurrCamera {
		get {
			if (m_Selector != null) {
				return m_Selector.TargetCamera;
			}
			return m_Cam;
		}
	}

	#if UNITY_5_3
	void OnLevelWasLoaded(int level)
	{
		Clear ();
	}
	#endif

	private bool m_IsInitRegEvents = false;
	void RegisterEvents()
	{
		#if !UNITY_5_3
		if (!m_IsInitRegEvents)
		{
			m_IsInitRegEvents = true;
			SceneManager.sceneLoaded += OnSceneLoading;
			SceneManager.activeSceneChanged += OnActiveSceneChanged;
		}
		#endif
	}

    public Renderer GetRendererByInstanceID(int instanceID) {
        if (m_ObjectsMap == null || m_ObjectsMap.Count <= 0)
            return null;
        Renderer ret;
        if (!m_ObjectsMap.TryGetValue(instanceID, out ret))
            ret = null;
        return ret;
    }


    void OnActiveSceneChanged(Scene oldScene, Scene newScene)
	{
		bool isMustRefresh = (m_VisibleSceneMap != null) && 
								((oldScene != null && 
				#if UNITY_2019
								m_VisibleSceneMap.Contains (oldScene.handle) 
				#else
				m_VisibleSceneMap.Contains(oldScene.path)
				#endif
								)|| (newScene != null && 
					#if UNITY_2019
								m_VisibleSceneMap.Contains(newScene.handle)
				#else
				m_VisibleSceneMap.Contains(newScene.path)
					#endif
								));
		if (isMustRefresh)
			Refresh ();
	}

	void OnSceneLoading(Scene s, LoadSceneMode m)
	{
		if (s != null && m == LoadSceneMode.Single) {
			Clear ();
		}
	}

	void UnRegisterEvents()
	{
		#if !UNITY_5_3
		if (m_IsInitRegEvents)
		{
			m_IsInitRegEvents = false;
			SceneManager.sceneLoaded -= OnSceneLoading;
			SceneManager.activeSceneChanged -= OnActiveSceneChanged;
		}
		#endif
	}

	void Awake()
	{
		//m_Trans = this.transform;
		m_Cam = GetComponent<Camera> ();
		//if (m_Cam != null)
		//	m_Radius = m_Cam.farClipPlane;
		RegisterEvents();
	}

	void CreateTemplates()
	{
		#if UNITY_EDITOR
		if (m_TemplateObj == null)
			return;

		var templateGameObj = m_TemplateObj.gameObject;
		if (templateGameObj.activeSelf)
			templateGameObj.SetActive(false);

		if (m_CreatorObjCount <= 0)
			return;

		if (m_TargetObjects == null)
			m_TargetObjects = new List<Renderer>(m_CreatorObjCount);
		else
		{
			m_TargetObjects.Clear();
			m_TargetObjects.Capacity = m_CreatorObjCount;
		}

		var root = new GameObject("KdTreeObjs");
		var rootTrans = root.transform;
		rootTrans.localPosition = Vector3.zero;
		rootTrans.localScale = Vector3.one;
		rootTrans.localRotation = Quaternion.identity;
		int cnt = (int)Mathf.Sqrt(m_CreatorObjCount);
        Vector3 localScale = templateGameObj.transform.localScale;
        Quaternion quat = templateGameObj.transform.localRotation;
        for (int i = 0; i < m_CreatorObjCount; ++i)
		{
			var instaneObj = GameObject.Instantiate(templateGameObj);
			instaneObj.SetActive(false);
			var trans = instaneObj.transform;
			trans.SetParent(rootTrans, false);
			trans.localScale = localScale;
			trans.localRotation = quat;
			int r = (int)i/cnt;
			int c = i % cnt;
			Vector3 pt = new Vector3(c * 5, 0, r * 5);
			trans.localPosition = pt;

			var renderer = instaneObj.GetComponent<Renderer>();
			if (renderer != null)
				m_TargetObjects.Add(renderer);
			else
			{
				// 一般不会执行
				GameObject.Destroy(instaneObj);
			}
		}

		#endif
	}


	void Start()
	{
		CreateTemplates ();
		ReBuild (m_TargetObjects);
	}

	public bool IsThisCamera (Camera cam)
	{
		if (cam == null)
			return false;
		var c = this.CurrCamera;
		return c == cam;
	}

	/*
	public void ReBuild(List<util_combine_sub_mesh> combine)
	{
		if (!m_IsInited) {
			if (combine != null && combine.Count > 0) {
				List<Renderer> lst = null;
				HashSet<int> hash = null;
				for (int i = 0; i < combine.Count; ++i) {
					var subMesh = combine [i];
					if (subMesh == null)
						continue;
					var groups = subMesh._lst_group;
					if (groups != null) {
						for (int j = 0; j < groups.Count; ++j) {
							var group = groups [j];
							if (group == null)
								continue;
							var datas = group._lst_data;
							if (datas != null) {
								// 构造GPU Instancing Group
								for (int k = 0; k < datas.Count; ++k) {
									var data = datas [k];
									if (data == null)
										continue;
									if (data._lst_data != null) {
										for (int l = 0; l < data._lst_data.Count; ++l) {
											var subData = data._lst_data [l];
											if (subData == null)
												continue;
											if (subData._rd != null) {
												if (lst == null)
													lst = new List<Renderer> ();
												if (hash == null)
													hash = new HashSet<int> ();
												int instanceID = subData._rd.GetInstanceID ();
												if (!hash.Contains (instanceID)) {
													lst.Add (subData._rd);
													hash.Add (instanceID);
												}
											}
											// 添加GROUP

										}
									}
								}
							}
						}
					}
				}

				if (lst != null) {
					ReBuild (lst);
				}

			}
		} else {
			Debug.LogError ("已经构建过了KdTree");
		}
	}*/

	public static bool IsVaildInstancing(Renderer r)
	{
		if (r == null)
			return false;
		Vector3 scale = r.transform.lossyScale;
		int negCnt = 0;
		if (scale.x < 0)
			++negCnt;
		if (scale.y < 0)
			++negCnt;
		if (scale.z < 0)
			++negCnt;
		return (negCnt == 0) || (negCnt == 2);
	}

	public static Dictionary<int, int> GetMeshRendererMeshCntMap(IList<Renderer> lst)
	{
		Dictionary<int, int> ret = null;
		if (lst != null) {
			for (int i = 0; i < lst.Count; ++i) {
				var meshR = lst [i] as MeshRenderer;
				if (meshR == null)
					continue;
				var filter = meshR.GetComponent<MeshFilter> ();
				if (filter == null || filter.sharedMesh == null)
					continue;
				int meshInstanceID = filter.sharedMesh.GetInstanceID ();
				int cnt;
				if (ret != null) {
					if (!ret.TryGetValue (meshInstanceID, out cnt))
						cnt = 0;
				} else {
					cnt = 0;
					ret = new Dictionary<int, int> ();
				}

				++cnt;
				ret [meshInstanceID] = cnt;

			}
		}
		return ret;
	}

	public void ReBuild(IList<Renderer> targetObjects, bool isAddMode = false)
	{
		if (!m_IsInited || isAddMode) {
			if (targetObjects != null && targetObjects.Count > 0) {
				ObjectGPUInstancingMgr.GetInstance ().RegisterClipper (this);
				m_IsInited = true;

				if (m_ObjectsMap == null)
					m_ObjectsMap = new Dictionary<int, Renderer> (targetObjects.Count);
				else {
					if (!isAddMode)
						m_ObjectsMap.Clear ();
				}
				if (m_ObjectsList == null)
					m_ObjectsList = new List<int> (targetObjects.Count);
				else {
					if (!isAddMode) {
						m_ObjectsList.Clear ();
						m_ObjectsList.Capacity = targetObjects.Count;
					}
				}

                bool isNew = false;
                for (int i = 0; i < targetObjects.Count; ++i) {
					var item = targetObjects [i];
					if (item == null)
						continue;

					int instanceId = item.GetInstanceID ();
					if (!m_ObjectsMap.ContainsKey (instanceId)) {
						m_ObjectsMap.Add (instanceId, item);
						m_ObjectsList.Add (instanceId);
                        isNew = true;

                    }

                    if (this.AutoSetVisible || (this.OpenGPUInstancing && ObjectGPUInstancingMgr.SupportGPUInstancing)) {
                        var gameObj = item.gameObject;
                        if (gameObj.activeSelf)
                            gameObj.SetActive(false);
                    }
				}

				if (isAddMode && isNew)
					AddBuildClear ();

				ObjectGPUInstancingMgr.GetInstance ().Build (m_UseThread);
			}
		}	
	}

	public int ObjectsCount
	{
		get
		{
			if (m_ObjectsMap == null)
				return 0;
			return m_ObjectsMap.Count;
		}
	}

	public int GetInstanceId(int index)
	{
		if (m_ObjectsList == null || index >= m_ObjectsList.Count)
			return 0;
		return m_ObjectsList [index];
	}

    public static BoundingSphere GetBoundingSphere(Bounds bound) {
        var ret = new BoundingSphere();
        var extents = bound.extents;
        //var scale = obj.transform.lossyScale;
        Vector3 v1 = new Vector3(extents.x, 0, 0);
        Vector3 v2 = new Vector3(0, extents.y, 0);
        Vector3 v3 = new Vector3(0, 0, extents.z);
        Vector3 v = v1 + v2 + v3;
        ret.position = bound.center;
        ret.radius = v.magnitude;
        return ret;
    }


    public static BoundingSphere GetBoundingSphere (Renderer obj)
	{
		if (obj == null)
			return new BoundingSphere ();
        var ret = GetBoundingSphere(obj.bounds);
        return ret;
	}

	public bool GetBoundSphereByInstanceId (int instanceId, out BoundingSphere boudingSphere)
	{
		if (m_ObjectsMap == null || m_ObjectsMap.Count <= 0) {
			boudingSphere = new BoundingSphere ();
			return false;
		}

		Renderer obj;
		bool ret = m_ObjectsMap.TryGetValue (instanceId, out obj) && obj != null;
		if (ret) {
			boudingSphere = GetBoundingSphere (obj);
		} else {
			boudingSphere = new BoundingSphere ();
		}

		return ret;
	}

	public KdCameraInfo CamInfo
	{
		get {
			var cam = this.CurrCamera;
			if (cam == null)
				return new KdCameraInfo ();
			KdCameraInfo info = new KdCameraInfo ();
            /*
			info.near = cam.nearClipPlane;
			info.far = cam.farClipPlane;
			info.fieldOfView = cam.fieldOfView;
			info.aspect = ((float)Screen.width) / ((float)Screen.height) * m_AspectScale;

			var trans = cam.transform;
			info.position = trans.position;
			info.lookAt = trans.forward;
			info.up = trans.up;
			info.right = trans.right;
            */
            info.position = cam.transform.position;
            info.cullMatrix = cam.cullingMatrix;
            info.far = cam.farClipPlane;
			return info;
		}
	}

	public void ForEachObjects (Action<Renderer> onOjbectsCallBack)
	{
		if (onOjbectsCallBack == null || m_ObjectsMap == null || m_ObjectsMap.Count <= 0)
			return;
		var iter = m_ObjectsMap.GetEnumerator ();
		while (iter.MoveNext ()) {
			var gameObj = iter.Current.Value;
			if (gameObj == null)
				continue;
			onOjbectsCallBack (gameObj);
		}
		iter.Dispose ();
	}

	void DestoryInstancingMap(bool doDisable = false)
	{
        if (doDisable)
            AutoDisableObjects();
        /*
		var iter = m_GPUInstancingMap.GetEnumerator ();
		while (iter.MoveNext ()) {
			if (iter.Current.Value.CmdBuffer != null)
				iter.Current.Value.Free ();
		}
		iter.Dispose ();
		*/
        m_GPUInstancingMap.Clear ();
	}

    void ClearChgQueue() {
        while (m_ChgQueue != null) {
            var next = m_ChgQueue.NextNode;
            m_ChgQueue.Dispose();
            m_ChgQueue = next;
        }
    }

	private void AddBuildClear()
	{
        //DestoryInstancingMap ();
        ClearChgQueue();

        #if UNITY_EDITOR
        this.VisibleCount = 0;
		this.KdTreeVisibleCount = 0;
		this.GPUInstancingGroupCount = 0;
		#endif
	}

    void DisableObjectList() {
        if (m_GPUInstancingMap == null || m_GPUInstancingMap.Count <= 0)
            return;
        var iter = m_GPUInstancingMap.GetEnumerator();
        while (iter.MoveNext()) {
            var r = iter.Current.Value;
            r.SetRendererVisible(false, this);
        }
        iter.Dispose();
    }

    void AutoDisableObjects() {
        if (!this.OpenGPUInstancing && this.AutoSetVisible)
            DisableObjectList();
    }

	// 多场景加载
	private void Refresh()
	{
		ObjectGPUInstancingMgr.GetInstance ().Refresh ();
        ClearChgQueue();
        ClearVisibleSceneMap(); 
        DestoryInstancingMap (true);
	}

	public void Clear()
	{
		if (m_IsInited) {
			ClearVisibleSceneMap ();
			DestoryInstancingMap ();
			ObjectGPUInstancingMgr.GetInstance ().ClearClipper (this);
			if (m_ObjectsMap != null)
				m_ObjectsMap.Clear ();
			if (m_ObjectsList != null)
				m_ObjectsList.Clear ();
            ClearChgQueue();
            m_Selector = null;

			#if UNITY_EDITOR
			this.VisibleCount = 0;
            this.KdTreeVisibleCount = 0;
			this.GPUInstancingGroupCount = 0;
			#endif
			m_IsInited = false;
		}
	}

#if UNITY_EDITOR
    void OnDrawGizmos() {
        // Draw gizmos to show the culling sphere.
        //画出显示筛除剔除球形的小图标
        if (ShowBoudingBox) {
           
            if (m_ObjectsMap != null) {
                var activityObj = UnityEditor.Selection.activeObject as GameObject;
                if (activityObj == null)
                    return;
                var renderer = activityObj.GetComponent<Renderer>();
                if (renderer == null || !m_ObjectsMap.ContainsKey(renderer.GetInstanceID()))
                    return;

                Gizmos.color = Color.yellow;
                BoundingSphere spere = GetBoundingSphere(renderer);
                Gizmos.DrawWireSphere(spere.position, spere.radius);
            }
        }
    }
#endif

    void OnApplicationQuit()
	{
		IsAppQuit = true;
		UnRegisterEvents ();
		Clear ();
	}

	void OnDestroy() 
	{
		UnRegisterEvents ();
		Clear ();
	}

	void CheckVisible()
	{
		#if UNITY_EDITOR
		this.VisibleCount = ObjectGPUInstancingMgr.GetInstance ().VisibleCount;
        this.KdTreeVisibleCount = ObjectGPUInstancingMgr.GetInstance().KdTreeVisibleCount;
		this.VisibleNodePoolCount = NsLib.Utils.AbstractPool<VisibleNode>.PoolItemCount;
		this.GPUInstancingGroupCount = m_GPUInstancingMap.Count;
		#endif

		// 处理循环数据
		if (m_ChgQueue == null)
			ObjectGPUInstancingMgr.GetInstance().UptoVisibleQueue(ref m_ChgQueue);

		printQueue ();
	}

	void ClearVisibleSceneMap()
	{
		if (m_VisibleSceneMap != null)
			m_VisibleSceneMap.Clear ();
	}
	#if !UNITY_2019
	private HashSet<string> m_VisibleSceneMap = null;
	#else
	private HashSet<int> m_VisibleSceneMap = null;
	#endif
	bool IsVaildScene(Renderer r)
	{
		if (r == null || r.gameObject == null)
			return false;
		var scene = r.gameObject.scene;

		if (scene == null)
			return false;
        
		if (m_VisibleSceneMap == null) {
			#if UNITY_2019
			m_VisibleSceneMap = new HashSet<int> ();
			m_VisibleSceneMap.Add (scene.handle);
			#else
			m_VisibleSceneMap = new HashSet<string>();
			m_VisibleSceneMap.Add(scene.path);
			#endif
		} else 
			#if UNITY_2019
		if (!m_VisibleSceneMap.Contains(scene.handle)){
			m_VisibleSceneMap.Add (scene.handle);
		}
			#else
			if (!m_VisibleSceneMap.Contains(scene.path)){
				m_VisibleSceneMap.Add (scene.path);
			}
			#endif

		var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene ();
		bool ret = scene == activeScene;
		return ret;
	}

	void ProcessGPUInstancingVisibleChanged(Renderer r, bool isVisible)
	{
		if (r == null)
			return;

		MeshRenderer meshRenderer = r as MeshRenderer;
		if (meshRenderer == null)
			return;
		
		//var mats = meshRenderer.sharedMaterials;
		//if (mats == null)
		//	return;

        MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
        if (filter == null)
            return;
        Mesh mesh = filter.sharedMesh;
        if (mesh == null)
            return;

		bool isVaildScene = IsVaildScene (r);
        if (!isVaildScene)
            return;

        int instanceId = mesh.GetInstanceID();
        GPUInstancingKey key = new GPUInstancingKey();
        key.meshInstanceID = instanceId;
        if (meshRenderer.sharedMaterial != null)
            key.SharedMaterials = meshRenderer.sharedMaterial.GetInstanceID();
        else
            key.SharedMaterials = 0;
        GPUInstancingObj obj;
		if (!m_GPUInstancingMap.TryGetValue (key, out obj)) {
			if (!isVisible)
				return;
			
			obj = new GPUInstancingObj ();
			obj.mesh = mesh;
			obj.sharedMaterials = meshRenderer.sharedMaterials;
            obj.receiveShadow = meshRenderer.receiveShadows;
            obj.shadowCastingMode = meshRenderer.shadowCastingMode;
            obj.layer = meshRenderer.gameObject.layer;
			//meshRenderer.SetPropertyBlock (obj.propBlock);

#if !UNITY_EDITOR
            if (mesh.isReadable)
				mesh.UploadMeshData (true);
#endif
        }

		if (isVisible) {
			obj.AddMatrix (meshRenderer);
		} else {
			obj.RemoveMatrix (meshRenderer);
		}
		m_GPUInstancingMap [key] = obj;

	}

	void OnRenderVisibleStateChanged(Renderer r, bool isVisible)
	{
		if (OpenGPUInstancing) {
			// 缩放为负数的为奇数（即，XYZ，只有一个为负数的情况，会导致Shader CULL BACK或者CULL FRONT显示错误，除非CULL OFF）会导致DrawInstance错误
			if (IsVaildInstancing (r))
				ProcessGPUInstancingVisibleChanged (r, isVisible);
			else {
				if (r.gameObject.activeSelf != isVisible)
					r.gameObject.SetActive (isVisible);
			}
		}
	}

	void printQueue()
	{
		if (m_ChgQueue == null) {
			// 无数据变化
			return;
		}

		bool isNoLimit = PerMaxVisbileCount <= 0;
		int cnt = 0;
		while (m_ChgQueue != null) {
			var next = m_ChgQueue.NextNode;
			Renderer r;
			if (!m_ObjectsMap.TryGetValue (m_ChgQueue.InstanceId, out r))
				r = null;
			if (r != null) {
				OnRenderVisibleStateChanged (r, m_ChgQueue.isVisible);
				if (AutoSetVisible) {
					if (r.gameObject.activeSelf != m_ChgQueue.isVisible)
						r.gameObject.SetActive (m_ChgQueue.isVisible);
				}
				//Debug.LogFormat ("{0} {1}", r.gameObject.name, n.isVisible? "变可见": "变非可见");
			}
			m_ChgQueue.Dispose ();
			m_ChgQueue = next;

			if (!isNoLimit) {
				++cnt;
				if (cnt >= PerMaxVisbileCount)
					break;
			}
		}
	}

	void DrawGPUInstancings()
	{
		if (!OpenGPUInstancing)
			return;
        var cam = this.CurrCamera;
        if (cam == null)
            return;
		var iter = m_GPUInstancingMap.GetEnumerator ();
		while (iter.MoveNext ()) {
			var obj = iter.Current.Value;
			obj.CustomRender (cam);
		}
		iter.Dispose ();
	}

	private void CheckUpdate()
	{
		ObjectGPUInstancingMgr.GetInstance ().UpdateClipper ();
		CheckVisible ();

		DrawGPUInstancings ();
	}

	void Update()
	{
		if (m_IsInited && m_Selector == null) {
			CheckUpdate ();
		}
	}

	public void ClearSelector(kdTreeCameraSelector selector)
	{
		if (m_Selector == selector)
			m_Selector = null;
	}

	void OnEnable()
	{
		if (m_Selector != null) {
			m_Selector.enabled = false;
			m_Selector = null;
		}
		if (m_Cam != null && !m_Cam.enabled)
			m_Cam.enabled = true;
	}

	void OnDisable()
	{
		if (m_Cam != null && m_Cam.enabled)
			m_Cam.enabled = false;
	}

    public void SetCameraSelector(kdTreeCameraSelector selector) {
        if (m_Selector != selector) {
          //  DestoryInstancingMap(true);
            m_Selector = selector;
        }
    }

    public void CameraSelectorUpdate (kdTreeCameraSelector selector)
	{
		if (selector == null)
			return;
		if (m_IsInited) {
			if (m_Selector != selector) {
				if (m_Selector != null)
					m_Selector.enabled = false;
				m_Selector = selector;
              //  DestoryInstancingMap();

            }
			/*
			if (gameObject.activeSelf)
				gameObject.SetActive (false);
				*/

			if (enabled)
				enabled = false;

			CheckUpdate ();
		}
	}
}
