#define _USE_WORLD_AXIS
#if _USE_WORLD_AXIS
#define _Use_World_SimpleMode
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NsLib.Utils;

public interface IKdTreeCameraClipper
{
	void ForEachObjects (Action<Renderer> onOjbectsCallBack);
	int ObjectsCount {
		get;
	}

	KdCameraInfo CamInfo
	{
		get;
	}

	bool IsThisCamera (Camera cam);
	int GetInstanceId(int index);
	bool GetBoundSphereByInstanceId (int instanceId, out BoundingSphere boudingSphere);

	Camera CurrCamera {
		get;
	}

	void CameraSelectorUpdate (kdTreeCameraSelector selector);
	void ClearSelector(kdTreeCameraSelector selector);
    void SetCameraSelector(kdTreeCameraSelector selector);

    void ReBuild(IList<Renderer> targetObjects, bool isAddMode = false);
    Renderer GetRendererByInstanceID(int instanceID);
}

// KDTree和摄影机裁剪线程
public class KDTreeCameraClipThread : CustomThread
{
	#if UNITY_EDITOR
	#if !UNITY_5_3
	//private UnityEngine.Profiling.CustomSampler m_Sampler = null;
	#endif
	#endif

	public KDTreeCameraClipThread()
	{
		#if !UNITY_5_3
		//m_Sampler = UnityEngine.Profiling.CustomSampler.Create ("KdTreeThreadSampler");
		#endif
	}

	protected override void OnFree (bool isManual)
	{
		#if !UNITY_5_3
		//m_Sampler = null;
		#endif
		base.OnFree (isManual);
	}

	// 多线程执行
	protected override void Execute ()
	{
		#if UNITY_EDITOR
		#if !UNITY_5_3 && !UNITY_5_6
		//m_Sampler.Begin();
		UnityEngine.Profiling.Profiler.BeginThreadProfiling("KdTreeThreadGroup", "KdTreeThread");
		#else
		//UnityEngine.Profiler.BeginSample("KdTreeThreadSampler");
		#endif
		#endif
		ObjectGPUInstancingMgr.GetInstance ()._ThreadRun ();
		#if UNITY_EDITOR
		#if !UNITY_5_3 && !UNITY_5_6
		UnityEngine.Profiling.Profiler.EndThreadProfiling();
		//m_Sampler.End();

		#else
		//UnityEngine.Profiler.EndSample();
		#endif
		#endif
	}
}

public enum KdTreeStatus
{
	None = 0,
	WaitRebuild,
	Rebuilded
}

public struct KdTreeObj
{
	public int InstanceId;
	public BoundingSphere boundSphere;
}

public class VisibleNode: PoolNode<VisibleNode>
{
	public int InstanceId = 0;
	public VisibleNode NextNode = null;
	public bool isVisible = false;
	protected override void OnFree ()
	{
		InstanceId = 0;
		NextNode = null;
		isVisible = false;
	}
}

public enum VisibleQueueStatus
{
	WaitQuery, // 等待查询状态，交换后等待
	WaitChange, // 等待队列交换出去
	Refresh,      // 重新找
	Error
}

#if _USE_WORLD_AXIS
public struct KdCameraPanels
{
	public Vector4 left, right, top, bottom, near, far;
#if !_Use_World_SimpleMode
	public Vector4 lb, lt, rb, rt;
	public Vector4 nb, nt, nl, nr;
	public Vector4 fb, ft, fl, fr;
	public Vector3 a, b, c, d, e, f, g, h;
#endif
}
#endif

public struct KdCameraInfo
{
    public float far;
    public Vector3 position;
    public Matrix4x4 cullMatrix;

