using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GPUInstancingGroupType
{
	GroupAutoVisible,
	GroupGPUInstancing
}

public class GPUInstancingGroup : MonoBehaviour {

	public List<Renderer> m_GPUInstancingList = null;
	public Camera m_TargetCamera = null;
	public GPUInstancingGroupType m_GroupType = GPUInstancingGroupType.GroupGPUInstancing;
	public bool m_UseChilds = false;
	private KdTreeCameraClipper m_OwnerClipper = null;

	void Start()
	{
		if (m_UseChilds) {
			Renderer[] rs = GetComponentsInChildren<Renderer> (true);
			if (m_GPUInstancingList == null)
				m_GPUInstancingList = new List<Renderer> (rs.Length);
			else
				m_GPUInstancingList.Clear ();
			m_GPUInstancingList.AddRange (rs);
		}
		if (m_GPUInstancingList != null && m_GPUInstancingList.Count > 0) {
			Camera cam = m_TargetCamera;
			if (cam == null)
				cam = Camera.main;
			if (cam == null)
				return;
			KdTreeCameraClipper clipper = cam.GetComponent<KdTreeCameraClipper> ();
			if (clipper == null) {
				clipper = cam.gameObject.AddComponent<KdTreeCameraClipper> ();
				clipper.AutoSetVisible = m_GroupType == GPUInstancingGroupType.GroupAutoVisible;
				clipper.OpenGPUInstancing = m_GroupType == GPUInstancingGroupType.GroupGPUInstancing;
				clipper.m_AspectScale = 2.0f;
				clipper.PerMaxVisbileCount = 500;
				clipper.m_UseThread = true;

				m_OwnerClipper = clipper;
			} else
				clipper.Clear ();
			clipper.ReBuild (m_GPUInstancingList);
		}
	}

	private void Reset()
	{
		if (m_OwnerClipper == null)
			return;
		GameObject.Destroy (m_OwnerClipper);
		m_OwnerClipper = null;
	}

	void OnApplicationQuit()
	{
		Reset ();
	}

	void OnDestroy()
	{
		Reset ();
	}
}
