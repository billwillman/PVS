using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class kdTreeCameraSelector : MonoBehaviour {

	private Camera m_TargetCam = null;

	public Camera TargetCamera
	{
		get
		{
			return m_TargetCam;
		}
	}

	void Awake()
	{
		m_TargetCam = GetComponent<Camera> ();
	}

	void OnEnable()
	{
		if (m_TargetCam != null && !m_TargetCam.enabled)
			m_TargetCam.enabled = true;

        ObjectGPUInstancingMgr.GetInstance ().CameraSelectorUpdate (this);
       // ObjectGPUInstancingMgr.GetInstance().SetCameraSelector(this);
       // ObjectGPUInstancingMgr.GetInstance ().UpdateClipper ();
	}

	private void Clear()
	{
		if (m_TargetCam != null && m_TargetCam.enabled)
			m_TargetCam.enabled = false;
		ObjectGPUInstancingMgr.GetInstance ().ClearSelector (this);
	}

	void OnDisable()
	{
		Clear ();
	}

	void OnDestroy()
	{
		if (!KdTreeCameraClipper.IsAppQuit)
			Clear ();
	}

	void Update()
	{
		ObjectGPUInstancingMgr.GetInstance ().CameraSelectorUpdate (this);
	}
}