    /*
	public float near, far;
	public float fieldOfView;
	public float aspect;// 宽高比
	public Vector3 position;
	public Vector3 lookAt;
	public Vector3 up;
	public Vector3 right;

	public Matrix4x4 CalcProjMatrix()
	{
		Matrix4x4 ret = Matrix4x4.Perspective (fieldOfView , aspect, near, far);
		return ret;
	}

	public Matrix4x4 CalcViewMatrix()
	{
		#if UNITY_5_3 || UNITY_5_6
		Matrix4x4 ret = new Matrix4x4 ();
		ret.m00 = right.x; ret.m01 = right.y; ret.m02 = right.z;
		ret.m10 = up.x; ret.m11 = up.y; ret.m12 = up.z;
		ret.m20 = -lookAt.x; ret.m21 = -lookAt.y; ret.m22 = -lookAt.z;
		ret.m30 = 0; ret.m31 = 0; ret.m32 = 0; ret.m33 = 1;
		Matrix4x4 trans = new Matrix4x4();
		trans.SetTRS(-position, Quaternion.identity, Vector3.one);
		#else
		Matrix4x4 ret = new Matrix4x4 (
		new Vector4(right.x, right.y, right.z, 0),
		new Vector4(up.x, up.y, up.z, 0),
		new Vector4(-lookAt.x, -lookAt.y, -lookAt.z, 0),
		new Vector4(0, 0, 0, 1)
		);
		Matrix4x4 trans = Matrix4x4.Translate (-position);
		#endif
		ret = ret * trans;
		return ret;
	}

	public Matrix4x4 CalcViewProjMatrix()
	{
		Matrix4x4 ret = CalcProjMatrix () * CalcViewMatrix();
		return ret;
	}

	public float nearHeight
	{
		get {
			float halfAngle = fieldOfView / 2.0f * Mathf.Deg2Rad;
			float ret = 2.0f * Mathf.Tan (halfAngle) * near;
			return ret;
		}
	}

	public float farHeight
	{
		get
		{
			float halfAngle = fieldOfView / 2.0f * Mathf.Deg2Rad;
			float ret = 2.0f * Mathf.Tan (halfAngle) * far;
			return ret;
		}
	}

	public float nearWidth
	{
		get {
			float ret = aspect * nearHeight;
			return ret;
		}
	}

	public float farWidth
	{
		get {
			float ret = aspect * farHeight;
			return ret;
		}
	}

	public Vector3 nearCenter
	{
		get {
			Vector3 ret = position + lookAt * near;
			return ret;
		}
	}

	public Vector3 farCenter
	{
		get
		{
			Vector3 ret = position + lookAt * far;
			return ret;
		}
	}

	public Vector3 nearFarCenter
	{
		get {
			Vector3 ret = (nearCenter + farCenter) / 2.0f;
			return ret;
		}
	}

	// front back
	public Vector4 FBCenterPlane
	{
		get {
			Vector3 p = this.nearFarCenter;
			Vector3 n = -this.lookAt;
			float d = -n.x * p.x - n.y * p.y - n.z * p.z;
			Vector4 ret = new Vector4 (n.x, n.y, n.z, d);
			return ret;
		}
	}

	// top bottom
	public Vector4 TBCenterPlane
	{
		get {
			Vector3 p = this.nearFarCenter;
			Vector3 n = this.up;
			float d = -n.x * p.x - n.y * p.y - n.z * p.z;
			Vector4 ret = new Vector4 (n.x, n.y, n.z, d);
			return ret;
		}
	}

	public Vector3 Right
	{
		get
		{
			//return Vector3.Cross (this.up, this.lookAt);
			return right;
		}
	}

	// left right
	public Vector4 LRCenterPanel
	{
		get {
			Vector3 p = this.nearFarCenter;
			Vector3 right = this.Right;
			Vector3 n = -right;
			float d = -n.x * p.x - n.y * p.y - n.z * p.z;
			Vector4 ret = new Vector4 (n.x, n.y, n.z, d);
			return ret;
		}
	}

	public Vector4 nearPlane
	{
		get {
			Vector3 p = this.nearCenter;
			Vector3 n = -this.lookAt;
			float d = -n.x * p.x - n.y * p.y - n.z * p.z;
			Vector4 ret = new Vector4 (n.x, n.y, n.z, d);
			return ret;
		}
	}

	public Vector4 farPlane
	{
		get {
			Vector3 p = this.farCenter;
			Vector3 n = -this.lookAt;
			float d = -n.x * p.x - n.y * p.y - n.z * p.z;
			Vector4 ret = new Vector4 (n.x, n.y, n.z, d);
			return ret;
		}
	}
    */

	// 点到平面距离
	public static float PtPlaneDistance(Vector4 plane, Vector3 pt)
	{
		float d = Mathf.Abs (pt.x * plane.x + pt.y * plane.y + pt.z * plane.z + plane.w) / Mathf.Sqrt (plane.x * plane.x + plane.y * plane.y + plane.z * plane.z);
		return d;
	}

	public static float PtPlaneDistance(float PtPlaneValue, Vector4 plane)
	{
		float d = Mathf.Abs (PtPlaneValue) / Mathf.Sqrt (plane.x * plane.x + plane.y * plane.y + plane.z * plane.z);
		return d;
	}

	#if _USE_WORLD_AXIS
	private static bool PtInCamera(Vector3 pt, KdCameraPanels planes)
	{
		bool ret = (PtPlaneValue(pt, planes.left) <= 0) && (PtPlaneValue (pt, planes.right) <= 0) && (PtPlaneValue(pt, planes.top) <= 0) && 
					(PtPlaneValue(pt, planes.bottom) <= 0) && (PtPlaneValue(pt, planes.near) <= 0) && (PtPlaneValue(pt, planes.far) <= 0);
		return ret;
	}
	#endif

	public static bool IsBoundingSphereIn(BoundingSphere spere, Matrix4x4 viewProjMatrix, Vector4 nearPlane)
	{
		Vector3 projPt = viewProjMatrix.MultiplyPoint(spere.position);
		float left = projPt.x - spere.radius;
		float right = projPt.x + spere.radius;
		if (right <= -1 || left >= 1)
			return false;

		float top = projPt.y + spere.radius;
		float bottom = projPt.y - spere.radius;
		if (bottom >= 1 || top <= -1)
			return false;

		var pt = spere.position;
		var distance = PtPlaneValue (pt, nearPlane);
		if (distance > 0) {
			distance = PtPlaneDistance (distance, nearPlane);
			return distance <= spere.radius;
		}

		float front = projPt.z + spere.radius;
		float back = projPt.z - spere.radius;
		if (back >= 1 || front <= 0)
			return false;

		return true;
	}

	#if _USE_WORLD_AXIS

	// 八分体
	// 判断包围球是否在摄影机内
	public bool IsBoundingSphereIn(BoundingSphere spere, KdCameraPanels planes)
	{
	#if _Use_World_SimpleMode
		float r = PtPlaneValue(spere.position, planes.left);
		if (r < -spere.radius)
			return false;
		r = PtPlaneValue(spere.position, planes.right);
		if (r < -spere.radius)
			return false;
		r = PtPlaneValue(spere.position, planes.top);
		if (r < -spere.radius)
			return false;
		r = PtPlaneValue(spere.position, planes.bottom);
		if (r < -spere.radius)
			return false;
		r = PtPlaneValue(spere.position, planes.near);
		if (r < -spere.radius)
			return false;
		r = PtPlaneValue(spere.position, planes.far);
		if (r < -spere.radius)
			return false;
		return true;
	#else
		int axisSpace = GetPtPlanelAxis (spere.position, planes);
		switch (axisSpace) {
		case 1:
			{
				if (PtPlaneValue (spere.position, planes.near) <= 0 && PtPlaneValue (spere.position, planes.left) <= 0 && PtPlaneValue (spere.position, planes.top) <= 0)
					return true;

				bool nl = CheckN3Plane (planes.nl, spere);
				bool nt = CheckN3Plane (planes.nt, spere);
				bool lt = CheckN3Plane (planes.lt, spere);

				float r1 = PtPlaneDistance (planes.near, spere.position);
				if (r1 <= spere.radius) {
					if (nl || nt)
						return true;
				}
				r1 = PtPlaneDistance (planes.left, spere.position);
				if (r1 <= spere.radius) {
					if (nl || lt)
						return true;
				}
				r1 = PtPlaneDistance (planes.top, spere.position);
				if (r1 <= spere.radius) {
					if (lt || nt)
						return true;
				}

				return false;
			}
			break;
		case 2:
			{
				if ((PtPlaneValue (spere.position, planes.near) <= 0) && (PtPlaneValue (spere.position, planes.right) <= 0) && (PtPlaneValue (spere.position, planes.top) <= 0))
					return true;

				bool nr = CheckN3Plane(planes.nr, spere);
				bool nt = CheckN3Plane(planes.nt, spere);
				bool rt = CheckN3Plane (planes.rt, spere);

				float r2 = PtPlaneDistance (planes.near, spere.position);
				if (r2 <= spere.radius) {
					if (nr || nt)
						return true;
				}
				r2 = PtPlaneDistance (planes.right, spere.position);
				if (r2 <= spere.radius) {
					if (nr || rt)
						return true;
				}
				r2 = PtPlaneDistance (planes.top, spere.position);
				if (r2 <= spere.radius) {
					if (nt || rt)
						return true;
				}
				return false;
			}
			break;
		case 3:
			{
				if ((PtPlaneValue (spere.position, planes.near) <= 0) && (PtPlaneValue (spere.position, planes.left) <= 0) && (PtPlaneValue (spere.position, planes.bottom) <= 0))
					return true;

				bool lb = CheckN3Plane(planes.lb, spere);
				bool nl = CheckN3Plane (planes.nl, spere);;
				bool nb = CheckN3Plane (planes.nb, spere);

				float r3 = PtPlaneDistance (planes.left, spere.position);
				if (r3 <= spere.radius) {
					if (lb || nl)
						return true;
				}
				r3 = PtPlaneDistance (planes.bottom, spere.position);
				if (r3 <= spere.radius) {
					if (lb || nb)
						return true;
				}
				r3 = PtPlaneDistance (planes.near, spere.position);
				if (r3 <= spere.radius) {
					if (nl || nb)
						return true;
				}
				return false;
			}
			break;
		case 4:
			{
				if ((PtPlaneValue (spere.position, planes.near) <= 0) && (PtPlaneValue (spere.position, planes.right) <= 0) && (PtPlaneValue (spere.position, planes.bottom) <= 0))
					return true;

				bool rb = CheckN3Plane (planes.rb, spere);
				bool nr = CheckN3Plane (planes.nr, spere);
				bool nb = CheckN3Plane (planes.nb, spere);

				float r4 = PtPlaneDistance (planes.right, spere.position);
				if (r4 <= spere.radius) {
					if (rb || nr)
						return true;
				}
				r4 = PtPlaneDistance (planes.bottom, spere.position);
				if (r4 <= spere.radius) {
					if (rb || nb)
						return true;
				}
				r4 = PtPlaneDistance (planes.near, spere.position);
				if (r4 <= spere.radius) {
					if (nr || nb)
						return true;
				}
				return false;
			}
			break;
		case 5:
			{
				if ((PtPlaneValue (spere.position, planes.far) <= 0) && (PtPlaneValue (spere.position, planes.left) <= 0) && (PtPlaneValue (spere.position, planes.top) <= 0))
					return true;

				bool fl = CheckN3Plane (planes.fl, spere);
				bool lt = CheckN3Plane (planes.lt, spere);
				bool ft = CheckN3Plane (planes.ft, spere);

				float r5 = PtPlaneDistance (planes.left, spere.position);
				if (r5 <= spere.radius) {
					if (fl || lt)
						return true;
				}
				r5 = PtPlaneDistance (planes.far, spere.position);
				if (r5 <= spere.radius) {
					if (fl || ft)
						return true;
				}
				r5 = PtPlaneDistance (planes.top, spere.position);
				if (r5 <= spere.radius) {
					if (lt || ft)
						return true;
				}
				return false;
			}
			break;
		case 6:
			{
				if ((PtPlaneValue (spere.position, planes.far) <= 0) && (PtPlaneValue (spere.position, planes.right) <= 0) && (PtPlaneValue (spere.position, planes.top) <= 0))
					return true;

				bool rt = CheckN3Plane (planes.rt, spere);
				bool fr = CheckN3Plane (planes.fr, spere);
				bool ft = CheckN3Plane (planes.ft, spere);

				float r6 = PtPlaneDistance (planes.right, spere.position);
				if (r6 <= spere.radius) {
					if (rt || fr)
						return true;
				}
				r6 = PtPlaneDistance (planes.far, spere.position);
				if (r6 <= spere.radius) {
					if (fr || ft)
						return true;
				}
				r6 = PtPlaneDistance (planes.top, spere.position);
				if (r6 <= spere.radius) {
					if (rt || ft)
						return true;
				}
				return false;
			}
			break;
		case 7:
			{
				if ((PtPlaneValue (spere.position, planes.far) <= 0) && (PtPlaneValue (spere.position, planes.left) <= 0) && (PtPlaneValue (spere.position, planes.bottom) <= 0))
					return true;

				bool lb = CheckN3Plane (planes.lb, spere);
				bool fl = CheckN3Plane (planes.fl, spere);
				bool fb = CheckN3Plane (planes.fb, spere);

				float r7 = PtPlaneDistance (planes.left, spere.position);
				if (r7 <= spere.radius) {
					if (lb || fl)
						return true;
				}
				r7 = PtPlaneDistance (planes.bottom, spere.position);
				if (r7 <= spere.radius) {
					if (lb || fb)
						return true;
				}
				r7 = PtPlaneDistance (planes.far, spere.position);
				if (r7 <= spere.radius) {
					if (fl || fb)
						return true;
				}
				return false;
			}
			break;
		case 8:
			{
				if ((PtPlaneValue (spere.position, planes.far) <= 0) && (PtPlaneValue (spere.position, planes.right) <= 0) && (PtPlaneValue (spere.position, planes.bottom) <= 0))
					return true;

				bool rb = CheckN3Plane (planes.rb, spere);
				bool fr = CheckN3Plane (planes.fr, spere);
				bool fb = CheckN3Plane (planes.fb, spere);

				float r8 = PtPlaneDistance (planes.right, spere.position);
				if (r8 <= spere.radius) {
					if (rb || fr)
						return true;
				}
				r8 = PtPlaneDistance (planes.bottom, spere.position);
				if (r8 <= spere.radius) {
					if (rb || fb)
						return true;
				}
				r8 = PtPlaneDistance (planes.far, spere.position);
				if (r8 <= spere.radius) {
					if (fr || fb)
						return true;
				}
				return false;
			}
			break;
		}
		return false;
	#endif
	}
	#endif

	public static float PtPlaneValue(Vector3 p, Vector4 plane)
	{
		float ret = p.x * plane.x + p.y * plane.y + p.z * plane.z + plane.w;
		return ret;
	}

	public static bool PtPlaneDistanceLessEqualRadius(Vector4 pnl, BoundingSphere spere)
	{
		float r = PtPlaneDistance (pnl, spere.position);
		bool ret = r < spere.radius;
		return ret;
	}

	private static bool CheckN3Plane(Vector4 pnl, BoundingSphere spere)
	{
		Vector3 p = spere.position;
		float v = PtPlaneValue (p, pnl);
		if (v >= 0)
			return true;
		bool ret = PtPlaneDistanceLessEqualRadius (pnl, spere);
		return ret;
	}

	// 采用N3法向量解决在外屏幕R小于的情况
	private static Vector3 GetN3Normal(Vector4 leftPnl, Vector4 rightPnl)
	{
		Vector3 n1 = new Vector3 (-leftPnl.x, -leftPnl.y, -leftPnl.z);
		Vector3 n2 = new Vector3 (-rightPnl.x, -rightPnl.y, -rightPnl.z);

		Vector3 A = (n1 + n2);
		Vector3 B = Vector3.Cross (n1, n2);
		Vector3 AB = Vector3.Dot (A, B) * B;
		Vector3 n3 = A - AB;
		//n3.Normalize ();
		return n3;
	}

	private static Vector4 GetN3Plane(Vector4 leftPnl, Vector4 rightPnl, Vector3 orgin)
	{
		Vector3 n = GetN3Normal (leftPnl, rightPnl);
		float d = -n.x * orgin.x - n.y * orgin.y - n.z * orgin.z;
		Vector4 ret = new Vector4 (n.x, n.y, n.z, d);
		return ret;
	}

	internal static Vector4 NewPlane(Vector3 n, Vector3 p)
	{
		float d = -n.x * p.x - n.y * p.y - n.z * p.z;
		Vector4 ret = new Vector4 (n.x, n.y, n.z, d);
		return ret;
	}

	/*
	private static Vector3 m_ProjForward = new Vector3 (0, 0, 1);
	private static Vector3 m_ProjCenter = new Vector3(0f, 0f, 0.5f);
	private static Vector3 m_ProjUp = new Vector3 (0, 0, 1);
	private static Vector3 m_ProjRight = new Vector3(1, 0, 0);
	private static Vector4 m_ProjFBCenterPlane = NewPlane(-m_ProjForward, m_ProjCenter);
	private static Vector4 m_ProjTBCenterPlane = NewPlane(m_ProjUp, m_ProjCenter);
	private static Vector4 m_ProjLRCenterPanel = NewPlane(-m_ProjRight, m_ProjCenter);


	public static int GetPtProjPlaneAxis(Vector3 projPt)
	{
		// front back
		float v1 = PtPlaneValue(projPt, m_ProjFBCenterPlane);
		// top bottom
		float v2 = PtPlaneValue(projPt, m_ProjTBCenterPlane);
		// left right
		float v3 = PtPlaneValue(projPt, m_ProjLRCenterPanel);

		if (v1 >= 0) {
			if (v2 >= 0) {
				// 1, 2
				if (v3 >= 0) {
					return 1;
				}
				return 2;
			} else {
				// 3, 4
				if (v3 >= 0)
					return 3;
				return 4;
			}
		} else {
			if (v2 >= 0) {
				// 5, 6
				if (v3 >= 0)
					return 5;
				return 6;
			} else {
				// 7, 8
				if (v3 >= 0)
					return 7;
				return 8;
			}
		}
	}
	*/
	#if _USE_WORLD_AXIS

	public KdCameraPanels CalcPanels()
	{
		KdCameraPanels ret = new KdCameraPanels ();

#if _Use_World_SimpleMode
        //Matrix4x4 mat = CalcViewProjMatrix();
        Matrix4x4 mat = this.cullMatrix;
        ret.left = new Vector4(mat.m30 + mat.m00, mat.m31 + mat.m01, mat.m32 + mat.m02, mat.m33 + mat.m03);
        float len = 1.0f / (new Vector3(ret.left.x, ret.left.y, ret.left.z).magnitude);
        ret.left *= len;

		ret.right = new Vector4(mat.m30 - mat.m00, mat.m31 - mat.m01, mat.m32 - mat.m02, mat.m33 - mat.m03);
        len = 1.0f / (new Vector3(ret.right.x, ret.right.y, ret.right.z).magnitude);
        ret.right *= len;

        ret.top = new Vector4(mat.m30 - mat.m10, mat.m31 - mat.m11, mat.m32 - mat.m12, mat.m33 - mat.m13);
        len = 1.0f / (new Vector3(ret.top.x, ret.top.y, ret.top.z).magnitude);
        ret.top *= len;

        ret.bottom = new Vector4(mat.m30 + mat.m10, mat.m31 + mat.m11, mat.m32 + mat.m12, mat.m33 + mat.m13);
        len = 1.0f / (new Vector3(ret.bottom.x, ret.bottom.y, ret.bottom.z).magnitude);
        ret.bottom *= len;

        ret.near = new Vector4(mat.m30 + mat.m20, mat.m31 + mat.m21, mat.m32 + mat.m22, mat.m33 + mat.m23);
        len = 1.0f / (new Vector3(ret.near.x, ret.near.y, ret.near.z).magnitude);
        ret.near *= len;

        ret.far = new Vector4(mat.m30 - mat.m20, mat.m31 - mat.m21, mat.m32 - mat.m22, mat.m33 - mat.m23);
        len = 1.0f / (new Vector3(ret.far.x, ret.far.y, ret.far.z).magnitude);
        ret.far *= len;

#else
		Vector3 nCenter = this.nearCenter;
		Vector3 n = -this.lookAt;
		float dd = -n.x * nCenter.x - n.y * nCenter.y - n.z * nCenter.z;
		ret.near = new Vector4 (n.x, n.y, n.z, dd);

		Vector3 fCenter = this.farCenter;
		dd = n.x * fCenter.x + n.y * fCenter.y + n.z * fCenter.z;
		ret.far = new Vector4 (-n.x, -n.y, -n.z, dd);

		float halfNearHeight = this.nearHeight / 2.0f;
		float halfNearWidth = this.aspect * halfNearHeight;
		float halfFarHeight = this.farHeight / 2.0f;
		float halfFarWidth = this.aspect * halfFarHeight;

		Vector3 right = this.Right;
		Vector3 nHalfW = right * halfNearWidth;
		Vector3 nHalfH = up * halfNearHeight;
		Vector3 fHalfW = right * halfFarWidth;
		Vector3 fHalfH = up * halfFarHeight;


		// 近平面四点
		Vector3 a = -nHalfW + nHalfH + nCenter;
		Vector3 b = -nHalfW - nHalfH + nCenter;
		Vector3 c = nHalfW - nHalfH + nCenter;
		Vector3 d = nHalfW + nHalfH + nCenter;
		// 远平面四点
		Vector3 e = -fHalfW + fHalfH + fCenter;
		Vector3 f = -fHalfW - fHalfH + fCenter;
		Vector3 g = fHalfW - fHalfH + fCenter;
		Vector3 h = fHalfW + fHalfH + fCenter;

#if !_Use_World_SimpleMode
		ret.a = a;
		ret.b = b;
		ret.c = c;
		ret.d = d;
		ret.e = e;
		ret.f = f;
		ret.g = g;
		ret.h = h;
#endif

		Vector3 ab = b - a;
		Vector3 ae = e - a;
		n = Vector3.Cross (ab, ae);
		n.Normalize ();
		dd = -n.x * a.x - n.y * a.y - n.z * a.z;
		ret.left = new Vector4 (n.x, n.y, n.z, dd);

		Vector3 ad = d - a;
		n = Vector3.Cross (ae, ad);
		n.Normalize ();
		dd = -n.x * a.x - n.y * a.y - n.z * a.z;
		ret.top = new Vector4 (n.x, n.y, n.z, dd);

		Vector3 dc = c - d;
		Vector3 dh = h - d;
		n = Vector3.Cross (dh, dc);
		n.Normalize ();
		dd = -n.x * d.x - n.y * d.y - n.z * d.z;
		ret.right = new Vector4 (n.x, n.y, n.z, dd);

		Vector3 cg = g - c;
		Vector3 cb = b - c;
		n = Vector3.Cross (cg, cb);
		n.Normalize ();
		dd = -n.x * c.x - n.y * c.y - n.z * c.z;
		ret.bottom = new Vector4 (n.x, n.y, n.z, dd);

#if !_Use_World_SimpleMode
		// 交叉平面
		ret.lt = GetN3Plane (ret.left, ret.top, a);
		ret.lb = GetN3Plane(ret.left, ret.bottom, b);
		ret.rb = GetN3Plane (ret.right, ret.bottom, c);
		ret.rt = GetN3Plane (ret.right, ret.top, d);

		ret.nl = GetN3Plane (ret.near, ret.left, a);
		ret.nr = GetN3Plane (ret.near, ret.right, c);
		ret.nb = GetN3Plane (ret.near, ret.bottom, c);
		ret.nt = GetN3Plane (ret.near, ret.top, a);

		ret.fb = GetN3Plane (ret.far, ret.bottom, g);
		ret.fl = GetN3Plane (ret.far, ret.left, e);
		ret.fr = GetN3Plane (ret.far, ret.right, g);
		ret.ft = GetN3Plane (ret.far, ret.top, e);
		//-------------------
#endif
#endif

        return ret;
	}
	#endif
}

// 摄影机裁剪，采用以其他物体为参考，让摄影机平面转到物体空间中做
public class ObjectGPUInstancingMgr : Singleton<ObjectGPUInstancingMgr> {
	private IKdTreeCameraClipper m_Clipper = null;
	private KDTree.KDTree m_KdTree = null;
	private KDTree.KDQuery m_KdQuery = null;
	private KDTreeCameraClipThread m_ClipperThread = null;
	private Vector3[] m_VecArr = null;
	private KdTreeStatus m_KdTreeStatus = KdTreeStatus.None;
	private List<int> m_KdQueryIdxList = null;
	private bool m_UseThread = false;
	private List<KdTreeObj> m_KdTreeObjList = null;
	// 上一帧数据
	private HashSet<int> m_LastVisibleHash = null;
	private HashSet<int> m_TempHash = new HashSet<int>();

	// --------保证线程安全
	private KdCameraInfo m_KdCamInfo;
	// 交换队列
	private VisibleNode m_ChgVisibleQueue = null;
	private VisibleQueueStatus m_VisibleQueueStatus = VisibleQueueStatus.WaitQuery;
	// 投递结果
	//private Matrix4x4 m_KdCameraMVP = Matrix4x4.identity;
	// 摄影机6个平面
	private System.Threading.ReaderWriterLockSlim m_ReadWriterLock = new System.Threading.ReaderWriterLockSlim();

	private int m_VisibleCount = 0;
    private int m_KdTreeVisible = 0;
	//---------------------------

	~ObjectGPUInstancingMgr()
	{
		Dispose ();
	}

	public void Refresh()
	{
        m_ReadWriterLock.EnterWriteLock();
        m_VisibleQueueStatus = VisibleQueueStatus.Refresh;
        m_ReadWriterLock.ExitWriteLock();
	}

	private void Dispose()
	{
		if (m_ReadWriterLock != null) {
			m_ReadWriterLock.Dispose ();
			m_ReadWriterLock = null;
		}
	}

	private static readonly int _lockLimitMiliTime = 10;

	internal VisibleQueueStatus CurrentQueueStatus
	{
		get
		{
			if (m_ReadWriterLock.TryEnterReadLock (_lockLimitMiliTime)) {
				try {
					return m_VisibleQueueStatus;
				} finally {
					m_ReadWriterLock.ExitReadLock ();
				}
			} else
				return VisibleQueueStatus.Error;
		}
	}

	public int VisibleCount
	{
		get
		{
			
			if (m_ReadWriterLock.TryEnterReadLock (_lockLimitMiliTime)) {
				try {
					return m_VisibleCount;
				} finally {
					m_ReadWriterLock.ExitReadLock ();
				}
			} else
				return -1;
		}
	}

    public int KdTreeVisibleCount {
        get {
            if (m_ReadWriterLock.TryEnterReadLock(_lockLimitMiliTime)) {
                try {
                    return m_KdTreeVisible;
                } finally {
                    m_ReadWriterLock.ExitReadLock();
                }
            } else
                return -1;
        }
    }

	public void UptoVisibleQueue(ref VisibleNode root)
	{
		VisibleQueueStatus status = this.CurrentQueueStatus;
		if (status == VisibleQueueStatus.WaitChange) {
			if (m_ReadWriterLock.TryEnterWriteLock (_lockLimitMiliTime)) {
				try {
					m_VisibleQueueStatus = VisibleQueueStatus.WaitQuery;
					root = m_ChgVisibleQueue;
					m_ChgVisibleQueue = null;
				} finally {
					m_ReadWriterLock.ExitWriteLock ();
				}
			}
		}
	}


	private static readonly int _cThreadWait = 0;
	// 线程返回
	internal void _ThreadRun(bool doWait = true)
	{
		if (m_KdTree != null) {
			if (m_KdTreeStatus == KdTreeStatus.WaitRebuild) {
				m_KdTree.Rebuild ();
				m_KdTreeStatus = KdTreeStatus.Rebuilded;

                m_ReadWriterLock.EnterWriteLock();
                m_VisibleQueueStatus = VisibleQueueStatus.WaitQuery;
                m_ReadWriterLock.ExitWriteLock();

            } else if (m_KdTreeStatus == KdTreeStatus.Rebuilded) {

				VisibleQueueStatus status = this.CurrentQueueStatus;

				if (status == VisibleQueueStatus.Refresh) {
					if (m_LastVisibleHash != null)
						m_LastVisibleHash.Clear ();
					status = VisibleQueueStatus.WaitQuery;
				}

				if (status == VisibleQueueStatus.WaitQuery) {

					if (m_KdQuery == null)
						m_KdQuery = new KDTree.KDQuery (100);
					if (m_KdQueryIdxList == null)
						m_KdQueryIdxList = new List<int> ();
					else
						m_KdQueryIdxList.Clear ();

					Vector3 center;
					KdCameraInfo camInfo;
					m_ReadWriterLock.EnterReadLock ();
					try {
						camInfo = m_KdCamInfo;
					} finally {
						m_ReadWriterLock.ExitReadLock ();
					}
					float radius = camInfo.far;
					m_KdQuery.Radius (m_KdTree, camInfo.position, radius, m_KdQueryIdxList);

					int visibleCount = 0;
                    int kdTreeVisibleCount = m_KdQueryIdxList.Count;
					m_TempHash.Clear ();

					if (m_KdQueryIdxList.Count > 0) {

						// 计算出摄影机面板
						#if _USE_WORLD_AXIS
					KdCameraPanels camPanels = camInfo.CalcPanels();
						#else
						Matrix4x4 mat = camInfo.CalcViewProjMatrix ();
						Vector4 nearPlane = camInfo.nearPlane;
						#endif

						for (int i = 0; i < m_KdQueryIdxList.Count; ++i) {
							int idx = m_KdQueryIdxList [i];
							if (idx < 0 || idx >= m_KdTreeObjList.Count || idx >= m_VecArr.Length)
								continue;

							//Vector3 pos = m_VecArr [idx];
							KdTreeObj obj = m_KdTreeObjList [idx];
							//pos = camMVP.MultiplyPoint (pos);
							#if _USE_WORLD_AXIS
						bool isVisible = camInfo.IsBoundingSphereIn (obj.boundSphere, camPanels);
							#else
							bool isVisible = KdCameraInfo.IsBoundingSphereIn (obj.boundSphere, mat, nearPlane); //KdCameraInfo.NewPlane(camInfo.lookAt, camInfo.position)
							#endif

							// 摄影机剪裁
							// 投递结果
							if (isVisible) {
								++visibleCount;

								m_TempHash.Add (obj.InstanceId);
							}
						}
						
					}

					// 查看和上一帧变化的
					VisibleNode chgRoot = null;
					VisibleNode chgEndNode = null;
					if (m_TempHash.Count <= 0) {
						// 从有变到没有
						if (m_LastVisibleHash != null) {
							var iter = m_LastVisibleHash.GetEnumerator ();
							while (iter.MoveNext ()) {
								var visibleNode = GetVisibleNode (iter.Current, false);
								AddVisibleQueue (ref chgRoot, ref chgEndNode, visibleNode);
							}
							iter.Dispose ();

							m_LastVisibleHash.Clear ();
						}
					} else {
						if (m_LastVisibleHash == null) {
							m_LastVisibleHash = new HashSet<int> ();
							var iter = m_TempHash.GetEnumerator ();
							while (iter.MoveNext ()) {
								var n = GetVisibleNode (iter.Current, true);
								AddVisibleQueue (ref chgRoot, ref chgEndNode, n);
							}
							iter.Dispose ();

							var tmp = m_LastVisibleHash;
							m_LastVisibleHash = m_TempHash;
							m_TempHash = tmp;
						} else {
							// rootNode下都是当前可见的
							var iter = m_TempHash.GetEnumerator ();
							while (iter.MoveNext ()) {
								bool isContains = m_LastVisibleHash.Contains (iter.Current);
								if (isContains) {
									m_LastVisibleHash.Remove (iter.Current);
									continue;
								}
								var chgN = GetVisibleNode (iter.Current, true);
								AddVisibleQueue (ref chgRoot, ref chgEndNode, chgN);
							}
							iter.Dispose ();
							// 剩下的就是从可见变成不可见
							iter = m_LastVisibleHash.GetEnumerator ();
							while (iter.MoveNext ()) {
								var chgN = GetVisibleNode (iter.Current, false);
								AddVisibleQueue (ref chgRoot, ref chgEndNode, chgN);
							}
							iter.Dispose ();
							// 交换一下
							HashSet<int> tmp = m_LastVisibleHash;
							m_LastVisibleHash = m_TempHash;
							m_TempHash = tmp;
							m_TempHash.Clear ();
						}
					}
						
					VisibleNode tmpNode;


					m_ReadWriterLock.EnterWriteLock ();
					try {
						tmpNode = m_ChgVisibleQueue;
						m_VisibleCount = visibleCount;
                        m_KdTreeVisible = kdTreeVisibleCount;
                        m_ChgVisibleQueue = chgRoot;
						m_VisibleQueueStatus = VisibleQueueStatus.WaitChange;
					} finally {
						m_ReadWriterLock.ExitWriteLock ();
					}

					if (tmpNode != null)
						ClearVisibleQueue (ref tmpNode);
				} else
					doWait = false;
			}

			if (doWait && _cThreadWait > 0)
				System.Threading.Thread.Sleep (_cThreadWait);
		}


	}

	private static void AddVisibleQueue(ref VisibleNode rootNode, ref VisibleNode endNode, VisibleNode visibleNode)
	{
		if (visibleNode == null)
			return;
		if (endNode == null)
			endNode = visibleNode;
		else {
			endNode.NextNode = visibleNode;
			endNode = visibleNode;
		}
		if (rootNode == null)
			rootNode = visibleNode;
	}

	private static void ClearVisibleQueue(ref VisibleNode root)
	{
		if (root == null)
			return;
		while (root != null) {
			VisibleNode nextNode = root.NextNode;
			root.Dispose ();
			root = nextNode;
		}
		root = null;
	}


	private void AttachThreadVars()
	{
		if (m_Clipper == null)
			return;
		// 保证不卡死主线程
		if (m_ReadWriterLock.TryEnterWriteLock (_lockLimitMiliTime)) {
			try {
				m_KdCamInfo = m_Clipper.CamInfo;
			} finally {
				m_ReadWriterLock.ExitWriteLock ();
			}
		}
	}

	private float m_LastUpdateTick = -1;
	private static readonly float _cDetlaUpdateTick = 0f;

	public void UpdateClipper()
	{
		UpdateClipper (Time.unscaledTime, true);
	}

    public void SetCameraSelector(kdTreeCameraSelector selector) {
        if (m_Clipper == null)
            return;
        m_Clipper.SetCameraSelector(selector);
    }


    public void UpdateClipper(float tick, bool mustUpdate = false)
	{
		if (m_Clipper == null)
			return;
		bool doUpdate = false;
		if (m_LastUpdateTick < 0) {
			m_LastUpdateTick = tick;
			doUpdate = true;
		} else if (tick - m_LastUpdateTick >= _cDetlaUpdateTick) {
			m_LastUpdateTick = tick;
			doUpdate = true;
		}

		if (!m_UseThread)
			doUpdate = true;

		if (doUpdate || mustUpdate) {
			AttachThreadVars ();
			if (!m_UseThread) {
				#if UNITY_5_3
				UnityEngine.Profiler.BeginSample("KdTreeSampler");
				#endif
				_ThreadRun (false);
				#if UNITY_5_3
				UnityEngine.Profiler.EndSample();
				#endif
			}
		}
	}

	public IKdTreeCameraClipper Clipper
	{
		get
		{
			return m_Clipper;
		}
	}

	public void RegisterClipper(IKdTreeCameraClipper clipper)
	{
		m_Clipper = clipper;
	}

	public void ClearClipper(IKdTreeCameraClipper clipper)
	{
		if (m_Clipper == clipper) {
			Reset ();
			m_Clipper = null;	
		}
	}

	public void Reset()
	{
		FreeThread ();
		m_KdTree = null;
		m_KdQuery = null;
		m_KdTreeStatus = KdTreeStatus.None;
		if (m_KdTreeObjList != null)
			m_KdTreeObjList.Clear ();
		if (m_TempHash != null)
			m_TempHash.Clear ();
		if (m_LastVisibleHash != null)
			m_LastVisibleHash.Clear ();
		if (m_ChgVisibleQueue != null) {
			ClearVisibleQueue (ref m_ChgVisibleQueue);
		}
		m_VisibleQueueStatus = VisibleQueueStatus.WaitQuery;
		m_KdCamInfo = new KdCameraInfo ();
		m_VisibleCount = 0;
        m_KdTreeVisible = 0;
    }

	void FreeThread()
	{
		if (m_ClipperThread != null) {
			m_ClipperThread.Dispose ();
			m_ClipperThread = null;
		}
	}

	void CreateThread()
	{
		// 开启线程
		if (m_UseThread && m_ClipperThread == null) {
			m_ClipperThread = new KDTreeCameraClipThread ();
			m_ClipperThread.Start ();
		}
	}

	// 进入场景只Build一次，一个场景Build一次，频率还好, 内部代码有GC
	public void Build(bool isUseThread = true)
	{
		Reset ();

		if (m_Clipper != null) {
			int cnt = m_Clipper.ObjectsCount;
			if (cnt <= 0)
				return;

			Action<Vector3[]> action = (Vector3[] arr) => {

				int idx = 0;
				bool isSet = false;

				if (m_KdTreeObjList == null)
					m_KdTreeObjList = new List<KdTreeObj>(m_Clipper.ObjectsCount);
				else
				{
					m_KdTreeObjList.Clear();
					m_KdTreeObjList.Capacity = m_Clipper.ObjectsCount;
				}

				m_Clipper.ForEachObjects (
					(Renderer render)=>
					{
						Vector3 pos = render.transform.position;
						arr[idx] = pos;
						++idx;
						isSet = true;

						BoundingSphere boundingSpere = KdTreeCameraClipper.GetBoundingSphere(render);


						KdTreeObj obj = new KdTreeObj();
						obj.InstanceId = render.GetInstanceID();
						obj.boundSphere = boundingSpere;
						m_KdTreeObjList.Add(obj);

					}
				);

				if (isSet)
				{
					m_KdTree = new KDTree.KDTree(arr, 32, false);
					m_KdTreeStatus = KdTreeStatus.WaitRebuild;
					m_UseThread = isUseThread;

					UpdateClipper();
					CreateThread();
				}
			};

			if (m_VecArr == null) {
				m_VecArr = new Vector3[cnt];
				action (m_VecArr);
			}
			else if (cnt > m_VecArr.Length) {
				Array.Resize (ref m_VecArr, cnt);
				action (m_VecArr);
			} else {
				UnsafeUtil.Vector3HackArraySizeCall (m_VecArr, cnt, action);
			}
		}
	}

	private static VisibleNode GetVisibleNode(int instanceId, bool isVisible)
	{
		// 内部带锁了，线程安全
		VisibleNode ret = AbstractPool<VisibleNode>.GetNode () as VisibleNode;
		ret.InstanceId = instanceId;
		ret.isVisible = isVisible;
		return ret;
	}

	/*
	void Update()
	{
		ObjectGPUInstancingMgr.GetInstance ().UpdateClipper (Time.unscaledTime);
	}*/

	// 设备是否支持GPU Instancing
	public static bool SupportGPUInstancing
	{
		get
		{
			// 是否支持GPU Instancing
			return SystemInfo.supportsInstancing;
		}
	}

	public void CameraSelectorUpdate (kdTreeCameraSelector selector)
	{
		if (m_Clipper == null || selector == null)
			return;
		m_Clipper.CameraSelectorUpdate (selector);
	}

	public void ClearSelector(kdTreeCameraSelector selector)
	{
		if (m_Clipper == null || selector == null)
			return;
		m_Clipper.ClearSelector (selector);
	}
}
